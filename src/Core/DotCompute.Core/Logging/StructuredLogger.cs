using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DotCompute.Core.Types;

namespace DotCompute.Core.Logging;

/// <summary>
/// Production-grade structured logger with semantic properties, correlation IDs, and performance metrics.
/// Provides async, buffered logging with configurable sinks and minimal performance impact.
/// </summary>
public sealed class StructuredLogger : ILogger, IDisposable
{
    private readonly string _categoryName;
    private readonly ILogger _baseLogger;
    private readonly StructuredLoggingOptions _options;
    private readonly LogBuffer _logBuffer;
    private readonly LogEnricher _logEnricher;
    private readonly ConcurrentDictionary<string, object> _globalContext;
    private readonly Timer _flushTimer;
    private volatile bool _disposed;

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

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _baseLogger.BeginScope(state);

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
            _logBuffer.AddLogEntry(logEntry);
            
            // Also log through base logger for immediate visibility if needed
            if (_options.EnableSynchronousLogging || logLevel >= LogLevel.Error)
            {
                _baseLogger.Log(logLevel, eventId, logEntry, exception, (entry, ex) => entry.FormattedMessage);
            }
        }
        catch (Exception ex)
        {
            // Never throw from logging - log the error through base logger
            _baseLogger.LogError(ex, "Failed to process structured log entry");
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
            EventId = new EventId(1001, "KernelExecution"),
            
            Properties = new Dictionary<string, object>
            {
                ["KernelName"] = kernelName,
                ["DeviceId"] = deviceId,
                ["ExecutionTimeMs"] = executionTime.TotalMilliseconds,
                ["ThroughputOpsPerSec"] = metrics.ThroughputOpsPerSecond,
                ["OccupancyPercentage"] = metrics.OccupancyPercentage,
                ["MemoryBandwidthGBPerSec"] = metrics.MemoryBandwidthGBPerSecond,
                ["CacheHitRate"] = metrics.CacheHitRate,
                ["InstructionThroughput"] = metrics.InstructionThroughput,
                ["WarpEfficiency"] = metrics.WarpEfficiency,
                ["BranchDivergence"] = metrics.BranchDivergence,
                ["PowerConsumption"] = metrics.PowerConsumption,
                ["Success"] = exception == null
            },
            
            PerformanceMetrics = new LogPerformanceMetrics
            {
                ExecutionTimeMs = executionTime.TotalMilliseconds,
                ThroughputOpsPerSecond = metrics.ThroughputOpsPerSecond,
                MemoryUsageBytes = metrics.MemoryUsageBytes,
                CacheHitRatio = metrics.CacheHitRate,
                DeviceUtilizationPercentage = metrics.DeviceUtilization
            }
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
        _logBuffer.AddLogEntry(logEntry);
        
        // Log critical kernel failures immediately
        if (exception != null)
        {
            _baseLogger.LogError(exception, "Kernel execution failed: {KernelName} on {DeviceId} after {ExecutionTime}ms",
                kernelName, deviceId, executionTime.TotalMilliseconds);
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
            EventId = new EventId(1002, "MemoryOperation"),
            
            Properties = new Dictionary<string, object>
            {
                ["OperationType"] = operationType,
                ["DeviceId"] = deviceId,
                ["Bytes"] = bytes,
                ["DurationMs"] = duration.TotalMilliseconds,
                ["BandwidthGBPerSec"] = bandwidthGBPerSec,
                ["AccessPattern"] = metrics.AccessPattern,
                ["CoalescingEfficiency"] = metrics.CoalescingEfficiency,
                ["CacheHitRate"] = metrics.CacheHitRate,
                ["MemorySegment"] = metrics.MemorySegment,
                ["TransferDirection"] = metrics.TransferDirection,
                ["QueueDepth"] = metrics.QueueDepth,
                ["Success"] = exception == null
            }
        };
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            logEntry.CorrelationId = correlationId;
            logEntry.Properties["CorrelationId"] = correlationId;
        }
        
        _logEnricher.EnrichLogEntry(logEntry);
        _logBuffer.AddLogEntry(logEntry);
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
            EventId = new EventId(1003, "SystemPerformance"),
            
            Properties = new Dictionary<string, object>
            {
                ["CpuUtilization"] = snapshot.CpuUtilizationPercent,
                ["MemoryUsage"] = snapshot.UsedMemoryBytes,
                ["AvailableMemory"] = snapshot.AvailableMemoryBytes,
                ["TotalMemory"] = snapshot.TotalMemoryBytes,
                ["ProcessWorkingSet"] = snapshot.ProcessWorkingSetBytes,
                ["ProcessPrivateMemory"] = snapshot.ProcessPrivateMemoryBytes,
                ["ThreadCount"] = snapshot.ProcessThreadCount,
                ["HandleCount"] = snapshot.ProcessHandleCount,
                ["Gen0Collections"] = snapshot.GCGen0Collections,
                ["Gen1Collections"] = snapshot.GCGen1Collections,
                ["Gen2Collections"] = snapshot.GCGen2Collections,
                ["GCTotalMemory"] = snapshot.GCTotalMemoryBytes,
                ["SystemLoadFactor"] = snapshot.SystemLoadFactor,
                ["LogicalProcessorCount"] = snapshot.LogicalProcessorCount
            }
        };
        
        // Note: Hardware counters would be added here if available
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            logEntry.CorrelationId = correlationId;
            logEntry.Properties["CorrelationId"] = correlationId;
        }
        
        _logEnricher.EnrichLogEntry(logEntry);
        _logBuffer.AddLogEntry(logEntry);
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
            CorrelationId = correlationId,
            
            Properties = new Dictionary<string, object>
            {
                ["OperationName"] = operationName,
                ["CorrelationId"] = correlationId,
                ["TotalDurationMs"] = metrics.TotalDuration.TotalMilliseconds,
                ["SpanCount"] = metrics.TotalSpans,
                ["DeviceCount"] = metrics.DeviceCount,
                ["ParallelismEfficiency"] = metrics.ParallelismEfficiency,
                ["DeviceEfficiency"] = metrics.DeviceEfficiency,
                ["CriticalPathDurationMs"] = metrics.CriticalPathDuration,
                ["Success"] = exception == null
            }
        };
        
        // Add device-specific metrics
        foreach (var deviceMetric in metrics.DeviceMetrics)
        {
            logEntry.Properties[$"Device_{deviceMetric.Key}_Operations"] = deviceMetric.Value.OperationCount;
            logEntry.Properties[$"Device_{deviceMetric.Key}_Utilization"] = deviceMetric.Value.UtilizationPercentage;
        }
        
        _logEnricher.EnrichLogEntry(logEntry);
        _logBuffer.AddLogEntry(logEntry);
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
            EventId = new EventId(2001, "SecurityEvent"),
            
            Properties = new Dictionary<string, object>
            {
                ["SecurityEventType"] = eventType.ToString(),
                ["Description"] = description,
                ["Severity"] = GetSecuritySeverity(eventType)
            }
        };
        
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
        _logBuffer.AddLogEntry(logEntry);
        
        // Log security violations immediately
        if (eventType == SecurityEventType.SecurityViolation)
        {
            _baseLogger.LogWarning("Security violation detected: {Description} with context {@Context}",
                description, context ?? new Dictionary<string, object>());
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
        
        _globalContext.AddOrUpdate(key, value, (k, v) => value);
    }

    /// <summary>
    /// Removes a property from the global logging context.
    /// </summary>
    public void RemoveGlobalProperty(string key)
    {
        ThrowIfDisposed();
        
        _globalContext.TryRemove(key, out _);
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
            EventId = eventId,
            Properties = new Dictionary<string, object>()
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
                    _baseLogger.LogError(ex, "Failed to flush log buffer during timer flush");
                }
            });
        }
        catch (Exception ex)
        {
            _baseLogger.LogError(ex, "Failed to start log flush task");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(StructuredLogger));
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
            // Final flush
            _logBuffer.FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _baseLogger.LogError(ex, "Failed to perform final flush during dispose");
        }
        
        _flushTimer?.Dispose();
    }
}

