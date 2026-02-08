// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using DotCompute.Abstractions.Observability;
using DotCompute.Abstractions.RingKernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Observability;

/// <summary>
/// Prometheus-compatible metrics exporter for Ring Kernels.
/// </summary>
/// <remarks>
/// Exports Ring Kernel metrics in Prometheus text format for scraping
/// by Prometheus server or compatible monitoring systems.
/// </remarks>
public sealed partial class RingKernelMetricsExporter : IDisposable
{
    private readonly ILogger<RingKernelMetricsExporter> _logger;
    private readonly RingKernelMetricsExporterOptions _options;
    private readonly IRingKernelRuntime? _runtime;
    private readonly IRingKernelHealthCheck? _healthCheck;
    private readonly ConcurrentDictionary<string, KernelMetricsSnapshot> _snapshots = new();
    private readonly Timer _collectionTimer;
    private volatile bool _disposed;

    // Event IDs: 9800-9849 for RingKernelMetricsExporter
    [LoggerMessage(EventId = 9800, Level = LogLevel.Debug,
        Message = "Collected metrics for {KernelCount} kernels")]
    private static partial void LogMetricsCollected(ILogger logger, int kernelCount);

    [LoggerMessage(EventId = 9801, Level = LogLevel.Error,
        Message = "Failed to collect metrics for kernel {KernelId}")]
    private static partial void LogMetricsCollectionFailed(ILogger logger, Exception ex, string kernelId);

    [LoggerMessage(EventId = 9802, Level = LogLevel.Information,
        Message = "Ring Kernel metrics exporter started with {Interval}s collection interval")]
    private static partial void LogExporterStarted(ILogger logger, int interval);

    /// <summary>
    /// Creates a new Ring Kernel metrics exporter.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Exporter configuration options.</param>
    /// <param name="runtime">Optional Ring Kernel runtime for live metrics.</param>
    /// <param name="healthCheck">Optional health check for health metrics.</param>
    public RingKernelMetricsExporter(
        ILogger<RingKernelMetricsExporter> logger,
        IOptions<RingKernelMetricsExporterOptions> options,
        IRingKernelRuntime? runtime = null,
        IRingKernelHealthCheck? healthCheck = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new RingKernelMetricsExporterOptions();
        _runtime = runtime;
        _healthCheck = healthCheck;

        _collectionTimer = new Timer(
            CollectMetricsCallback,
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(_options.CollectionIntervalSeconds));

        LogExporterStarted(_logger, _options.CollectionIntervalSeconds);
    }

    /// <summary>
    /// Registers a kernel for metrics collection.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    public void RegisterKernel(string kernelId)
    {
        ArgumentNullException.ThrowIfNull(kernelId);
        _snapshots.TryAdd(kernelId, new KernelMetricsSnapshot { KernelId = kernelId });
    }

    /// <summary>
    /// Unregisters a kernel from metrics collection.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    public void UnregisterKernel(string kernelId)
    {
        ArgumentNullException.ThrowIfNull(kernelId);
        _snapshots.TryRemove(kernelId, out _);
    }

