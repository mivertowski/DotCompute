using System.Globalization;
using System.Text;
using DotCompute.Core.Logging;
using DotCompute.Core.Telemetry.Metrics;
using DotCompute.Core.Telemetry.Profiles;
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
    private readonly IMetricServer? _metricServer;
    private readonly Timer _collectionTimer;
    private readonly MetricsCollector _metricsCollector;
    private readonly CollectorRegistry _registry;
    private volatile bool _disposed;

    // Prometheus metrics (prometheus-net types)

    private Prometheus.Counter _kernelExecutionsTotal = null!;
    private Prometheus.Counter _memoryOperationsTotal = null!;
    private Prometheus.Counter _errorsTotal = null!;
    private Prometheus.Histogram _kernelExecutionDuration = null!;
    private Prometheus.Histogram _memoryTransferDuration = null!;
    private Prometheus.Gauge _currentMemoryUsage = null!;
    private Prometheus.Gauge _deviceUtilization = null!;
    private Prometheus.Gauge _deviceTemperature = null!;
    private Prometheus.Histogram _throughputOpsPerSecond = null!;
    private Prometheus.Gauge _occupancyPercentage = null!;
    private Prometheus.Gauge _cacheHitRate = null!;
    private Prometheus.Gauge _memoryBandwidth = null!;
    private Prometheus.Counter _profilesCreated = null!;
    private Prometheus.Gauge _activeProfiles = null!;
    private Prometheus.Histogram _profileDuration = null!;

    // Advanced compute metrics

    private Prometheus.Gauge _warpEfficiency = null!;
    private Prometheus.Gauge _branchDivergence = null!;
    private Prometheus.Gauge _memoryCoalescingEfficiency = null!;
    private Prometheus.Gauge _instructionThroughput = null!;
    private Prometheus.Gauge _powerConsumption = null!;
    private Prometheus.Counter _compilationEvents = null!;
    private Prometheus.Histogram _compilationDuration = null!;
    /// <summary>
    /// Initializes a new instance of the PrometheusExporter class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
    /// <param name="metricsCollector">The metrics collector.</param>

    public PrometheusExporter(ILogger<PrometheusExporter> logger, IOptions<PrometheusExporterOptions> options,
        MetricsCollector metricsCollector)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _options = options?.Value ?? new PrometheusExporterOptions();
        ArgumentNullException.ThrowIfNull(metricsCollector);

        _metricsCollector = metricsCollector;

        // Create dedicated registry for DotCompute metrics
        _registry = Prometheus.Metrics.NewCustomRegistry();

        // Initialize Prometheus metrics

        InitializeMetrics();

        // Start metric server if enabled

        if (_options.StartMetricServer)
        {
            try
            {
                _metricServer = new MetricServer(
                    hostname: _options.Hostname,
                    port: _options.Port,
                    url: _options.Endpoint,
                    registry: _registry);
                _metricServer.Start();
                _logger.LogInfoMessage($"Started Prometheus metric server on {_options.Hostname}:{_options.Port}{_options.Endpoint}");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Failed to start Prometheus metric server - port may be in use");
                _metricServer = null;
            }
        }
        else
        {
            _metricServer = null;
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
            var successLabel = success.ToString().ToUpperInvariant();

            // Core metrics
            _kernelExecutionsTotal.WithLabels(kernelName, deviceId, successLabel).Inc();
            _kernelExecutionDuration.WithLabels(kernelName, deviceId, successLabel).Observe(executionTime.TotalSeconds);

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
            _logger.LogErrorMessage(ex, $"Failed to record kernel execution metrics for {kernelName}");
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
            var successLabel = success.ToString().ToUpperInvariant();
            var sizeCategory = CategorizeMemorySize(bytes);

            _memoryOperationsTotal.WithLabels(operationType, deviceId, successLabel).Inc();
            _memoryTransferDuration.WithLabels(operationType, deviceId, sizeCategory).Observe(duration.TotalSeconds);

            if (!success)
            {
                _errorsTotal.WithLabels("memory_operation", deviceId).Inc();
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to record memory operation metrics for {operationType}");
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
            _logger.LogErrorMessage(ex, $"Failed to record device metrics for {deviceId}");
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
            _logger.LogErrorMessage(ex, $"Failed to record profile metrics for {correlationId}");
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
            var successLabel = success.ToString().ToUpperInvariant();

            _compilationEvents.WithLabels(kernelName, deviceId, successLabel).Inc();
            _compilationDuration.WithLabels(kernelName, deviceId, successLabel).Observe(compilationTime.TotalSeconds);

            if (!success)
            {
                _errorsTotal.WithLabels("kernel_compilation", deviceId).Inc();
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to record compilation metrics for {kernelName}");
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
        await _registry.CollectAndExportAsTextAsync(stream, cancellationToken).ConfigureAwait(false);
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


        _logger.LogInfoMessage("Exported Prometheus metrics to {filePath}");
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
            TotalMetricFamilies = 18, // Count of metrics defined in InitializeMetrics
            LastCollectionTime = DateTimeOffset.UtcNow,
            IsServerRunning = _metricServer != null,
            ServerEndpoint = _metricServer != null ? $"http://{_options.Hostname}:{_options.Port}{_options.Endpoint}" : null
        };
    }

    private void InitializeMetrics()
    {
        var factory = Prometheus.Metrics.WithCustomRegistry(_registry);

        // Core execution metrics
        _kernelExecutionsTotal = factory.CreateCounter(
            "dotcompute_kernel_executions_total",
            "Total number of kernel executions",
            "kernel_name", "device_id", "success");

        _memoryOperationsTotal = factory.CreateCounter(
            "dotcompute_memory_operations_total",
            "Total number of memory operations",
            "operation_type", "device_id", "success");

        _errorsTotal = factory.CreateCounter(
            "dotcompute_errors_total",
            "Total number of errors by type",
            "error_type", "device_id");

        // Timing metrics (using compute-optimized bucket ranges)
        _kernelExecutionDuration = factory.CreateHistogram(
            "dotcompute_kernel_execution_duration_seconds",
            "Kernel execution duration in seconds",
            "kernel_name", "device_id", "success");

        _memoryTransferDuration = factory.CreateHistogram(
            "dotcompute_memory_transfer_duration_seconds",
            "Memory transfer duration in seconds",
            "operation_type", "device_id", "size_category");

        // Resource utilization metrics
        _currentMemoryUsage = factory.CreateGauge(
            "dotcompute_memory_usage_bytes",
            "Current memory usage in bytes",
            "device_id");

        _deviceUtilization = factory.CreateGauge(
            "dotcompute_device_utilization_ratio",
            "Device utilization ratio (0.0 to 1.0)",
            "device_id");

        _deviceTemperature = factory.CreateGauge(
            "dotcompute_device_temperature_celsius",
            "Device temperature in Celsius",
            "device_id");

        // Performance metrics
        _throughputOpsPerSecond = factory.CreateHistogram(
            "dotcompute_kernel_throughput_ops_per_second",
            "Kernel throughput in operations per second",
            "kernel_name", "device_id");

        _occupancyPercentage = factory.CreateGauge(
            "dotcompute_kernel_occupancy_percentage",
            "Kernel occupancy percentage",
            "kernel_name", "device_id");

        _cacheHitRate = factory.CreateGauge(
            "dotcompute_cache_hit_rate",
            "Cache hit rate (0.0 to 1.0)",
            "kernel_name", "device_id");

        _memoryBandwidth = factory.CreateGauge(
            "dotcompute_memory_bandwidth_gb_per_second",
            "Memory bandwidth in GB per second",
            "kernel_name", "device_id");

        // Advanced compute metrics
        _warpEfficiency = factory.CreateGauge(
            "dotcompute_warp_efficiency",
            "Warp execution efficiency (0.0 to 1.0)",
            "kernel_name", "device_id");

        _branchDivergence = factory.CreateGauge(
            "dotcompute_branch_divergence",
            "Branch divergence ratio (0.0 to 1.0)",
            "kernel_name", "device_id");

        _memoryCoalescingEfficiency = factory.CreateGauge(
            "dotcompute_memory_coalescing_efficiency",
            "Memory coalescing efficiency (0.0 to 1.0)",
            "kernel_name", "device_id");

        _instructionThroughput = factory.CreateGauge(
            "dotcompute_instruction_throughput_per_second",
            "Instruction throughput per second",
            "kernel_name", "device_id");

        _powerConsumption = factory.CreateGauge(
            "dotcompute_power_consumption_watts",
            "Power consumption in watts",
            "component", "device_id");

        // Profiling metrics
        _profilesCreated = factory.CreateCounter(
            "dotcompute_profiles_created_total",
            "Total number of performance profiles created");

        _activeProfiles = factory.CreateGauge(
            "dotcompute_active_profiles",
            "Number of active performance profiles");

        _profileDuration = factory.CreateHistogram(
            "dotcompute_profile_duration_seconds",
            "Performance profile duration in seconds");

        // Compilation metrics
        _compilationEvents = factory.CreateCounter(
            "dotcompute_kernel_compilations_total",
            "Total number of kernel compilations",
            "kernel_name", "device_id", "success");

        _compilationDuration = factory.CreateHistogram(
            "dotcompute_kernel_compilation_duration_seconds",
            "Kernel compilation duration in seconds",
            "kernel_name", "device_id", "success");
    }

    private void CollectMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }

        // Metrics are collected on-demand through RecordXxx methods
        // This timer callback is reserved for future system-level metric collection
        // such as GC metrics, process memory, etc.
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

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
            if (_metricServer is IDisposable disposableServer)
            {
                disposableServer.Dispose();
            }
            _collectionTimer?.Dispose();
            _logger.LogInfoMessage("PrometheusExporter disposed");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during PrometheusExporter disposal");
        }
    }
}

