using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DotCompute.Core.Types;

namespace DotCompute.Core.Logging;

/// <summary>
/// Production-grade structured logger with semantic properties, correlation IDs, and performance metrics.
/// Provides async, buffered logging with configurable sinks and minimal performance impact.
/// </summary>
public sealed partial class StructuredLogger : ILogger, IDisposable
{
    private readonly string _categoryName;
    private readonly ILogger _baseLogger;
    private readonly StructuredLoggingOptions _options;
    private readonly LogBuffer _logBuffer;
    private readonly LogEnricher _logEnricher;
    private readonly ConcurrentDictionary<string, object> _globalContext;
    private readonly Timer _flushTimer;
    private volatile bool _disposed;

    #region LoggerMessage Delegates - Event ID range 25100-25104

    private static readonly Action<ILogger, Exception?> _logStructuredEntryFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(25100, nameof(LogStructuredEntryFailed)),
            "Failed to process structured log entry");

    private static void LogStructuredEntryFailed(ILogger logger, Exception ex) => _logStructuredEntryFailed(logger, ex);

    private static readonly Action<ILogger, string, string, double, Exception?> _logKernelFailed =
        LoggerMessage.Define<string, string, double>(
            LogLevel.Error,
            new EventId(25101, nameof(LogKernelFailed)),
            "Kernel execution failed: {KernelName} on {DeviceId} after {ExecutionTime}ms");

    private static void LogKernelFailed(ILogger logger, string kernelName, string deviceId, double executionTime, Exception ex) => _logKernelFailed(logger, kernelName, deviceId, executionTime, ex);

    private static readonly Action<ILogger, Exception?> _logFlushFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(25102, nameof(LogFlushFailed)),
            "Failed to flush log buffer during timer flush");

    private static void LogFlushFailed(ILogger logger, Exception ex) => _logFlushFailed(logger, ex);

    private static readonly Action<ILogger, Exception?> _logFlushTaskFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(25103, nameof(LogFlushTaskFailed)),
            "Failed to start log flush task");

    private static void LogFlushTaskFailed(ILogger logger, Exception ex) => _logFlushTaskFailed(logger, ex);

    private static readonly Action<ILogger, Exception?> _logFinalFlushFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(25104, nameof(LogFinalFlushFailed)),
            "Failed to perform final flush during dispose");

    private static void LogFinalFlushFailed(ILogger logger, Exception ex) => _logFinalFlushFailed(logger, ex);

    #endregion
    /// <summary>
    /// Initializes a new instance of the StructuredLogger class.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <param name="baseLogger">The base logger.</param>
    /// <param name="options">The options.</param>
    /// <param name="logBuffer">The log buffer.</param>
    /// <param name="logEnricher">The log enricher.</param>

    public StructuredLogger(string categoryName, ILogger baseLogger, IOptions<StructuredLoggingOptions> options,
        LogBuffer logBuffer, LogEnricher logEnricher)
    {
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
        _baseLogger = baseLogger ?? throw new ArgumentNullException(nameof(baseLogger));
        _options = options?.Value ?? new StructuredLoggingOptions();
        _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));
        _logEnricher = logEnricher ?? throw new ArgumentNullException(nameof(logEnricher));


        _globalContext = new ConcurrentDictionary<string, object>();

        // Initialize global context

        _globalContext["MachineName"] = Environment.MachineName;
        _globalContext["ProcessId"] = Environment.ProcessId;
        _globalContext["Application"] = "DotCompute";
        _globalContext["Version"] = typeof(StructuredLogger).Assembly.GetName().Version?.ToString() ?? "Unknown";

        // Start periodic flush timer

        _flushTimer = new Timer(FlushLogs, null,

            TimeSpan.FromSeconds(_options.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_options.FlushIntervalSeconds));
    }
    /// <summary>
    /// Gets begin scope.
    /// </summary>
    /// <typeparam name="TState">The TState type parameter.</typeparam>
    /// <param name="state">The state.</param>
    /// <returns>The result of the operation.</returns>

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _baseLogger.BeginScope(state);
    /// <summary>
    /// Determines whether enabled.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

    public bool IsEnabled(LogLevel logLevel) => _baseLogger.IsEnabled(logLevel);

    /// <summary>
    /// Logs a structured message with performance metrics and correlation context.
    /// </summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,

        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || _disposed)
        {
            return;
        }


        try
        {
            // Create structured log entry
            var logEntry = CreateStructuredLogEntry(logLevel, eventId, state, exception, formatter);

            // Enrich with contextual data

            _logEnricher.EnrichLogEntry(logEntry);

            // Add to buffer for async processing

            _ = _logBuffer.AddLogEntry(logEntry);

            // Also log through base logger for immediate visibility if needed

            if (_options.EnableSynchronousLogging || logLevel >= LogLevel.Error)
            {
                _baseLogger.Log(logLevel, eventId, logEntry, exception, (entry, ex) => entry.FormattedMessage);
            }
        }
        catch (Exception ex)
        {
            // Never throw from logging - log the error through base logger
            LogStructuredEntryFailed(_baseLogger, ex);
        }
    }

    /// <summary>
    /// Logs kernel execution with detailed performance metrics.
    /// </summary>
    public void LogKernelExecution(string kernelName, string deviceId, TimeSpan executionTime,
        KernelPerformanceMetrics metrics, string? correlationId = null, Exception? exception = null)
    {
        ThrowIfDisposed();


        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            LogLevel = exception != null ? LogLevel.Error : LogLevel.Information,
            Category = _categoryName,
            Message = "Kernel execution completed",
            FormattedMessage = $"Kernel '{kernelName}' executed on device '{deviceId}' in {executionTime.TotalMilliseconds:F2}ms",
            Exception = exception,
            EventId = new EventId(1001, "KernelExecution")
        };

        logEntry.Properties["KernelName"] = kernelName;
        logEntry.Properties["DeviceId"] = deviceId;
        logEntry.Properties["ExecutionTimeMs"] = executionTime.TotalMilliseconds;
        logEntry.Properties["ThroughputOpsPerSec"] = metrics.ThroughputOpsPerSecond;
        logEntry.Properties["OccupancyPercentage"] = metrics.OccupancyPercentage;
        logEntry.Properties["MemoryBandwidthGBPerSec"] = metrics.MemoryBandwidthGBPerSecond;
        logEntry.Properties["CacheHitRate"] = metrics.CacheHitRate;
        logEntry.Properties["InstructionThroughput"] = metrics.InstructionThroughput;
        logEntry.Properties["WarpEfficiency"] = metrics.WarpEfficiency;
        logEntry.Properties["BranchDivergence"] = metrics.BranchDivergence;
        logEntry.Properties["PowerConsumption"] = metrics.PowerConsumption;
        logEntry.Properties["Success"] = exception == null;

        logEntry.PerformanceMetrics = new LogPerformanceMetrics
        {
            ExecutionTimeMs = executionTime.TotalMilliseconds,
            ThroughputOpsPerSecond = metrics.ThroughputOpsPerSecond,
            MemoryUsageBytes = metrics.MemoryUsageBytes,
            CacheHitRatio = metrics.CacheHitRate,
            DeviceUtilizationPercentage = metrics.DeviceUtilization
        };


        if (!string.IsNullOrEmpty(correlationId))
        {
            logEntry.CorrelationId = correlationId;
            logEntry.Properties["CorrelationId"] = correlationId;
        }

        // Add trace context if available

        var activity = Activity.Current;
        if (activity != null)
        {
            logEntry.TraceId = activity.TraceId.ToString();
            logEntry.SpanId = activity.SpanId.ToString();
            logEntry.Properties["TraceId"] = logEntry.TraceId;
            logEntry.Properties["SpanId"] = logEntry.SpanId;
        }


        _logEnricher.EnrichLogEntry(logEntry);
        _ = _logBuffer.AddLogEntry(logEntry);

        // Log critical kernel failures immediately

        if (exception != null)
        {
            LogKernelFailed(_baseLogger, kernelName, deviceId, executionTime.TotalMilliseconds, exception);
        }
    }

    /// <summary>
    /// Logs memory operations with transfer metrics and access patterns.
    /// </summary>
    public void LogMemoryOperation(string operationType, string deviceId, long bytes, TimeSpan duration,
        MemoryAccessMetrics metrics, string? correlationId = null, Exception? exception = null)
    {
        ThrowIfDisposed();


        var bandwidthGBPerSec = bytes / (1024.0 * 1024 * 1024) / duration.TotalSeconds;


        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            LogLevel = exception != null ? LogLevel.Error : LogLevel.Debug,
            Category = _categoryName,
            Message = "Memory operation completed",
            FormattedMessage = $"Memory operation '{operationType}' on device '{deviceId}': " +
                              $"{bytes:N0} bytes in {duration.TotalMilliseconds:F2}ms ({bandwidthGBPerSec:F2} GB/s)",
            Exception = exception,
            EventId = new EventId(1002, "MemoryOperation")
        };

        logEntry.Properties["OperationType"] = operationType;
        logEntry.Properties["DeviceId"] = deviceId;
        logEntry.Properties["Bytes"] = bytes;
        logEntry.Properties["DurationMs"] = duration.TotalMilliseconds;
        logEntry.Properties["BandwidthGBPerSec"] = bandwidthGBPerSec;
        logEntry.Properties["AccessPattern"] = metrics.AccessPattern;
        logEntry.Properties["CoalescingEfficiency"] = metrics.CoalescingEfficiency;
        logEntry.Properties["CacheHitRate"] = metrics.CacheHitRate;
        logEntry.Properties["MemorySegment"] = metrics.MemorySegment;
        logEntry.Properties["TransferDirection"] = metrics.TransferDirection;
        logEntry.Properties["QueueDepth"] = metrics.QueueDepth;
        logEntry.Properties["Success"] = exception == null;


        if (!string.IsNullOrEmpty(correlationId))
        {
            logEntry.CorrelationId = correlationId;
            logEntry.Properties["CorrelationId"] = correlationId;
        }


        _logEnricher.EnrichLogEntry(logEntry);
        _ = _logBuffer.AddLogEntry(logEntry);
    }

    /// <summary>
    /// Logs system performance snapshots for monitoring and alerting.
    /// </summary>
    public void LogSystemPerformance(SystemPerformanceSnapshot snapshot, string? correlationId = null)
    {
        ThrowIfDisposed();


        var logEntry = new StructuredLogEntry
        {
            Timestamp = snapshot.Timestamp,
            LogLevel = LogLevel.Information,
            Category = $"{_categoryName}.Performance",
            Message = "System performance snapshot",
            FormattedMessage = $"System performance: CPU {snapshot.CpuUtilizationPercent:F1}%, " +
                              $"Memory {snapshot.UsedMemoryBytes:N0} bytes, " +
                              $"Threads {snapshot.ProcessThreadCount}",
            EventId = new EventId(1003, "SystemPerformance")
        };

        logEntry.Properties["CpuUtilization"] = snapshot.CpuUtilizationPercent;
        logEntry.Properties["MemoryUsage"] = snapshot.UsedMemoryBytes;
        logEntry.Properties["AvailableMemory"] = snapshot.AvailableMemoryBytes;
        logEntry.Properties["TotalMemory"] = snapshot.TotalMemoryBytes;
        logEntry.Properties["ProcessWorkingSet"] = snapshot.ProcessWorkingSetBytes;
        logEntry.Properties["ProcessPrivateMemory"] = snapshot.ProcessPrivateMemoryBytes;
        logEntry.Properties["ThreadCount"] = snapshot.ProcessThreadCount;
        logEntry.Properties["HandleCount"] = snapshot.ProcessHandleCount;
        logEntry.Properties["Gen0Collections"] = snapshot.GCGen0Collections;
        logEntry.Properties["Gen1Collections"] = snapshot.GCGen1Collections;
        logEntry.Properties["Gen2Collections"] = snapshot.GCGen2Collections;
        logEntry.Properties["GCTotalMemory"] = snapshot.GCTotalMemoryBytes;
        logEntry.Properties["SystemLoadFactor"] = snapshot.SystemLoadFactor;
        logEntry.Properties["LogicalProcessorCount"] = snapshot.LogicalProcessorCount;

        // Note: Hardware counters would be added here if available


        if (!string.IsNullOrEmpty(correlationId))
        {
            logEntry.CorrelationId = correlationId;
            logEntry.Properties["CorrelationId"] = correlationId;
        }


        _logEnricher.EnrichLogEntry(logEntry);
        _ = _logBuffer.AddLogEntry(logEntry);
    }

    /// <summary>
    /// Logs distributed operation traces for cross-device analysis.
    /// </summary>
    public void LogDistributedOperation(string operationName, string correlationId,

        DistributedOperationMetrics metrics, Exception? exception = null)
    {
        ThrowIfDisposed();


        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            LogLevel = exception != null ? LogLevel.Error : LogLevel.Information,
            Category = $"{_categoryName}.Distributed",
            Message = "Distributed operation completed",
            FormattedMessage = $"Distributed operation '{operationName}' completed: " +
                              $"{metrics.TotalSpans} spans across {metrics.DeviceCount} devices " +
                              $"in {metrics.TotalDuration.TotalMilliseconds:F2}ms",
            Exception = exception,
            EventId = new EventId(1004, "DistributedOperation"),
            CorrelationId = correlationId
        };

        logEntry.Properties["OperationName"] = operationName;
        logEntry.Properties["CorrelationId"] = correlationId;
        logEntry.Properties["TotalDurationMs"] = metrics.TotalDuration.TotalMilliseconds;
        logEntry.Properties["SpanCount"] = metrics.TotalSpans;
        logEntry.Properties["DeviceCount"] = metrics.DeviceCount;
        logEntry.Properties["ParallelismEfficiency"] = metrics.ParallelismEfficiency;
        logEntry.Properties["DeviceEfficiency"] = metrics.DeviceEfficiency;
        logEntry.Properties["CriticalPathDurationMs"] = metrics.CriticalPathDuration;
        logEntry.Properties["Success"] = exception == null;

        // Add device-specific metrics

        foreach (var deviceMetric in metrics.DeviceMetrics)
        {
            logEntry.Properties[$"Device_{deviceMetric.Key}_Operations"] = deviceMetric.Value.OperationCount;
            logEntry.Properties[$"Device_{deviceMetric.Key}_Utilization"] = deviceMetric.Value.UtilizationPercentage;
        }


        _logEnricher.EnrichLogEntry(logEntry);
        _ = _logBuffer.AddLogEntry(logEntry);
    }

    /// <summary>
    /// Logs security events for auditing and compliance.
    /// </summary>
    public void LogSecurityEvent(SecurityEventType eventType, string description,

        Dictionary<string, object>? context = null, string? correlationId = null)
    {
        ThrowIfDisposed();


        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            LogLevel = eventType == SecurityEventType.SecurityViolation ? LogLevel.Warning : LogLevel.Information,
            Category = $"{_categoryName}.Security",
            Message = "Security event occurred",
            FormattedMessage = $"Security event: {eventType} - {description}",
            EventId = new EventId(2001, "SecurityEvent")
        };

        logEntry.Properties["SecurityEventType"] = eventType.ToString();
        logEntry.Properties["Description"] = description;
        logEntry.Properties["Severity"] = GetSecuritySeverity(eventType);


        if (context != null)
        {
            foreach (var item in context)
            {
                logEntry.Properties[item.Key] = item.Value;
            }
        }


        if (!string.IsNullOrEmpty(correlationId))
        {
            logEntry.CorrelationId = correlationId;
            logEntry.Properties["CorrelationId"] = correlationId;
        }


        _logEnricher.EnrichLogEntry(logEntry);
        _ = _logBuffer.AddLogEntry(logEntry);

        // Log security violations immediately

        if (eventType == SecurityEventType.SecurityViolation)
        {
            _baseLogger.LogWarning("Security violation detected: {Description} with context {@Context}",
                description, context ?? []);
        }
    }

    /// <summary>
    /// Forces immediate flush of buffered log entries.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        await _logBuffer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a property to the global logging context.
    /// </summary>
    public void AddGlobalProperty(string key, object value)
    {
        ThrowIfDisposed();


        _ = _globalContext.AddOrUpdate(key, value, (k, v) => value);
    }

    /// <summary>
    /// Removes a property from the global logging context.
    /// </summary>
    public void RemoveGlobalProperty(string key)
    {
        ThrowIfDisposed();


        _ = _globalContext.TryRemove(key, out _);
    }

    private StructuredLogEntry CreateStructuredLogEntry<TState>(LogLevel logLevel, EventId eventId,

        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            LogLevel = logLevel,
            Category = _categoryName,
            Message = state?.ToString() ?? string.Empty,
            FormattedMessage = formatter(state, exception),
            Exception = exception,
            EventId = eventId
        };

        // Add global context

        foreach (var contextItem in _globalContext)
        {
            logEntry.Properties[contextItem.Key] = contextItem.Value;
        }

        // Extract properties from state if it's a structured type

        ExtractPropertiesFromState(state, logEntry.Properties);

        // Add trace context

        var activity = Activity.Current;
        if (activity != null)
        {
            logEntry.TraceId = activity.TraceId.ToString();
            logEntry.SpanId = activity.SpanId.ToString();
        }


        return logEntry;
    }

    private static void ExtractPropertiesFromState<TState>(TState state, Dictionary<string, object> properties)
    {
        if (state == null)
        {
            return;
        }

        // Handle common structured logging patterns

        if (state is IEnumerable<KeyValuePair<string, object?>> keyValuePairs)
        {
            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Value != null)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }
        }
        else if (state is IDictionary<string, object> dictionary)
        {
            foreach (var kvp in dictionary)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }
    }

    private static string GetSecuritySeverity(SecurityEventType eventType)
    {
        return eventType switch
        {
            SecurityEventType.SecurityViolation => "High",
            SecurityEventType.UnauthorizedAccess => "High",
            SecurityEventType.InvalidInput => "Medium",
            SecurityEventType.ResourceAccess => "Low",
            SecurityEventType.ConfigurationChange => "Medium",
            _ => "Low"
        };
    }

    private void FlushLogs(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _logBuffer.FlushAsync();
                }
                catch (Exception ex)
                {
                    LogFlushFailed(_baseLogger, ex);
                }
            });
        }
        catch (Exception ex)
        {
            LogFlushTaskFailed(_baseLogger, ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
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
            // Final flush
            _logBuffer.FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LogFinalFlushFailed(_baseLogger, ex);
        }


        _flushTimer?.Dispose();
    }
}
/// <summary>
/// A class that represents structured logging options.
/// </summary>

