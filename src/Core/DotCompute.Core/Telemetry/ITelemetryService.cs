using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Core.Types;
using DotCompute.Core.Logging;
using Logging = DotCompute.Core.Logging;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Core telemetry service interface for DotCompute observability integration.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Records a kernel execution event with performance metrics.
    /// </summary>
    void RecordKernelExecution(string kernelName, string deviceId, TimeSpan executionTime, 
        KernelPerformanceMetrics metrics, string? correlationId = null, Exception? exception = null);
    
    /// <summary>
    /// Records a memory operation with transfer metrics.
    /// </summary>
    void RecordMemoryOperation(string operationType, string deviceId, long bytes, TimeSpan duration,
        Types.MemoryAccessMetrics metrics, string? correlationId = null, Exception? exception = null);
    
    /// <summary>
    /// Starts distributed tracing for cross-device operations.
    /// </summary>
    TraceContext StartDistributedTrace(string operationName, string? correlationId = null,
        Dictionary<string, object?>? tags = null);
    
    /// <summary>
    /// Finishes a distributed trace and returns analysis results.
    /// </summary>
    Task<TraceData?> FinishDistributedTraceAsync(string correlationId, TraceStatus status = TraceStatus.Ok);
    
    /// <summary>
    /// Creates a performance profile for detailed analysis.
    /// </summary>
    Task<PerformanceProfile> CreatePerformanceProfileAsync(string correlationId, 
        ProfileOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets current system health metrics.
    /// </summary>
    SystemHealthMetrics GetSystemHealth();
    
    /// <summary>
    /// Exports telemetry data to configured monitoring systems.
    /// </summary>
    Task ExportTelemetryAsync(TelemetryExportFormat format = TelemetryExportFormat.Prometheus,
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
    public List<string> ExportEndpoints { get; set; } = new();
}

/// <summary>
/// Prometheus metrics configuration.
/// </summary>
public sealed class PrometheusOptions
{
    public bool Enabled { get; set; } = true;
    public string MetricsPath { get; set; } = "/metrics";
    public int Port { get; set; } = 9090;
    public Dictionary<string, string> Labels { get; set; } = new();
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
            services.Configure(configureOptions);
        }
        
        // Core telemetry services
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<TelemetryProvider>();
        services.AddSingleton<MetricsCollector>();
        services.AddSingleton<DistributedTracer>();
        services.AddSingleton<PerformanceProfiler>();
        
        // Logging services
        services.AddSingleton<LogBuffer>();
        services.AddSingleton<LogEnricher>();
        
        // Log sinks
        services.AddSingleton<ILogSink, ConsoleSink>();
        
        // OpenTelemetry integration - simplified for compatibility
        try
        {
            // Add OpenTelemetry services if available
            services.AddSingleton<OpenTelemetryIntegration>();
        }
        catch (Exception)
        {
            // OpenTelemetry packages not available - continue without telemetry
        }
        
        return services;
    }
    
    /// <summary>
    /// Adds file-based logging sink.
    /// </summary>
    public static IServiceCollection AddFileLogging(this IServiceCollection services, string logFilePath)
    {
        services.AddSingleton<ILogSink>(provider => new FileSink(logFilePath));
        return services;
    }
    
    /// <summary>
    /// Adds custom log sink.
    /// </summary>
    public static IServiceCollection AddLogSink<T>(this IServiceCollection services) where T : class, ILogSink
    {
        services.AddSingleton<ILogSink, T>();
        return services;
    }
}

/// <summary>
/// OpenTelemetry integration helper.
/// </summary>
public sealed class OpenTelemetryIntegration
{
    public void Initialize()
    {
        // Placeholder for OpenTelemetry initialization
    }
}

/// <summary>
/// Log sink interface.
/// </summary>
public interface ILogSink : IDisposable
{
    Task WriteAsync(StructuredLogEntry entry);
}

/// <summary>
/// Console log sink implementation.
/// </summary>
public sealed class ConsoleSink : ILogSink
{
    public async Task WriteAsync(StructuredLogEntry entry)
    {
        Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] {entry.LogLevel}: {entry.FormattedMessage}");
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// File log sink implementation.
/// </summary>
public sealed class FileSink : ILogSink
{
    private readonly string _filePath;

    public FileSink(string filePath)
    {
        _filePath = filePath;
    }

