using Microsoft.Extensions.DependencyInjection;
using DotCompute.Abstractions.Logging;
using DotCompute.Abstractions.Telemetry.Options;
using DotCompute.Abstractions.Telemetry.Types;
using DotCompute.Abstractions.Telemetry.Context;
using DotCompute.Abstractions.Telemetry.Traces;
using DotCompute.Abstractions.Telemetry.Profiles;

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

        // Core telemetry services - TODO: Move implementations to DotCompute.Core
        // Note: These concrete types should be registered in the implementation project
        // _ = services.AddSingleton<ITelemetryService, TelemetryService>();
        // _ = services.AddSingleton<TelemetryProvider>();
        // _ = services.AddSingleton<MetricsCollector>();
        // _ = services.AddSingleton<DistributedTracer>();
        // _ = services.AddSingleton<PerformanceProfiler>();

        // Logging services - TODO: Move implementations to DotCompute.Core
        // _ = services.AddSingleton<LogBuffer>();
        // _ = services.AddSingleton<LogEnricher>();

        // Log sinks - Keep interfaces only, implementations in core
        // _ = services.AddSingleton<ILogSink, ConsoleSink>();

        // OpenTelemetry integration - simplified for compatibility
        // TODO: Move to implementation project
        // try
        // {
        //     // Add OpenTelemetry services if available
        //     _ = services.AddSingleton<OpenTelemetryIntegration>();
        // }
        // catch (Exception)
        // {
        //     // OpenTelemetry packages not available - continue without telemetry
        // }


        return services;
    }


    /// <summary>
    /// Adds file-based logging sink.
    /// TODO: FileSink implementation should be in DotCompute.Core
    /// </summary>
    public static IServiceCollection AddFileLogging(this IServiceCollection services, string logFilePath)
        // TODO: Replace with actual FileSink implementation from DotCompute.Core
        => throw new NotImplementedException("FileSink implementation moved to DotCompute.Core. Use implementation project's extension method instead.");


    /// <summary>
    /// Adds custom log sink.
    /// </summary>
    public static IServiceCollection AddLogSink<T>(this IServiceCollection services) where T : class, ILogSink
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

// TODO: Concrete log sink implementations should be moved to DotCompute.Core.
// Keeping only the ILogSink interface in abstractions.
// public sealed class ConsoleSink : ILogSink - MOVED TO IMPLEMENTATION PROJECT
// public sealed class FileSink : ILogSink - MOVED TO IMPLEMENTATION PROJECT

// TODO: Default implementation of the telemetry service should be moved to DotCompute.Core.
// This concrete implementation is temporarily removed from abstractions project.
// internal sealed class TelemetryService : ITelemetryService, IDisposable - MOVED TO IMPLEMENTATION PROJECT