// Supporting data structures and enums
public sealed class StructuredLoggingOptions
{
    /// <summary>
    /// Gets or sets the enable synchronous logging.
    /// </summary>
    /// <value>The enable synchronous logging.</value>
    public bool EnableSynchronousLogging { get; set; }
    /// <summary>
    /// Gets or sets the flush interval seconds.
    /// </summary>
    /// <value>The flush interval seconds.</value>

    public int FlushIntervalSeconds { get; set; } = 5;
    /// <summary>
    /// Gets or sets the max buffer size.
    /// </summary>
    /// <value>The max buffer size.</value>
    public int MaxBufferSize { get; set; } = 10000;
    /// <summary>
    /// Gets or sets the enable sensitive data redaction.
    /// </summary>
    /// <value>The enable sensitive data redaction.</value>
    public bool EnableSensitiveDataRedaction { get; set; } = true;
    /// <summary>
    /// Gets or sets the minimum log level.
    /// </summary>
    /// <value>The minimum log level.</value>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
}
/// <summary>
/// A class that represents structured log entry.
/// </summary>

public sealed class StructuredLogEntry
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    /// <value>The log level.</value>
    public LogLevel LogLevel { get; set; }
    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    /// <value>The category.</value>
    public string Category { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public string Message { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the formatted message.
    /// </summary>
    /// <value>The formatted message.</value>
    public string FormattedMessage { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the exception.
    /// </summary>
    /// <value>The exception.</value>
    public Exception? Exception { get; set; }
    /// <summary>
    /// Gets or sets the event identifier.
    /// </summary>
    /// <value>The event id.</value>
    public EventId EventId { get; set; }
    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    /// <value>The correlation id.</value>
    public string? CorrelationId { get; set; }
    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    /// <value>The trace id.</value>
    public string? TraceId { get; set; }
    /// <summary>
    /// Gets or sets the span identifier.
    /// </summary>
    /// <value>The span id.</value>
    public string? SpanId { get; set; }
    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    /// <value>The properties.</value>
    public Dictionary<string, object> Properties { get; init; } = [];
    /// <summary>
    /// Gets or sets the performance metrics.
    /// </summary>
    /// <value>The performance metrics.</value>
    public LogPerformanceMetrics? PerformanceMetrics { get; set; }
}
/// <summary>
/// A class that represents log performance metrics.
/// </summary>

public sealed class LogPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the execution time ms.
    /// </summary>
    /// <value>The execution time ms.</value>
    public double ExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public double ThroughputOpsPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the memory usage bytes.
    /// </summary>
    /// <value>The memory usage bytes.</value>
    public long MemoryUsageBytes { get; set; }
    /// <summary>
    /// Gets or sets the cache hit ratio.
    /// </summary>
    /// <value>The cache hit ratio.</value>
    public double CacheHitRatio { get; set; }
    /// <summary>
    /// Gets or sets the device utilization percentage.
    /// </summary>
    /// <value>The device utilization percentage.</value>
    public double DeviceUtilizationPercentage { get; set; }
}
/// <summary>
/// A class that represents kernel performance metrics.
/// </summary>

public sealed class KernelPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public double ThroughputOpsPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the occupancy percentage.
    /// </summary>
    /// <value>The occupancy percentage.</value>
    public double OccupancyPercentage { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth g b per second.
    /// </summary>
    /// <value>The memory bandwidth g b per second.</value>
    public double MemoryBandwidthGBPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the instruction throughput.
    /// </summary>
    /// <value>The instruction throughput.</value>
    public double InstructionThroughput { get; set; }
    /// <summary>
    /// Gets or sets the warp efficiency.
    /// </summary>
    /// <value>The warp efficiency.</value>
    public double WarpEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the branch divergence.
    /// </summary>
    /// <value>The branch divergence.</value>
    public double BranchDivergence { get; set; }
    /// <summary>
    /// Gets or sets the power consumption.
    /// </summary>
    /// <value>The power consumption.</value>
    public double PowerConsumption { get; set; }
    /// <summary>
    /// Gets or sets the memory usage bytes.
    /// </summary>
    /// <value>The memory usage bytes.</value>
    public long MemoryUsageBytes { get; set; }
    /// <summary>
    /// Gets or sets the device utilization.
    /// </summary>
    /// <value>The device utilization.</value>
    public double DeviceUtilization { get; set; }
}
/// <summary>
/// A class that represents memory access metrics.
/// </summary>

public sealed class MemoryAccessMetrics
{
    /// <summary>
    /// Gets or sets the access pattern.
    /// </summary>
    /// <value>The access pattern.</value>
    public string AccessPattern { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the coalescing efficiency.
    /// </summary>
    /// <value>The coalescing efficiency.</value>
    public double CoalescingEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the memory segment.
    /// </summary>
    /// <value>The memory segment.</value>
    public string MemorySegment { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the transfer direction.
    /// </summary>
    /// <value>The transfer direction.</value>
    public string TransferDirection { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the queue depth.
    /// </summary>
    /// <value>The queue depth.</value>
    public int QueueDepth { get; set; }
}
/// <summary>
/// A class that represents distributed operation metrics.
/// </summary>

public sealed class DistributedOperationMetrics
{
    /// <summary>
    /// Gets or sets the total duration.
    /// </summary>
    /// <value>The total duration.</value>
    public TimeSpan TotalDuration { get; set; }
    /// <summary>
    /// Gets or sets the total spans.
    /// </summary>
    /// <value>The total spans.</value>
    public int TotalSpans { get; set; }
    /// <summary>
    /// Gets or sets the device count.
    /// </summary>
    /// <value>The device count.</value>
    public int DeviceCount { get; set; }
    /// <summary>
    /// Gets or sets the parallelism efficiency.
    /// </summary>
    /// <value>The parallelism efficiency.</value>
    public double ParallelismEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the device efficiency.
    /// </summary>
    /// <value>The device efficiency.</value>
    public double DeviceEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the critical path duration.
    /// </summary>
    /// <value>The critical path duration.</value>
    public double CriticalPathDuration { get; set; }
    /// <summary>
    /// Gets or sets the device metrics.
    /// </summary>
    /// <value>The device metrics.</value>
    public Dictionary<string, DeviceOperationMetrics> DeviceMetrics { get; init; } = [];
}
/// <summary>
/// A class that represents device operation metrics.
/// </summary>

public sealed class DeviceOperationMetrics
{
    /// <summary>
    /// Gets or sets the operation count.
    /// </summary>
    /// <value>The operation count.</value>
    public int OperationCount { get; set; }
    /// <summary>
    /// Gets or sets the utilization percentage.
    /// </summary>
    /// <value>The utilization percentage.</value>
    public double UtilizationPercentage { get; set; }
}
/// <summary>
/// An security event type enumeration.
/// </summary>

public enum SecurityEventType
{
    ResourceAccess,
    UnauthorizedAccess,
    InvalidInput,
    SecurityViolation,
    ConfigurationChange
}