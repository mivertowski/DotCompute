using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Production-grade Prometheus metrics exporter with real-time dashboard support and custom metric definitions.
/// Provides comprehensive GPU compute metrics in Prometheus format for monitoring and alerting.
/// </summary>
public sealed class PrometheusExporter : IDisposable
{
    private readonly ILogger<PrometheusExporter> _logger;
    private readonly PrometheusExporterOptions _options;
    private readonly MetricServer? _metricServer;
    private readonly Timer _collectionTimer;
    private readonly MetricsCollector _metricsCollector;
    private volatile bool _disposed;

    // Prometheus metrics

    private Counter _kernelExecutionsTotal = null!;
    private Counter _memoryOperationsTotal = null!;
    private Counter _errorsTotal = null!;
    private Histogram _kernelExecutionDuration = null!;
    private Histogram _memoryTransferDuration = null!;
    private Gauge _currentMemoryUsage = null!;
    private Gauge _deviceUtilization = null!;
    private Gauge _deviceTemperature = null!;
    private Histogram _throughputOpsPerSecond = null!;
    private Gauge _occupancyPercentage = null!;
    private Gauge _cacheHitRate = null!;
    private Gauge _memoryBandwidth = null!;
    private Counter _profilesCreated = null!;
    private Gauge _activeProfiles = null!;
    private Histogram _profileDuration = null!;

    // Advanced compute metrics

    private Gauge _warpEfficiency = null!;
    private Gauge _branchDivergence = null!;
    private Gauge _memoryCoalescingEfficiency = null!;
    private Gauge _instructionThroughput = null!;
    private Gauge _powerConsumption = null!;
    private Counter _compilationEvents = null!;
    private Histogram _compilationDuration = null!;

    public PrometheusExporter(ILogger<PrometheusExporter> logger, IOptions<PrometheusExporterOptions> options,
        MetricsCollector metricsCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new PrometheusExporterOptions();
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

        // Initialize Prometheus metrics

        InitializeMetrics();

        // Start metric server if enabled

        if (_options.StartMetricServer)
        {
            _metricServer = new MetricServer(hostname: _options.Hostname, port: _options.Port, url: _options.Endpoint);
            _ = _metricServer.Start();
            _logger.LogInformation("Started Prometheus metric server on {Hostname}:{Port}{Endpoint}",

                _options.Hostname, _options.Port, _options.Endpoint);
        }

        // Start collection timer

        _collectionTimer = new Timer(CollectMetrics, null,
            TimeSpan.Zero, TimeSpan.FromSeconds(_options.CollectionIntervalSeconds));
    }

