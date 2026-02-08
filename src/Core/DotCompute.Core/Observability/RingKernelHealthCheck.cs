// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Observability;
using DotCompute.Abstractions.RingKernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Observability;

/// <summary>
/// Health check implementation for Ring Kernels.
/// </summary>
/// <remarks>
/// Provides comprehensive health monitoring including:
/// - Stuck kernel detection
/// - Queue depth monitoring
/// - Latency threshold monitoring
/// - Error state detection
/// </remarks>
public sealed partial class RingKernelHealthCheck : IRingKernelHealthCheck, IDisposable
{
    private readonly ILogger<RingKernelHealthCheck> _logger;
    private readonly RingKernelHealthOptions _globalOptions;
    private readonly IRingKernelRuntime? _runtime;
    private readonly IRingKernelInstrumentation? _instrumentation;
    private readonly ConcurrentDictionary<string, KernelHealthState> _kernelStates = new();
    private readonly ConcurrentDictionary<string, RingKernelHealthOptions> _kernelOptions = new();
    private readonly Timer _backgroundCheckTimer;
    private bool _disposed;

    // Event IDs: 9750-9799 for RingKernelHealthCheck
    [LoggerMessage(EventId = 9750, Level = LogLevel.Information,
        Message = "Kernel {KernelId} registered for health monitoring")]
    private static partial void LogKernelRegistered(ILogger logger, string kernelId);

    [LoggerMessage(EventId = 9751, Level = LogLevel.Information,
        Message = "Kernel {KernelId} unregistered from health monitoring")]
    private static partial void LogKernelUnregistered(ILogger logger, string kernelId);

    [LoggerMessage(EventId = 9752, Level = LogLevel.Warning,
        Message = "Kernel {KernelId} health changed from {Previous} to {Current}: {Description}")]
    private static partial void LogHealthChanged(
        ILogger logger, string kernelId, RingKernelHealthStatus previous,
        RingKernelHealthStatus current, string description);

    [LoggerMessage(EventId = 9753, Level = LogLevel.Error,
        Message = "Health check failed for kernel {KernelId}: {Error}")]
    private static partial void LogHealthCheckFailed(
        ILogger logger, Exception ex, string kernelId, string error);

    [LoggerMessage(EventId = 9754, Level = LogLevel.Debug,
        Message = "Completed health check for {KernelCount} kernels in {Duration}ms")]
    private static partial void LogHealthCheckCompleted(
        ILogger logger, int kernelCount, double duration);

    /// <inheritdoc />
    public IReadOnlyCollection<string> RegisteredKernels => _kernelStates.Keys.ToList();

    /// <inheritdoc />
    public event EventHandler<RingKernelHealthChangedEventArgs>? HealthChanged;

    /// <summary>
    /// Creates a new Ring Kernel health check instance.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Global health check options.</param>
    /// <param name="runtime">Optional Ring Kernel runtime for live health checks.</param>
    /// <param name="instrumentation">Optional instrumentation for metrics.</param>
    public RingKernelHealthCheck(
        ILogger<RingKernelHealthCheck> logger,
        IOptions<RingKernelHealthOptions> options,
        IRingKernelRuntime? runtime = null,
        IRingKernelInstrumentation? instrumentation = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _globalOptions = options?.Value ?? new RingKernelHealthOptions();
        _runtime = runtime;
        _instrumentation = instrumentation;

        // Start background health check timer
        _backgroundCheckTimer = new Timer(
            PerformBackgroundHealthChecks,
            null,
            _globalOptions.CacheDuration,
            _globalOptions.CacheDuration);
    }

    /// <inheritdoc />
    public async Task<RingKernelHealthReport> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var sw = Stopwatch.StartNew();
        var entries = new Dictionary<string, RingKernelHealthEntry>();
        var overallStatus = RingKernelHealthStatus.Healthy;

        foreach (var kernelId in _kernelStates.Keys)
        {
            try
            {
                var entry = await CheckKernelHealthAsync(kernelId, cancellationToken).ConfigureAwait(false);
                entries[kernelId] = entry;

                if (entry.Status == RingKernelHealthStatus.Unhealthy)
                {
                    overallStatus = RingKernelHealthStatus.Unhealthy;
                }
                else if (entry.Status == RingKernelHealthStatus.Degraded &&
                         overallStatus != RingKernelHealthStatus.Unhealthy)
                {
                    overallStatus = RingKernelHealthStatus.Degraded;
                }
            }
            catch (Exception ex)
            {
                LogHealthCheckFailed(_logger, ex, kernelId, ex.Message);
                entries[kernelId] = CreateErrorEntry(kernelId, ex);
                overallStatus = RingKernelHealthStatus.Unhealthy;
            }
        }

