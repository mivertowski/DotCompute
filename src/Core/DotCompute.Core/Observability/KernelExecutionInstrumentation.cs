// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using DotCompute.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Observability;

/// <summary>
/// OpenTelemetry-compatible instrumentation for general kernel execution.
/// Provides distributed tracing, metrics, and structured logging for GPU kernel operations.
/// </summary>
/// <remarks>
/// This instrumentation provides comprehensive observability for kernel execution including:
/// - Distributed tracing with W3C Trace Context propagation
/// - Metrics for execution counts, durations, and errors
/// - Integration with circuit breaker for failure tracking
/// - Backend-specific tagging (CUDA, Metal, OpenCL, CPU)
/// </remarks>
public sealed partial class KernelExecutionInstrumentation : IDisposable
{
    private readonly ILogger<KernelExecutionInstrumentation> _logger;
    private readonly KernelExecutionInstrumentationOptions _options;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private bool _disposed;

    // Counters
    private readonly Counter<long> _kernelExecutionsCounter;
    private readonly Counter<long> _kernelSuccessCounter;
    private readonly Counter<long> _kernelFailureCounter;
    private readonly Counter<long> _kernelTimeoutsCounter;
    private readonly Counter<long> _circuitBreakerTripsCounter;

    // Histograms
    private readonly Histogram<double> _kernelDurationHistogram;
    private readonly Histogram<double> _memoryTransferDurationHistogram;
    private readonly Histogram<long> _problemSizeHistogram;