// Supporting data structures and enums
public sealed class StructuredLoggingOptions
{
    public bool EnableSynchronousLogging { get; set; } = false;
    public int FlushIntervalSeconds { get; set; } = 5;
    public int MaxBufferSize { get; set; } = 10000;
    public bool EnableSensitiveDataRedaction { get; set; } = true;
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
}

public sealed class StructuredLogEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel LogLevel { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string FormattedMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public EventId EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public LogPerformanceMetrics? PerformanceMetrics { get; set; }
}

public sealed class LogPerformanceMetrics
{
    public double ExecutionTimeMs { get; set; }
    public double ThroughputOpsPerSecond { get; set; }
    public long MemoryUsageBytes { get; set; }
    public double CacheHitRatio { get; set; }
    public double DeviceUtilizationPercentage { get; set; }
}

public sealed class KernelPerformanceMetrics
{
    public double ThroughputOpsPerSecond { get; set; }
    public double OccupancyPercentage { get; set; }
    public double MemoryBandwidthGBPerSecond { get; set; }
    public double CacheHitRate { get; set; }
    public double InstructionThroughput { get; set; }
    public double WarpEfficiency { get; set; }
    public double BranchDivergence { get; set; }
    public double PowerConsumption { get; set; }
    public long MemoryUsageBytes { get; set; }
    public double DeviceUtilization { get; set; }
}

public sealed class MemoryAccessMetrics
{
    public string AccessPattern { get; set; } = string.Empty;
    public double CoalescingEfficiency { get; set; }
    public double CacheHitRate { get; set; }
    public string MemorySegment { get; set; } = string.Empty;
    public string TransferDirection { get; set; } = string.Empty;
    public int QueueDepth { get; set; }
}

public sealed class DistributedOperationMetrics
{
    public TimeSpan TotalDuration { get; set; }
    public int TotalSpans { get; set; }
    public int DeviceCount { get; set; }
    public double ParallelismEfficiency { get; set; }
    public double DeviceEfficiency { get; set; }
    public double CriticalPathDuration { get; set; }
    public Dictionary<string, DeviceOperationMetrics> DeviceMetrics { get; set; } = new();
}

public sealed class DeviceOperationMetrics
{
    public int OperationCount { get; set; }
    public double UtilizationPercentage { get; set; }
}

public enum SecurityEventType
{
    ResourceAccess,
    UnauthorizedAccess,
    InvalidInput,
    SecurityViolation,
    ConfigurationChange
}