/// <summary>
/// Configuration options for Prometheus exporter.
/// </summary>
public sealed class PrometheusExporterOptions
{
    /// <summary>
    /// Gets or sets the start metric server.
    /// </summary>
    /// <value>The start metric server.</value>
    public bool StartMetricServer { get; set; } = true;
    /// <summary>
    /// Gets or sets the hostname.
    /// </summary>
    /// <value>The hostname.</value>
    public string Hostname { get; set; } = "*";
    /// <summary>
    /// Gets or sets the port.
    /// </summary>
    /// <value>The port.</value>
    public int Port { get; set; } = 9464;
    /// <summary>
    /// Gets or sets the endpoint.
    /// </summary>
    /// <value>The endpoint.</value>
    public string Endpoint { get; set; } = "/metrics";
    /// <summary>
    /// Gets or sets the collection interval seconds.
    /// </summary>
    /// <value>The collection interval seconds.</value>
    public int CollectionIntervalSeconds { get; set; } = 15;
    /// <summary>
    /// Gets or sets the common labels.
    /// </summary>
    /// <value>The common labels.</value>
    public IList<string> CommonLabels { get; } = ["application", "version", "environment"];
}

/// <summary>
/// Statistics about the Prometheus metrics export.
/// </summary>
public sealed class PrometheusMetricsStatistics
{
    /// <summary>
    /// Gets or sets the total metric families.
    /// </summary>
    /// <value>The total metric families.</value>
    public int TotalMetricFamilies { get; set; }
    /// <summary>
    /// Gets or sets the last collection time.
    /// </summary>
    /// <value>The last collection time.</value>
    public DateTimeOffset LastCollectionTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether server running.
    /// </summary>
    /// <value>The is server running.</value>
    public bool IsServerRunning { get; set; }
    /// <summary>
    /// Gets or sets the server endpoint.
    /// </summary>
    /// <value>The server endpoint.</value>
    public string? ServerEndpoint { get; set; }
}