        sw.Stop();
        LogHealthCheckCompleted(_logger, entries.Count, sw.Elapsed.TotalMilliseconds);

        return new RingKernelHealthReport
        {
            Status = overallStatus,
            TotalDuration = sw.Elapsed,
            Entries = entries
        };
    }

    /// <inheritdoc />
    public async Task<RingKernelHealthEntry> CheckKernelHealthAsync(
        string kernelId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(kernelId);

        var sw = Stopwatch.StartNew();
        var options = GetKernelOptions(kernelId);

        try
        {
            // Check if we have a cached result that's still valid
            if (_kernelStates.TryGetValue(kernelId, out var state) &&
                state.LastEntry != null &&
                DateTimeOffset.UtcNow - state.LastEntry.Timestamp < options.CacheDuration)
            {
                return state.LastEntry;
            }

            // Perform the actual health check
            var entry = await PerformHealthCheckAsync(kernelId, options, cancellationToken)
                .ConfigureAwait(false);

            sw.Stop();
            entry = entry with { Duration = sw.Elapsed };

            // Update state and notify if changed
            UpdateHealthState(kernelId, entry);

            return entry;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogHealthCheckFailed(_logger, ex, kernelId, ex.Message);
            var errorEntry = CreateErrorEntry(kernelId, ex);
            UpdateHealthState(kernelId, errorEntry);
            return errorEntry;
        }
    }

    /// <inheritdoc />
    public RingKernelHealthEntry? GetCachedHealth(string kernelId)
    {
        ThrowIfDisposed();

        if (_kernelStates.TryGetValue(kernelId, out var state))
        {
            return state.LastEntry;
        }

        return null;
    }

    /// <inheritdoc />
    public void RegisterKernel(string kernelId, RingKernelHealthOptions? options = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(kernelId);

        _kernelStates[kernelId] = new KernelHealthState();

        if (options != null)
        {
            _kernelOptions[kernelId] = options;
        }

        LogKernelRegistered(_logger, kernelId);
    }

    /// <inheritdoc />
    public void UnregisterKernel(string kernelId)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(kernelId);

        _kernelStates.TryRemove(kernelId, out _);
        _kernelOptions.TryRemove(kernelId, out _);

        LogKernelUnregistered(_logger, kernelId);
    }

    private async Task<RingKernelHealthEntry> PerformHealthCheckAsync(
        string kernelId,
        RingKernelHealthOptions options,
        CancellationToken cancellationToken)
    {
        var status = RingKernelHealthStatus.Healthy;
        var description = "Kernel is healthy";
        var data = new Dictionary<string, object>();
        var tags = new List<string>(options.Tags);

        var isActive = false;
        TimeSpan? uptime = null;
        DateTimeOffset? lastMessageProcessed = null;
        int? queueDepth = null;
        double? throughput = null;
        ulong? averageLatencyNanos = null;
        int? errorCode = null;

        if (_runtime != null)
        {
            try
            {
                // Get kernel status
                var kernelStatus = await _runtime.GetStatusAsync(kernelId, cancellationToken)
                    .ConfigureAwait(false);

                isActive = kernelStatus.IsActive;
                uptime = kernelStatus.Uptime;
                queueDepth = kernelStatus.MessagesPending;

                data["isLaunched"] = kernelStatus.IsLaunched;
                data["isActive"] = kernelStatus.IsActive;
                data["isTerminating"] = kernelStatus.IsTerminating;
                data["messagesProcessed"] = kernelStatus.MessagesProcessed;
                data["gridSize"] = kernelStatus.GridSize;
                data["blockSize"] = kernelStatus.BlockSize;

                // Get telemetry for detailed health
                try
                {
                    var telemetry = await _runtime.GetTelemetryAsync(kernelId, cancellationToken)
                        .ConfigureAwait(false);

                    averageLatencyNanos = telemetry.AverageLatencyNanos;
                    errorCode = telemetry.ErrorCode;

                    data["messagesDropped"] = telemetry.MessagesDropped;
                    data["queueDepth"] = telemetry.QueueDepth;
                    data["maxLatencyNanos"] = telemetry.MaxLatencyNanos;
                    data["minLatencyNanos"] = telemetry.MinLatencyNanos;

                    queueDepth = telemetry.QueueDepth;

                    // Convert timestamp to DateTimeOffset
                    if (telemetry.LastProcessedTimestamp > 0)
                    {
                        lastMessageProcessed = DateTimeOffset.UtcNow
                            .AddTicks(-telemetry.LastProcessedTimestamp / 100); // Convert nanos to ticks
                    }

                    // Calculate throughput
                    if (uptime.HasValue && uptime.Value.TotalSeconds > 0)
                    {
                        throughput = telemetry.MessagesProcessed / uptime.Value.TotalSeconds;
                    }

                    // Check for errors
                    if (telemetry.ErrorCode != 0)
                    {
                        status = RingKernelHealthStatus.Unhealthy;
                        description = $"Kernel error code: {telemetry.ErrorCode}";
                        tags.Add("error");
                    }

                    // Check for stuck kernel
                    if (isActive && telemetry.MessagesProcessed > 0)
                    {
                        var currentTimestamp = DateTimeOffset.UtcNow.Ticks * 100; // Convert to nanos
                        var stuckThresholdNanos = options.StuckThreshold.Ticks * 100;

                        if (!telemetry.IsHealthy(currentTimestamp, stuckThresholdNanos))
                        {
                            status = RingKernelHealthStatus.Unhealthy;
                            description = "Kernel appears to be stuck (no messages processed within threshold)";
                            tags.Add("stuck");
                        }
                    }

                    // Check latency thresholds
                    if (averageLatencyNanos.HasValue && averageLatencyNanos > 0)
                    {
                        var avgLatencyMs = averageLatencyNanos.Value / 1_000_000.0;

                        if (avgLatencyMs >= options.LatencyCriticalThresholdMs)
                        {
                            status = RingKernelHealthStatus.Unhealthy;
                            description = $"Critical latency threshold exceeded: {avgLatencyMs:F2}ms";
                            tags.Add("high-latency");
                        }
                        else if (avgLatencyMs >= options.LatencyWarningThresholdMs &&
                                 status == RingKernelHealthStatus.Healthy)
                        {
                            status = RingKernelHealthStatus.Degraded;
                            description = $"Latency warning threshold exceeded: {avgLatencyMs:F2}ms";
                            tags.Add("elevated-latency");
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Telemetry not enabled for this kernel
                    data["telemetryEnabled"] = false;
                }

                // Get metrics for throughput
                try
                {
                    var metrics = await _runtime.GetMetricsAsync(kernelId, cancellationToken)
                        .ConfigureAwait(false);

                    throughput = metrics.ThroughputMsgsPerSec;
                    data["inputQueueUtilization"] = metrics.InputQueueUtilization;
                    data["outputQueueUtilization"] = metrics.OutputQueueUtilization;
                    data["currentMemoryBytes"] = metrics.CurrentMemoryBytes;
                    data["peakMemoryBytes"] = metrics.PeakMemoryBytes;

                    // Check queue depth thresholds
                    if (metrics.InputQueueUtilization >= options.QueueDepthCriticalThreshold)
                    {
                        status = RingKernelHealthStatus.Unhealthy;
                        description = $"Critical queue depth: {metrics.InputQueueUtilization:P0}";
                        tags.Add("queue-full");
                    }
                    else if (metrics.InputQueueUtilization >= options.QueueDepthWarningThreshold &&
                             status == RingKernelHealthStatus.Healthy)
                    {
                        status = RingKernelHealthStatus.Degraded;
                        description = $"High queue depth: {metrics.InputQueueUtilization:P0}";
                        tags.Add("queue-high");
                    }

                    // Check minimum throughput
                    if (isActive && options.MinimumThroughput > 0 &&
                        metrics.ThroughputMsgsPerSec < options.MinimumThroughput)
                    {
                        if (status == RingKernelHealthStatus.Healthy)
                        {
                            status = RingKernelHealthStatus.Degraded;
                            description = $"Low throughput: {metrics.ThroughputMsgsPerSec:F2} msg/s";
                            tags.Add("low-throughput");
                        }
                    }
                }
                catch (Exception)
                {
                    // Metrics not available
                }
            }
            catch (ArgumentException)
            {
                status = RingKernelHealthStatus.Unhealthy;
                description = "Kernel not found";
                tags.Add("not-found");
            }
        }
        else
        {
            // No runtime available - return unknown status based on cached state
            if (_kernelStates.TryGetValue(kernelId, out var state) && state.LastEntry != null)
            {
                return state.LastEntry;
            }

            status = RingKernelHealthStatus.Degraded;
            description = "Runtime not available for health checks";
            tags.Add("no-runtime");
        }

        // Record health status to instrumentation
        _instrumentation?.RecordHealthStatus(kernelId, status == RingKernelHealthStatus.Healthy, DateTimeOffset.UtcNow);

        return new RingKernelHealthEntry
        {
            KernelId = kernelId,
            Status = status,
            Description = description,
            Data = data,
            Tags = tags,
            IsActive = isActive,
            Uptime = uptime,
            LastMessageProcessed = lastMessageProcessed,
            QueueDepth = queueDepth,
            Throughput = throughput,
            AverageLatencyNanos = averageLatencyNanos,
            ErrorCode = errorCode
        };
    }

    private void UpdateHealthState(string kernelId, RingKernelHealthEntry entry)
    {
        var state = _kernelStates.GetOrAdd(kernelId, _ => new KernelHealthState());
        var previousStatus = state.LastEntry?.Status ?? RingKernelHealthStatus.Healthy;

        state.LastEntry = entry;
        state.ConsecutiveFailures = entry.Status == RingKernelHealthStatus.Unhealthy
            ? state.ConsecutiveFailures + 1
            : 0;

        if (previousStatus != entry.Status)
        {
            LogHealthChanged(_logger, kernelId, previousStatus, entry.Status, entry.Description);

            HealthChanged?.Invoke(this, new RingKernelHealthChangedEventArgs
            {
                KernelId = kernelId,
                PreviousStatus = previousStatus,
                CurrentStatus = entry.Status,
                Entry = entry
            });
        }
    }

    private static RingKernelHealthEntry CreateErrorEntry(string kernelId, Exception ex)
    {
        return new RingKernelHealthEntry
        {
            KernelId = kernelId,
            Status = RingKernelHealthStatus.Unhealthy,
            Description = $"Health check failed: {ex.Message}",
            Exception = ex,
            Tags = ["error", "exception"]
        };
    }

    private RingKernelHealthOptions GetKernelOptions(string kernelId)
    {
        if (_kernelOptions.TryGetValue(kernelId, out var options))
        {
            return options;
        }

        return _globalOptions;
    }

    private void PerformBackgroundHealthChecks(object? state)
    {
        if (_disposed)
        {
            return;
        }

        // Perform health checks in the background for all registered kernels
        foreach (var kernelId in _kernelStates.Keys)
        {
            try
            {
                // Fire and forget - results are cached
                _ = CheckKernelHealthAsync(kernelId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogHealthCheckFailed(_logger, ex, kernelId, ex.Message);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backgroundCheckTimer.Dispose();
        _kernelStates.Clear();
        _kernelOptions.Clear();
    }

    private sealed class KernelHealthState
    {
        public RingKernelHealthEntry? LastEntry { get; set; }
        public int ConsecutiveFailures { get; set; }
    }
}

/// <summary>
/// Factory for creating Ring Kernel health check instances.
/// </summary>
public sealed class RingKernelHealthCheckFactory : IRingKernelHealthCheckFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRingKernelRuntime? _runtime;
    private readonly IRingKernelInstrumentation? _instrumentation;

    /// <summary>
    /// Creates a new health check factory.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    /// <param name="runtime">Optional Ring Kernel runtime.</param>
    /// <param name="instrumentation">Optional instrumentation.</param>
    public RingKernelHealthCheckFactory(
        ILoggerFactory loggerFactory,
        IRingKernelRuntime? runtime = null,
        IRingKernelInstrumentation? instrumentation = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _runtime = runtime;
        _instrumentation = instrumentation;
    }

    /// <inheritdoc />
    public IRingKernelHealthCheck CreateHealthCheck(RingKernelHealthOptions? options = null)
    {
        var logger = _loggerFactory.CreateLogger<RingKernelHealthCheck>();
        var wrappedOptions = Options.Create(options ?? new RingKernelHealthOptions());

        return new RingKernelHealthCheck(logger, wrappedOptions, _runtime, _instrumentation);
    }
}