    // Observable Gauges
    private readonly ConcurrentDictionary<string, long> _activeKernelsByBackend = new();
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitStatesByKernel = new();

    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 9800, Level = LogLevel.Debug,
        Message = "Kernel {KernelName} started on {Backend} with grid={GridSize}")]
    private static partial void LogKernelStarted(
        ILogger logger, string kernelName, string backend, string gridSize);

    [LoggerMessage(EventId = 9801, Level = LogLevel.Debug,
        Message = "Kernel {KernelName} completed on {Backend} in {DurationMs}ms")]
    private static partial void LogKernelCompleted(
        ILogger logger, string kernelName, string backend, double durationMs);

    [LoggerMessage(EventId = 9802, Level = LogLevel.Warning,
        Message = "Kernel {KernelName} failed on {Backend}: {ErrorMessage}")]
    private static partial void LogKernelFailed(
        ILogger logger, string kernelName, string backend, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 9803, Level = LogLevel.Warning,
        Message = "Kernel {KernelName} timed out on {Backend} after {TimeoutMs}ms")]
    private static partial void LogKernelTimeout(
        ILogger logger, string kernelName, string backend, double timeoutMs);

    [LoggerMessage(EventId = 9804, Level = LogLevel.Warning,
        Message = "Circuit breaker tripped for kernel {KernelName} on {Backend}: {Reason}")]
    private static partial void LogCircuitBreakerTripped(
        ILogger logger, string kernelName, string backend, string reason);

    [LoggerMessage(EventId = 9805, Level = LogLevel.Information,
        Message = "Circuit breaker recovered for kernel {KernelName} on {Backend}")]
    private static partial void LogCircuitBreakerRecovered(
        ILogger logger, string kernelName, string backend);

    #endregion

    /// <summary>
    /// Gets the ActivitySource for distributed tracing.
    /// </summary>
    public ActivitySource ActivitySource => _activitySource;

    /// <summary>
    /// Gets the Meter for metrics.
    /// </summary>
    public Meter Meter => _meter;

    /// <summary>
    /// Gets whether instrumentation is enabled.
    /// </summary>
    public bool IsEnabled => _options.Enabled && !_disposed;

    /// <summary>
    /// Creates a new kernel execution instrumentation instance.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Instrumentation configuration options.</param>
    public KernelExecutionInstrumentation(
        ILogger<KernelExecutionInstrumentation> logger,
        IOptions<KernelExecutionInstrumentationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new KernelExecutionInstrumentationOptions();

        // Create ActivitySource for distributed tracing
        _activitySource = new ActivitySource(
            _options.ServiceName,
            _options.ServiceVersion);

        // Create Meter for metrics
        _meter = new Meter(
            _options.ServiceName,
            _options.ServiceVersion);

        // Initialize counters
        _kernelExecutionsCounter = _meter.CreateCounter<long>(
            "dotcompute.kernel.executions.total",
            "executions",
            "Total number of kernel executions");

        _kernelSuccessCounter = _meter.CreateCounter<long>(
            "dotcompute.kernel.successes.total",
            "executions",
            "Total number of successful kernel executions");

        _kernelFailureCounter = _meter.CreateCounter<long>(
            "dotcompute.kernel.failures.total",
            "executions",
            "Total number of failed kernel executions");

        _kernelTimeoutsCounter = _meter.CreateCounter<long>(
            "dotcompute.kernel.timeouts.total",
            "executions",
            "Total number of kernel execution timeouts");

        _circuitBreakerTripsCounter = _meter.CreateCounter<long>(
            "dotcompute.circuit_breaker.trips.total",
            "trips",
            "Total number of circuit breaker trips");

        // Initialize histograms
        _kernelDurationHistogram = _meter.CreateHistogram<double>(
            "dotcompute.kernel.duration.milliseconds",
            "ms",
            "Kernel execution duration in milliseconds");

        _memoryTransferDurationHistogram = _meter.CreateHistogram<double>(
            "dotcompute.memory.transfer.duration.milliseconds",
            "ms",
            "Memory transfer duration in milliseconds");

        _problemSizeHistogram = _meter.CreateHistogram<long>(
            "dotcompute.kernel.problem_size.elements",
            "elements",
            "Kernel problem size in elements");

        // Initialize observable gauges
        _meter.CreateObservableGauge(
            "dotcompute.kernel.active.count",
            () => _activeKernelsByBackend.Select(kvp =>
                new Measurement<long>(kvp.Value, new KeyValuePair<string, object?>("backend", kvp.Key))),
            "kernels",
            "Number of active kernel executions by backend");

        _meter.CreateObservableGauge(
            "dotcompute.circuit_breaker.state",
            () => _circuitStatesByKernel.Select(kvp =>
                new Measurement<int>((int)kvp.Value, new KeyValuePair<string, object?>("kernel", kvp.Key))),
            "state",
            "Circuit breaker state (0=Closed, 1=Open, 2=HalfOpen)");
    }

    /// <summary>
    /// Starts a kernel execution activity for distributed tracing.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="backend">The backend type (CUDA, Metal, OpenCL, CPU).</param>
    /// <param name="gridSize">The grid/problem size.</param>
    /// <param name="blockSize">The block/workgroup size.</param>
    /// <returns>An activity representing the kernel execution, or null if tracing is disabled.</returns>
    public Activity? StartKernelExecution(
        string kernelName,
        string backend,
        int[]? gridSize = null,
        int[]? blockSize = null)
    {
        if (!IsEnabled)
            return null;

        var gridSizeStr = gridSize != null ? string.Join("x", gridSize) : "default";
        var blockSizeStr = blockSize != null ? string.Join("x", blockSize) : "default";

        var activity = _activitySource.StartActivity(
            $"kernel.execute.{kernelName}",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag(KernelSemanticConventions.KernelName, kernelName);
            activity.SetTag(KernelSemanticConventions.BackendType, backend);
            activity.SetTag(KernelSemanticConventions.GridSize, gridSizeStr);
            activity.SetTag(KernelSemanticConventions.BlockSize, blockSizeStr);
            activity.SetTag(KernelSemanticConventions.StartTimestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        // Track active kernels
        _activeKernelsByBackend.AddOrUpdate(backend, 1, (_, count) => count + 1);

        if (_options.EnableStructuredLogging)
        {
            LogKernelStarted(_logger, kernelName, backend, gridSizeStr);
        }

        // Record execution started
        var tags = CreateTags(kernelName, backend);
        _kernelExecutionsCounter.Add(1, tags);

        if (gridSize != null && gridSize.Length > 0)
        {
            var totalElements = gridSize.Aggregate(1, (a, b) => a * b);
            _problemSizeHistogram.Record(totalElements, tags);
        }

        return activity;
    }

    /// <summary>
    /// Records a successful kernel execution.
    /// </summary>
    /// <param name="activity">The activity to complete.</param>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="backend">The backend type.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="memoryTransferDuration">Optional memory transfer duration.</param>
    public void RecordKernelSuccess(
        Activity? activity,
        string kernelName,
        string backend,
        TimeSpan duration,
        TimeSpan? memoryTransferDuration = null)
    {
        if (!IsEnabled)
            return;

        var tags = CreateTags(kernelName, backend);

        // Record metrics
        _kernelSuccessCounter.Add(1, tags);
        _kernelDurationHistogram.Record(duration.TotalMilliseconds, tags);

        if (memoryTransferDuration.HasValue)
        {
            _memoryTransferDurationHistogram.Record(memoryTransferDuration.Value.TotalMilliseconds, tags);
        }

        // Update active count
        _activeKernelsByBackend.AddOrUpdate(backend, 0, (_, count) => Math.Max(0, count - 1));

        // Complete activity
        if (activity != null)
        {
            activity.SetTag(KernelSemanticConventions.Success, true);
            activity.SetTag(KernelSemanticConventions.DurationMs, duration.TotalMilliseconds);
            activity.SetStatus(ActivityStatusCode.Ok);
            activity.Dispose();
        }

        if (_options.EnableStructuredLogging)
        {
            LogKernelCompleted(_logger, kernelName, backend, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Records a failed kernel execution.
    /// </summary>
    /// <param name="activity">The activity to complete.</param>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="backend">The backend type.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="errorCategory">Optional error category.</param>
    public void RecordKernelFailure(
        Activity? activity,
        string kernelName,
        string backend,
        Exception exception,
        KernelErrorCategory? errorCategory = null)
    {
        if (!IsEnabled)
            return;

        var tags = CreateTags(kernelName, backend);
        var errorTags = tags.Concat([new KeyValuePair<string, object?>("error.category", errorCategory?.ToString() ?? "Unknown")]).ToArray();

        // Record metrics
        _kernelFailureCounter.Add(1, errorTags);

        // Update active count
        _activeKernelsByBackend.AddOrUpdate(backend, 0, (_, count) => Math.Max(0, count - 1));

        // Complete activity with error
        if (activity != null)
        {
            activity.SetTag(KernelSemanticConventions.Success, false);
            activity.SetTag(KernelSemanticConventions.ErrorType, exception.GetType().Name);
            activity.SetTag(KernelSemanticConventions.ErrorMessage, exception.Message);
            if (errorCategory.HasValue)
            {
                activity.SetTag(KernelSemanticConventions.ErrorCategory, errorCategory.Value.ToString());
            }
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.Dispose();
        }

        if (_options.EnableStructuredLogging)
        {
            LogKernelFailed(_logger, kernelName, backend, exception.Message, exception);
        }
    }

    /// <summary>
    /// Records a kernel timeout.
    /// </summary>
    /// <param name="activity">The activity to complete.</param>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="backend">The backend type.</param>
    /// <param name="timeout">The timeout duration.</param>
    public void RecordKernelTimeout(
        Activity? activity,
        string kernelName,
        string backend,
        TimeSpan timeout)
    {
        if (!IsEnabled)
            return;

        var tags = CreateTags(kernelName, backend);

        // Record metrics
        _kernelTimeoutsCounter.Add(1, tags);
        _kernelFailureCounter.Add(1, tags);

        // Update active count
        _activeKernelsByBackend.AddOrUpdate(backend, 0, (_, count) => Math.Max(0, count - 1));

        // Complete activity
        if (activity != null)
        {
            activity.SetTag(KernelSemanticConventions.Success, false);
            activity.SetTag(KernelSemanticConventions.ErrorType, "Timeout");
            activity.SetTag(KernelSemanticConventions.TimeoutMs, timeout.TotalMilliseconds);
            activity.SetStatus(ActivityStatusCode.Error, "Kernel execution timeout");
            activity.Dispose();
        }

        if (_options.EnableStructuredLogging)
        {
            LogKernelTimeout(_logger, kernelName, backend, timeout.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Records a circuit breaker state change.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="backend">The backend type.</param>
    /// <param name="newState">The new circuit state.</param>
    /// <param name="reason">The reason for the state change.</param>
    public void RecordCircuitBreakerStateChange(
        string kernelName,
        string backend,
        CircuitBreakerState newState,
        string? reason = null)
    {
        if (!IsEnabled)
            return;

        _circuitStatesByKernel[kernelName] = newState;

        if (newState == CircuitBreakerState.Open)
        {
            var tags = CreateTags(kernelName, backend);
            _circuitBreakerTripsCounter.Add(1, tags);

            if (_options.EnableStructuredLogging)
            {
                LogCircuitBreakerTripped(_logger, kernelName, backend, reason ?? "Unknown");
            }

            // Create span for circuit breaker event
            using var activity = _activitySource.StartActivity(
                "circuit_breaker.trip",
                ActivityKind.Internal);

            activity?.SetTag(KernelSemanticConventions.KernelName, kernelName);
            activity?.SetTag(KernelSemanticConventions.BackendType, backend);
            activity?.SetTag("circuit_breaker.reason", reason);
            activity?.SetTag("circuit_breaker.state", "Open");
        }
        else if (newState == CircuitBreakerState.Closed)
        {
            if (_options.EnableStructuredLogging)
            {
                LogCircuitBreakerRecovered(_logger, kernelName, backend);
            }
        }
    }

    /// <summary>
    /// Records a memory transfer operation.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="backend">The backend type.</param>
    /// <param name="direction">Transfer direction (HostToDevice, DeviceToHost, DeviceToDevice).</param>
    /// <param name="bytes">Number of bytes transferred.</param>
    /// <param name="duration">Transfer duration.</param>
    public void RecordMemoryTransfer(
        string kernelName,
        string backend,
        string direction,
        long bytes,
        TimeSpan duration)
    {
        if (!IsEnabled)
            return;

        var tags = new KeyValuePair<string, object?>[]
        {
            new("kernel.name", kernelName),
            new("backend", backend),
            new("transfer.direction", direction)
        };

        _memoryTransferDurationHistogram.Record(duration.TotalMilliseconds, tags);

        using var activity = _activitySource.StartActivity(
            $"memory.transfer.{direction}",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag(KernelSemanticConventions.KernelName, kernelName);
            activity.SetTag(KernelSemanticConventions.BackendType, backend);
            activity.SetTag("transfer.direction", direction);
            activity.SetTag("transfer.bytes", bytes);
            activity.SetTag("transfer.duration_ms", duration.TotalMilliseconds);
            activity.SetTag("transfer.bandwidth_gbps", bytes / duration.TotalSeconds / 1_000_000_000);
        }
    }

    /// <summary>
    /// Gets current instrumentation statistics.
    /// </summary>
    public KernelInstrumentationStats GetStats()
    {
        return new KernelInstrumentationStats
        {
            ActiveKernelsByBackend = new Dictionary<string, long>(_activeKernelsByBackend),
            CircuitStatesByKernel = new Dictionary<string, CircuitBreakerState>(_circuitStatesByKernel)
        };
    }

    private static KeyValuePair<string, object?>[] CreateTags(string kernelName, string backend)
    {
        return
        [
            new("kernel.name", kernelName),
            new("backend", backend)
        ];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _activitySource.Dispose();
            _meter.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for kernel execution instrumentation.
/// </summary>
public class KernelExecutionInstrumentationOptions
{
    /// <summary>
    /// Gets or sets whether instrumentation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the service name for traces and metrics.
    /// </summary>
    public string ServiceName { get; set; } = "DotCompute.KernelExecution";

    /// <summary>
    /// Gets or sets the service version.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets whether to enable structured logging.
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the sampling rate for traces (0.0 to 1.0).
    /// </summary>
    public double TraceSamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets whether to record memory transfer metrics.
    /// </summary>
    public bool RecordMemoryTransfers { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to record problem size histograms.
    /// </summary>
    public bool RecordProblemSizes { get; set; } = true;
}

/// <summary>
/// Statistics from kernel instrumentation.
/// </summary>
public class KernelInstrumentationStats
{
    /// <summary>
    /// Gets or sets active kernels by backend type.
    /// </summary>
    public Dictionary<string, long> ActiveKernelsByBackend { get; set; } = [];

    /// <summary>
    /// Gets or sets circuit breaker states by kernel name.
    /// </summary>
    public Dictionary<string, CircuitBreakerState> CircuitStatesByKernel { get; set; } = [];
}

/// <summary>
/// Semantic conventions for kernel execution telemetry.
/// </summary>
public static class KernelSemanticConventions
{
    /// <summary>
    /// Kernel name attribute.
    /// </summary>
    public const string KernelName = "kernel.name";

    /// <summary>
    /// Backend type attribute.
    /// </summary>
    public const string BackendType = "backend.type";

    /// <summary>
    /// Grid size attribute.
    /// </summary>
    public const string GridSize = "kernel.grid_size";

    /// <summary>
    /// Block size attribute.
    /// </summary>
    public const string BlockSize = "kernel.block_size";

    /// <summary>
    /// Success attribute.
    /// </summary>
    public const string Success = "operation.success";

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public const string DurationMs = "operation.duration_ms";

    /// <summary>
    /// Error type attribute.
    /// </summary>
    public const string ErrorType = "error.type";

    /// <summary>
    /// Error message attribute.
    /// </summary>
    public const string ErrorMessage = "error.message";

    /// <summary>
    /// Error category attribute.
    /// </summary>
    public const string ErrorCategory = "error.category";

    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public const string TimeoutMs = "operation.timeout_ms";

    /// <summary>
    /// Start timestamp.
    /// </summary>
    public const string StartTimestamp = "operation.start_timestamp";

    /// <summary>
    /// Device ID attribute.
    /// </summary>
    public const string DeviceId = "device.id";

    /// <summary>
    /// Device name attribute.
    /// </summary>
    public const string DeviceName = "device.name";
}