    /// <summary>
    /// Updates metrics snapshot for a kernel from telemetry data.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="telemetry">Telemetry data.</param>
    /// <param name="uptime">Kernel uptime.</param>
    public void UpdateFromTelemetry(string kernelId, RingKernelTelemetry telemetry, TimeSpan uptime)
    {
        var snapshot = _snapshots.GetOrAdd(kernelId, _ => new KernelMetricsSnapshot { KernelId = kernelId });

        snapshot.MessagesProcessed = telemetry.MessagesProcessed;
        snapshot.MessagesDropped = telemetry.MessagesDropped;
        snapshot.QueueDepth = telemetry.QueueDepth;
        snapshot.AverageLatencyNanos = telemetry.AverageLatencyNanos;
        snapshot.MaxLatencyNanos = telemetry.MaxLatencyNanos;
        snapshot.MinLatencyNanos = telemetry.MinLatencyNanos;
        snapshot.ErrorCode = telemetry.ErrorCode;
        snapshot.Throughput = telemetry.GetThroughput(uptime.TotalSeconds);
        snapshot.LastUpdate = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Exports all metrics in Prometheus text format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prometheus-formatted metrics string.</returns>
    public async Task<string> ExportAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Refresh metrics from runtime if available
        await CollectMetricsAsync(cancellationToken).ConfigureAwait(false);

        var sb = new StringBuilder();

        // Write HELP and TYPE headers
        WriteMetricHeader(sb, "ring_kernel_messages_processed_total",
            "Total messages processed by Ring Kernels", "counter");
        WriteMetricHeader(sb, "ring_kernel_messages_dropped_total",
            "Total messages dropped by Ring Kernels", "counter");
        WriteMetricHeader(sb, "ring_kernel_queue_depth",
            "Current queue depth of Ring Kernels", "gauge");
        WriteMetricHeader(sb, "ring_kernel_throughput_messages_per_second",
            "Current message throughput", "gauge");
        WriteMetricHeader(sb, "ring_kernel_latency_nanoseconds",
            "Average message processing latency in nanoseconds", "gauge");
        WriteMetricHeader(sb, "ring_kernel_latency_max_nanoseconds",
            "Maximum message processing latency in nanoseconds", "gauge");
        WriteMetricHeader(sb, "ring_kernel_latency_min_nanoseconds",
            "Minimum message processing latency in nanoseconds", "gauge");
        WriteMetricHeader(sb, "ring_kernel_error_code",
            "Current error code (0 = no error)", "gauge");
        WriteMetricHeader(sb, "ring_kernel_health_status",
            "Health status (0=unhealthy, 1=healthy)", "gauge");
        WriteMetricHeader(sb, "ring_kernel_active",
            "Whether kernel is active (0=inactive, 1=active)", "gauge");
        WriteMetricHeader(sb, "ring_kernel_uptime_seconds",
            "Kernel uptime in seconds", "gauge");

        // Write metrics for each kernel
        foreach (var kvp in _snapshots)
        {
            var kernelId = kvp.Key;
            var snapshot = kvp.Value;
            var labels = BuildLabels(kernelId, snapshot);

            WriteMetric(sb, "ring_kernel_messages_processed_total", snapshot.MessagesProcessed, labels);
            WriteMetric(sb, "ring_kernel_messages_dropped_total", snapshot.MessagesDropped, labels);
            WriteMetric(sb, "ring_kernel_queue_depth", snapshot.QueueDepth, labels);
            WriteMetric(sb, "ring_kernel_throughput_messages_per_second", snapshot.Throughput, labels);
            WriteMetric(sb, "ring_kernel_latency_nanoseconds", snapshot.AverageLatencyNanos, labels);
            WriteMetric(sb, "ring_kernel_latency_max_nanoseconds", snapshot.MaxLatencyNanos, labels);
            WriteMetric(sb, "ring_kernel_latency_min_nanoseconds",
                snapshot.MinLatencyNanos == ulong.MaxValue ? 0 : snapshot.MinLatencyNanos, labels);
            WriteMetric(sb, "ring_kernel_error_code", snapshot.ErrorCode, labels);
            WriteMetric(sb, "ring_kernel_health_status", snapshot.IsHealthy ? 1 : 0, labels);
            WriteMetric(sb, "ring_kernel_active", snapshot.IsActive ? 1 : 0, labels);
            WriteMetric(sb, "ring_kernel_uptime_seconds", snapshot.UptimeSeconds, labels);
        }

        // Add histogram metrics if enabled
        if (_options.EnableHistograms)
        {
            WriteLatencyHistogram(sb);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports metrics to a file.
    /// </summary>
    /// <param name="filePath">Output file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExportToFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var metrics = await ExportAsync(cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(filePath, metrics, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a summary of current metrics.
    /// </summary>
    /// <returns>Metrics summary.</returns>
    public RingKernelMetricsSummary GetSummary()
    {
        ThrowIfDisposed();

        var snapshots = _snapshots.Values.ToList();
        var activeSnapshots = snapshots.Where(s => s.IsActive).ToList();
        var snapshotsWithLatency = snapshots.Where(s => s.AverageLatencyNanos > 0).ToList();

        return new RingKernelMetricsSummary
        {
            TotalKernels = snapshots.Count,
            ActiveKernels = activeSnapshots.Count,
            HealthyKernels = snapshots.Count(s => s.IsHealthy),
            TotalMessagesProcessed = snapshots.Sum(s => (long)s.MessagesProcessed),
            TotalMessagesDropped = snapshots.Sum(s => (long)s.MessagesDropped),
            AverageThroughput = activeSnapshots.Count > 0
                ? activeSnapshots.Average(s => s.Throughput)
                : 0,
            AverageLatencyNanos = snapshotsWithLatency.Count > 0
                ? snapshotsWithLatency.Average(s => (double)s.AverageLatencyNanos)
                : 0,
            MaxQueueDepth = snapshots.Count > 0
                ? snapshots.Max(s => s.QueueDepth)
                : 0,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private async Task CollectMetricsAsync(CancellationToken cancellationToken)
    {
        if (_runtime == null)
        {
            return;
        }

        try
        {
            var kernelIds = await _runtime.ListKernelsAsync().ConfigureAwait(false);

            foreach (var kernelId in kernelIds)
            {
                try
                {
                    var status = await _runtime.GetStatusAsync(kernelId, cancellationToken)
                        .ConfigureAwait(false);

                    var snapshot = _snapshots.GetOrAdd(kernelId, _ => new KernelMetricsSnapshot { KernelId = kernelId });
                    snapshot.IsActive = status.IsActive;
                    snapshot.UptimeSeconds = status.Uptime.TotalSeconds;
                    snapshot.MessagesProcessed = (ulong)status.MessagesProcessed;
                    snapshot.QueueDepth = status.MessagesPending;

                    try
                    {
                        var telemetry = await _runtime.GetTelemetryAsync(kernelId, cancellationToken)
                            .ConfigureAwait(false);

                        snapshot.MessagesDropped = telemetry.MessagesDropped;
                        snapshot.AverageLatencyNanos = telemetry.AverageLatencyNanos;
                        snapshot.MaxLatencyNanos = telemetry.MaxLatencyNanos;
                        snapshot.MinLatencyNanos = telemetry.MinLatencyNanos;
                        snapshot.ErrorCode = telemetry.ErrorCode;
                        snapshot.Throughput = telemetry.GetThroughput(status.Uptime.TotalSeconds);
                        snapshot.IsHealthy = telemetry.IsHealthy(
                            DateTimeOffset.UtcNow.Ticks * 100, // Convert to nanos
                            (long)_options.StuckKernelThreshold.TotalSeconds * 1_000_000_000);
                    }
                    catch (InvalidOperationException)
                    {
                        // Telemetry not enabled
                    }

                    // Update from health check if available
                    if (_healthCheck != null)
                    {
                        var healthEntry = _healthCheck.GetCachedHealth(kernelId);
                        if (healthEntry != null)
                        {
                            snapshot.IsHealthy = healthEntry.Status == RingKernelHealthStatus.Healthy;
                        }
                    }

                    snapshot.LastUpdate = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    LogMetricsCollectionFailed(_logger, ex, kernelId);
                }
            }

            LogMetricsCollected(_logger, kernelIds.Count);
        }
        catch (Exception ex)
        {
            LogMetricsCollectionFailed(_logger, ex, "all");
        }
    }

    private void CollectMetricsCallback(object? state)
    {
        if (_disposed)
        {
            return;
        }

        _ = CollectMetricsAsync(CancellationToken.None);
    }

    private string BuildLabels(string kernelId, KernelMetricsSnapshot snapshot)
    {
        var labels = new List<string>
        {
            $"kernel_id=\"{EscapeLabel(kernelId)}\""
        };

        if (!string.IsNullOrEmpty(snapshot.Backend))
        {
            labels.Add($"backend=\"{EscapeLabel(snapshot.Backend)}\"");
        }

        foreach (var tag in _options.GlobalLabels)
        {
            labels.Add($"{EscapeLabel(tag.Key)}=\"{EscapeLabel(tag.Value)}\"");
        }

        return string.Join(",", labels);
    }

    private static void WriteMetricHeader(StringBuilder sb, string name, string help, string type)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP {name} {help}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE {name} {type}");
    }

    private static void WriteMetric(StringBuilder sb, string name, double value, string labels)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"{name}{{{labels}}} {value:G17}");
    }

    private static void WriteMetric(StringBuilder sb, string name, ulong value, string labels)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"{name}{{{labels}}} {value}");
    }

    private static void WriteMetric(StringBuilder sb, string name, int value, string labels)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"{name}{{{labels}}} {value}");
    }

    private void WriteLatencyHistogram(StringBuilder sb)
    {
        // Write histogram buckets
        sb.AppendLine("# HELP ring_kernel_message_latency_ms Message processing latency histogram");
        sb.AppendLine("# TYPE ring_kernel_message_latency_ms histogram");

        foreach (var kvp in _snapshots)
        {
            var kernelId = kvp.Key;
            var snapshot = kvp.Value;
            var labels = BuildLabels(kernelId, snapshot);

            // Simple histogram with predefined buckets
            var avgLatencyMs = snapshot.AverageLatencyNanos / 1_000_000.0;
            var buckets = _options.LatencyBuckets;

            ulong cumulativeCount = 0;
            foreach (var bucket in buckets)
            {
                if (avgLatencyMs <= bucket)
                {
                    cumulativeCount = snapshot.MessagesProcessed;
                }
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"ring_kernel_message_latency_ms_bucket{{{labels},le=\"{bucket}\"}} {cumulativeCount}");
            }

            sb.AppendLine(CultureInfo.InvariantCulture,
                $"ring_kernel_message_latency_ms_bucket{{{labels},le=\"+Inf\"}} {snapshot.MessagesProcessed}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"ring_kernel_message_latency_ms_sum{{{labels}}} {avgLatencyMs * snapshot.MessagesProcessed}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"ring_kernel_message_latency_ms_count{{{labels}}} {snapshot.MessagesProcessed}");
        }
    }

    private static string EscapeLabel(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _collectionTimer.Dispose();
        _snapshots.Clear();
    }

    private sealed class KernelMetricsSnapshot
    {
        public required string KernelId { get; init; }
        public string? Backend { get; set; }
        public bool IsActive { get; set; }
        public bool IsHealthy { get; set; } = true;
        public double UptimeSeconds { get; set; }
        public ulong MessagesProcessed { get; set; }
        public ulong MessagesDropped { get; set; }
        public int QueueDepth { get; set; }
        public double Throughput { get; set; }
        public ulong AverageLatencyNanos { get; set; }
        public ulong MaxLatencyNanos { get; set; }
        public ulong MinLatencyNanos { get; set; } = ulong.MaxValue;
        public ushort ErrorCode { get; set; }
        public DateTimeOffset LastUpdate { get; set; }
    }
}

