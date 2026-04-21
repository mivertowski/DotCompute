using DotCompute.Abstractions.Logging;
using DotCompute.Abstractions.Telemetry.Context;
using DotCompute.Abstractions.Telemetry.Options;
using DotCompute.Abstractions.Telemetry.Profiles;
using DotCompute.Abstractions.Telemetry.Traces;
using DotCompute.Abstractions.Telemetry.Types;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Abstractions.Interfaces.Telemetry;

/// <summary>
/// Core telemetry service interface for DotCompute observability integration.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Records a kernel execution event with performance metrics.
    /// </summary>
    public void RecordKernelExecution(string kernelName, string deviceId, TimeSpan executionTime,

        TelemetryKernelPerformanceMetrics metrics, string? correlationId = null, Exception? exception = null);


    /// <summary>
    /// Records a memory operation with transfer metrics.
    /// </summary>
    public void RecordMemoryOperation(string operationType, string deviceId, long bytes, TimeSpan duration,
        Types.MemoryAccessMetrics metrics, string? correlationId = null, Exception? exception = null);


    /// <summary>
    /// Starts distributed tracing for cross-device operations.
    /// </summary>
    public TraceContext StartDistributedTrace(string operationName, string? correlationId = null,
        Dictionary<string, object?>? tags = null);


    /// <summary>
    /// Finishes a distributed trace and returns analysis results.
    /// </summary>
    public Task<TraceData?> FinishDistributedTraceAsync(string correlationId, TraceStatus status = TraceStatus.Ok);


    /// <summary>
    /// Creates a performance profile for detailed analysis.
    /// </summary>
    public Task<PerformanceProfile> CreatePerformanceProfileAsync(string correlationId,

        ProfileOptions? options = null, CancellationToken cancellationToken = default);


    /// <summary>
    /// Gets current system health metrics.
    /// </summary>
    public SystemHealthMetrics GetSystemHealth();


    /// <summary>
    /// Exports telemetry data to configured monitoring systems.
    /// </summary>
    public Task ExportTelemetryAsync(TelemetryExportFormat format = TelemetryExportFormat.Prometheus,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Telemetry configuration options.
/// </summary>
public sealed class TelemetryServiceOptions
{
    public bool EnableTelemetry { get; set; } = true;
    public bool EnableDistributedTracing { get; set; } = true;
    public bool EnablePerformanceProfiling { get; set; } = true;
    public bool EnableMetricsCollection { get; set; } = true;
    public bool EnableStructuredLogging { get; set; } = true;


    public TelemetryOptions TelemetryProvider { get; set; } = new();
    public DistributedTracingOptions DistributedTracing { get; set; } = new();
    public PerformanceProfilerOptions PerformanceProfiling { get; set; } = new();
    public StructuredLoggingOptions StructuredLogging { get; set; } = new();
    public LogBufferOptions LogBuffer { get; set; } = new();


    public PrometheusOptions Prometheus { get; set; } = new();
    public IList<string> ExportEndpoints { get; } = [];
}

/// <summary>
/// Prometheus metrics configuration.
/// </summary>
public sealed class PrometheusOptions
{
    public bool Enabled { get; set; } = true;
    public string MetricsPath { get; set; } = "/metrics";
    public int Port { get; set; } = 9090;
    public Dictionary<string, string> Labels { get; } = [];
}

/// <summary>
/// Service collection extensions for telemetry registration.
/// </summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds DotCompute telemetry services to the service collection.
    /// </summary>
    public static IServiceCollection AddDotComputeTelemetry(this IServiceCollection services,
        Action<TelemetryServiceOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            _ = services.Configure(configureOptions);
        }

        // Concrete telemetry, logging, and OpenTelemetry registrations live in the
        // implementation project (DotCompute.Core). Callers that want those should use
        // the Core package's AddDotComputeTelemetry extension instead of this
        // abstractions-only surface.
        return services;
    }


    /// <summary>
    /// Placeholder for the file-logging sink. The concrete FileSink lives in DotCompute.Core;
    /// this entry throws to steer callers toward the implementation project's extension.
    /// </summary>
    public static IServiceCollection AddFileLogging(this IServiceCollection services, string logFilePath)
        => throw new NotImplementedException("FileSink implementation lives in DotCompute.Core. Use the implementation project's extension method instead.");


    /// <summary>
    /// Adds custom log sink.
    /// </summary>
    public static IServiceCollection AddLogSink<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services) where T : class, ILogSink
    {
        _ = services.AddSingleton<ILogSink, T>();
        return services;
    }
}

/// <summary>
/// OpenTelemetry integration helper.
/// </summary>
public sealed class OpenTelemetryIntegration
{
    public static void Initialize()
    {
        // Placeholder for OpenTelemetry initialization
    }
}

/// <summary>
/// Log sink interface.
/// </summary>
public interface ILogSink : IDisposable
{
    public Task WriteAsync(StructuredLogEntry entry);
}

// Concrete log sink implementations (ConsoleSink, FileSink) and the default
// ITelemetryService live in DotCompute.Core; the abstractions project intentionally
// ships only the ILogSink interface and null-object defaults.