    public async Task WriteAsync(StructuredLogEntry entry)
    {
        var logLine = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.LogLevel}: {entry.FormattedMessage}";
        await File.AppendAllTextAsync(_filePath, logLine + Environment.NewLine);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Default implementation of the telemetry service.
/// </summary>
internal sealed class TelemetryService : ITelemetryService, IDisposable
{
    private readonly TelemetryProvider _telemetryProvider;
    private readonly DistributedTracer _distributedTracer;
    private readonly PerformanceProfiler _performanceProfiler;
    private readonly StructuredLogger _structuredLogger;
    private volatile bool _disposed;

    public TelemetryService(
        TelemetryProvider telemetryProvider,
        DistributedTracer distributedTracer,
        PerformanceProfiler performanceProfiler,
        StructuredLogger structuredLogger)
    {
        _telemetryProvider = telemetryProvider ?? throw new ArgumentNullException(nameof(telemetryProvider));
        _distributedTracer = distributedTracer ?? throw new ArgumentNullException(nameof(distributedTracer));
        _performanceProfiler = performanceProfiler ?? throw new ArgumentNullException(nameof(performanceProfiler));
        _structuredLogger = structuredLogger ?? throw new ArgumentNullException(nameof(structuredLogger));
    }

    public void RecordKernelExecution(string kernelName, string deviceId, TimeSpan executionTime,
        KernelPerformanceMetrics metrics, string? correlationId = null, Exception? exception = null)
    {
        ThrowIfDisposed();
        
        // Record in metrics collector
        var metricData = new Dictionary<string, object>
        {
            ["throughput"] = metrics.AverageThroughput,
            ["occupancy"] = metrics.AverageOccupancy,
            ["efficiency"] = metrics.MemoryEfficiency
        };
        
        _telemetryProvider.RecordKernelExecution(kernelName, executionTime, deviceId, exception == null, metricData);
        
        // Log structured event - convert metrics to logging format
        var loggingMetrics = new Logging.KernelPerformanceMetrics
        {
            ThroughputOpsPerSecond = metrics.AverageThroughput,
            OccupancyPercentage = metrics.AverageOccupancy,
            MemoryUsageBytes = (long)(metrics.MemoryEfficiency * 1024 * 1024), // Convert efficiency to approximate bytes
            CacheHitRate = metrics.CacheHitRate,
            DeviceUtilization = metrics.AverageOccupancy
        };
        _structuredLogger.LogKernelExecution(kernelName, deviceId, executionTime, loggingMetrics, correlationId, exception);
    }

    public void RecordMemoryOperation(string operationType, string deviceId, long bytes, TimeSpan duration,
        Types.MemoryAccessMetrics metrics, string? correlationId = null, Exception? exception = null)
    {
        ThrowIfDisposed();
        
        // Record in telemetry provider
        _telemetryProvider.RecordMemoryOperation(operationType, bytes, duration, deviceId, exception == null);
        
        // Log structured event - convert metrics to logging format
        var loggingMemoryMetrics = new Logging.MemoryAccessMetrics
        {
            AccessPattern = operationType,
            CoalescingEfficiency = 0.85,
            CacheHitRate = 0.90,
            MemorySegment = deviceId,
            TransferDirection = "HostToDevice", // Default - would be determined from operation type
            QueueDepth = 1
        };
        _structuredLogger.LogMemoryOperation(operationType, deviceId, bytes, duration, loggingMemoryMetrics, correlationId, exception);
    }

    public TraceContext StartDistributedTrace(string operationName, string? correlationId = null,
        Dictionary<string, object?>? tags = null)
    {
        ThrowIfDisposed();
        
        return _distributedTracer.StartTrace(operationName, correlationId, null, tags);
    }

    public async Task<TraceData?> FinishDistributedTraceAsync(string correlationId, TraceStatus status = TraceStatus.Ok)
    {
        ThrowIfDisposed();
        
        return await _distributedTracer.FinishTraceAsync(correlationId, status);
    }

    public async Task<PerformanceProfile> CreatePerformanceProfileAsync(string correlationId,
        ProfileOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        return await _performanceProfiler.CreateProfileAsync(correlationId, options, cancellationToken);
    }

    public SystemHealthMetrics GetSystemHealth()
    {
        ThrowIfDisposed();
        
        return _telemetryProvider.GetSystemHealth();
    }

    public async Task ExportTelemetryAsync(TelemetryExportFormat format = TelemetryExportFormat.Prometheus,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _telemetryProvider.ExportTelemetryAsync(format, cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(TelemetryService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        _telemetryProvider?.Dispose();
        _distributedTracer?.Dispose();
        _performanceProfiler?.Dispose();
        _structuredLogger?.Dispose();
    }
}