/// <summary>
/// Configuration options for Ring Kernel metrics exporter.
/// </summary>
public sealed class RingKernelMetricsExporterOptions
{
    /// <summary>
    /// Gets or sets the collection interval in seconds.
    /// </summary>
    public int CollectionIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets whether to enable histogram metrics.
    /// </summary>
    public bool EnableHistograms { get; set; } = true;

    /// <summary>
    /// Gets or sets the latency histogram buckets in milliseconds.
    /// </summary>
    public IReadOnlyList<double> LatencyBuckets { get; set; } =
    [
        0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10
    ];

    /// <summary>
    /// Gets or sets the stuck kernel threshold.
    /// </summary>
    public TimeSpan StuckKernelThreshold { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets global labels to add to all metrics.
    /// </summary>
    public IDictionary<string, string> GlobalLabels { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the service name label.
    /// </summary>
    public string ServiceName { get; set; } = "dotcompute_ring_kernels";
}

/// <summary>
/// Summary of Ring Kernel metrics.
/// </summary>
public sealed class RingKernelMetricsSummary
{
    /// <summary>
    /// Gets the total number of kernels.
    /// </summary>
    public int TotalKernels { get; init; }

    /// <summary>
    /// Gets the number of active kernels.
    /// </summary>
    public int ActiveKernels { get; init; }

    /// <summary>
    /// Gets the number of healthy kernels.
    /// </summary>
    public int HealthyKernels { get; init; }

    /// <summary>
    /// Gets the total messages processed across all kernels.
    /// </summary>
    public long TotalMessagesProcessed { get; init; }

    /// <summary>
    /// Gets the total messages dropped across all kernels.
    /// </summary>
    public long TotalMessagesDropped { get; init; }

    /// <summary>
    /// Gets the average throughput across active kernels.
    /// </summary>
    public double AverageThroughput { get; init; }

    /// <summary>
    /// Gets the average latency in nanoseconds.
    /// </summary>
    public double AverageLatencyNanos { get; init; }

    /// <summary>
    /// Gets the maximum queue depth.
    /// </summary>
    public int MaxQueueDepth { get; init; }

    /// <summary>
    /// Gets the timestamp of this summary.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