    /// <summary>
    /// Records kernel execution metrics in Prometheus format.
    /// </summary>
    /// <param name="kernelName">Name of the executed kernel</param>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="executionTime">Kernel execution duration</param>
    /// <param name="metrics">Detailed execution metrics</param>
    /// <param name="success">Whether the execution was successful</param>
    public void RecordKernelExecution(string kernelName, string deviceId, TimeSpan executionTime,
        KernelExecutionMetrics metrics, bool success)
    {
        ThrowIfDisposed();


        try
        {
            var labels = new[] { kernelName, deviceId, success.ToString().ToLowerInvariant() };

            // Core metrics

            _kernelExecutionsTotal.WithLabels(labels).Inc();
            _kernelExecutionDuration.WithLabels(labels).Observe(executionTime.TotalSeconds);

            // Performance metrics

            _throughputOpsPerSecond.WithLabels(kernelName, deviceId).Observe(metrics.ThroughputOpsPerSecond);
            _occupancyPercentage.WithLabels(kernelName, deviceId).Set(metrics.OccupancyPercentage);
            _cacheHitRate.WithLabels(kernelName, deviceId).Set(metrics.CacheHitRate);
            _memoryBandwidth.WithLabels(kernelName, deviceId).Set(metrics.MemoryBandwidthGBPerSecond);

            // Advanced compute metrics

            _warpEfficiency.WithLabels(kernelName, deviceId).Set(metrics.WarpEfficiency);
            _branchDivergence.WithLabels(kernelName, deviceId).Set(metrics.BranchDivergence);
            _memoryCoalescingEfficiency.WithLabels(kernelName, deviceId).Set(metrics.MemoryCoalescingEfficiency);
            _instructionThroughput.WithLabels(kernelName, deviceId).Set(metrics.InstructionThroughput);
            _powerConsumption.WithLabels(kernelName, deviceId).Set(metrics.PowerConsumption);


            if (!success)
            {
                _errorsTotal.WithLabels("kernel_execution", deviceId).Inc();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record kernel execution metrics for {KernelName}", kernelName);
        }
    }

    /// <summary>
    /// Records memory operation metrics in Prometheus format.
    /// </summary>
    /// <param name="operationType">Type of memory operation</param>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="bytes">Number of bytes transferred</param>
    /// <param name="duration">Transfer duration</param>
    /// <param name="metrics">Detailed memory metrics</param>
    /// <param name="success">Whether the operation was successful</param>
    public void RecordMemoryOperation(string operationType, string deviceId, long bytes, TimeSpan duration,
        MemoryOperationMetrics metrics, bool success)
    {
        ThrowIfDisposed();


        try
        {
            var labels = new[] { operationType, deviceId, success.ToString().ToLowerInvariant() };
            var sizeCategory = CategorizeMemorySize(bytes);


            _memoryOperationsTotal.WithLabels(labels).Inc();
            _memoryTransferDuration.WithLabels(operationType, deviceId, sizeCategory).Observe(duration.TotalSeconds);


            if (!success)
            {
                _errorsTotal.WithLabels("memory_operation", deviceId).Inc();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record memory operation metrics for {OperationType}", operationType);
        }
    }

    /// <summary>
    /// Records device health and utilization metrics.
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="metrics">Device performance metrics</param>
    public void RecordDeviceMetrics(string deviceId, DevicePerformanceMetrics metrics)
    {
        ThrowIfDisposed();


        try
        {
            _deviceUtilization.WithLabels(deviceId).Set(metrics.UtilizationPercentage);
            _deviceTemperature.WithLabels(deviceId).Set(metrics.TemperatureCelsius);
            _currentMemoryUsage.WithLabels(deviceId).Set(metrics.CurrentMemoryUsage);
            _powerConsumption.WithLabels("device", deviceId).Set(metrics.PowerConsumptionWatts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record device metrics for {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// Records performance profiling metrics.
    /// </summary>
    /// <param name="correlationId">Profile correlation ID</param>
    /// <param name="profile">Performance profile data</param>
    public void RecordProfileMetrics(string correlationId, PerformanceProfile profile)
    {
        ThrowIfDisposed();


        try
        {
            _profilesCreated.Inc();


            if (profile.EndTime.HasValue)
            {
                _profileDuration.Observe(profile.TotalDuration.TotalSeconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record profile metrics for {CorrelationId}", correlationId);
        }
    }

    /// <summary>
    /// Records kernel compilation events.
    /// </summary>
    /// <param name="kernelName">Name of the compiled kernel</param>
    /// <param name="deviceId">Target device</param>
    /// <param name="compilationTime">Compilation duration</param>
    /// <param name="success">Whether compilation was successful</param>
    public void RecordKernelCompilation(string kernelName, string deviceId, TimeSpan compilationTime, bool success)
    {
        ThrowIfDisposed();


        try
        {
            var labels = new[] { kernelName, deviceId, success.ToString().ToLowerInvariant() };


            _compilationEvents.WithLabels(labels).Inc();
            _compilationDuration.WithLabels(labels).Observe(compilationTime.TotalSeconds);


            if (!success)
            {
                _errorsTotal.WithLabels("kernel_compilation", deviceId).Inc();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record compilation metrics for {KernelName}", kernelName);
        }
    }

    /// <summary>
    /// Exports current metrics in Prometheus text format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prometheus-formatted metrics string</returns>
    public async Task<string> ExportMetricsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream, cancellationToken);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Exports metrics to a file in Prometheus format.
    /// </summary>
    /// <param name="filePath">Path to export file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ExportMetricsToFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        var metricsText = await ExportMetricsAsync(cancellationToken);
        await File.WriteAllTextAsync(filePath, metricsText, cancellationToken);


        _logger.LogInformation("Exported Prometheus metrics to {FilePath}", filePath);
    }

    /// <summary>
    /// Gets current metric statistics for monitoring.
    /// </summary>
    /// <returns>Metrics statistics</returns>
    public PrometheusMetricsStatistics GetMetricsStatistics()
    {
        ThrowIfDisposed();


        return new PrometheusMetricsStatistics
        {
            TotalMetricFamilies = 0, // Registry doesn't expose count directly
            LastCollectionTime = DateTimeOffset.UtcNow,
            IsServerRunning = _metricServer != null,
            ServerEndpoint = _metricServer != null ? $"http://{_options.Hostname}:{_options.Port}{_options.Endpoint}" : null
        };
    }

    private void InitializeMetrics()
    {
        var commonLabels = _options.CommonLabels.ToArray();

        // Core execution metrics

        _kernelExecutionsTotal = Metrics.CreateCounter(
            "dotcompute_kernel_executions_total",
            "Total number of kernel executions",
            ["kernel_name", "device_id", "success", .. commonLabels]);


        _memoryOperationsTotal = Metrics.CreateCounter(
            "dotcompute_memory_operations_total",
            "Total number of memory operations",
            ["operation_type", "device_id", "success", .. commonLabels]);


        _errorsTotal = Metrics.CreateCounter(
            "dotcompute_errors_total",
            "Total number of errors by type",
            ["error_type", "device_id", .. commonLabels]);

        // Timing metrics

        _kernelExecutionDuration = Metrics.CreateHistogram(
            "dotcompute_kernel_execution_duration_seconds",
            "Kernel execution duration in seconds",
            ["kernel_name", "device_id", "success", .. commonLabels]);


        _memoryTransferDuration = Metrics.CreateHistogram(
            "dotcompute_memory_transfer_duration_seconds",
            "Memory transfer duration in seconds",
            ["operation_type", "device_id", "size_category", .. commonLabels]);

        // Resource utilization metrics

        _currentMemoryUsage = Metrics.CreateGauge(
            "dotcompute_memory_usage_bytes",
            "Current memory usage in bytes",
            ["device_id", .. commonLabels]);


        _deviceUtilization = Metrics.CreateGauge(
            "dotcompute_device_utilization_ratio",
            "Device utilization ratio (0.0 to 1.0)",
            ["device_id", .. commonLabels]);


        _deviceTemperature = Metrics.CreateGauge(
            "dotcompute_device_temperature_celsius",
            "Device temperature in Celsius",
            ["device_id", .. commonLabels]);

        // Performance metrics

        _throughputOpsPerSecond = Metrics.CreateHistogram(
            "dotcompute_kernel_throughput_ops_per_second",
            "Kernel throughput in operations per second",
            ["kernel_name", "device_id", .. commonLabels]);


        _occupancyPercentage = Metrics.CreateGauge(
            "dotcompute_kernel_occupancy_percentage",
            "Kernel occupancy percentage",
            ["kernel_name", "device_id", .. commonLabels]);


        _cacheHitRate = Metrics.CreateGauge(
            "dotcompute_cache_hit_rate",
            "Cache hit rate (0.0 to 1.0)",
            ["kernel_name", "device_id", .. commonLabels]);


        _memoryBandwidth = Metrics.CreateGauge(
            "dotcompute_memory_bandwidth_gb_per_second",
            "Memory bandwidth in GB per second",
            ["kernel_name", "device_id", .. commonLabels]);

        // Advanced compute metrics

        _warpEfficiency = Metrics.CreateGauge(
            "dotcompute_warp_efficiency",
            "Warp execution efficiency (0.0 to 1.0)",
            ["kernel_name", "device_id", .. commonLabels]);


        _branchDivergence = Metrics.CreateGauge(
            "dotcompute_branch_divergence",
            "Branch divergence ratio (0.0 to 1.0)",
            ["kernel_name", "device_id", .. commonLabels]);


        _memoryCoalescingEfficiency = Metrics.CreateGauge(
            "dotcompute_memory_coalescing_efficiency",
            "Memory coalescing efficiency (0.0 to 1.0)",
            ["kernel_name", "device_id", .. commonLabels]);


        _instructionThroughput = Metrics.CreateGauge(
            "dotcompute_instruction_throughput_per_second",
            "Instruction throughput per second",
            ["kernel_name", "device_id", .. commonLabels]);


        _powerConsumption = Metrics.CreateGauge(
            "dotcompute_power_consumption_watts",
            "Power consumption in watts",
            ["component", "device_id", .. commonLabels]);

        // Profiling metrics

        _profilesCreated = Metrics.CreateCounter(
            "dotcompute_profiles_created_total",
            "Total number of performance profiles created",
            commonLabels);


        _activeProfiles = Metrics.CreateGauge(
            "dotcompute_active_profiles",
            "Number of active performance profiles",
            commonLabels);


        _profileDuration = Metrics.CreateHistogram(
            "dotcompute_profile_duration_seconds",
            "Performance profile duration in seconds",
            commonLabels);

        // Compilation metrics

        _compilationEvents = Metrics.CreateCounter(
            "dotcompute_kernel_compilations_total",
            "Total number of kernel compilations",
            ["kernel_name", "device_id", "success", .. commonLabels]);


        _compilationDuration = Metrics.CreateHistogram(
            "dotcompute_kernel_compilation_duration_seconds",
            "Kernel compilation duration in seconds",
            ["kernel_name", "device_id", "success", .. commonLabels]);
    }

    private void CollectMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Update active profiles gauge
            // TODO: Implement system health collection when MetricsCollector is available
            _activeProfiles.Set(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect metrics during timer update");
        }
    }

    private static string CategorizeMemorySize(long bytes)
    {
        return bytes switch
        {
            < 1024 => "tiny",           // < 1KB
            < 1024 * 1024 => "small",  // < 1MB
            < 1024 * 1024 * 1024 => "medium", // < 1GB
            _ => "large"                // >= 1GB
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(PrometheusExporter));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;


        try
        {
            _metricServer?.Stop();
            _collectionTimer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PrometheusExporter disposal");
        }
    }
}

/// <summary>
/// Configuration options for Prometheus exporter.
/// </summary>
public sealed class PrometheusExporterOptions
{
    public bool StartMetricServer { get; set; } = true;
    public string Hostname { get; set; } = "*";
    public int Port { get; set; } = 9464;
    public string Endpoint { get; set; } = "/metrics";
    public int CollectionIntervalSeconds { get; set; } = 15;
    public List<string> CommonLabels { get; set; } = new() { "application", "version", "environment" };
}

/// <summary>
/// Statistics about the Prometheus metrics export.
/// </summary>
public sealed class PrometheusMetricsStatistics
{
    public int TotalMetricFamilies { get; set; }
    public DateTimeOffset LastCollectionTime { get; set; }
    public bool IsServerRunning { get; set; }
    public string? ServerEndpoint { get; set; }
}