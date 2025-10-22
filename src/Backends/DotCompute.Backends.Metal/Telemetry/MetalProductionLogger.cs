// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.Execution;

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Production-grade structured logging with correlation IDs and enterprise integration
/// </summary>
public sealed partial class MetalProductionLogger : IDisposable
{
    private readonly ILogger<MetalProductionLogger> _logger;
    private readonly MetalLoggingOptions _options;
    private readonly ConcurrentDictionary<string, LogContext> _activeContexts;
    private readonly ConcurrentQueue<StructuredLogEntry> _logBuffer;
    private readonly Timer? _bufferFlushTimer;
    private volatile bool _disposed;

    public MetalProductionLogger(
        ILogger<MetalProductionLogger> logger,
        MetalLoggingOptions options)
    {
        _logger = logger;
        _options = options;
        _activeContexts = new ConcurrentDictionary<string, LogContext>();
        _logBuffer = new ConcurrentQueue<StructuredLogEntry>();

        if (_options.EnableBuffering && _options.BufferFlushInterval > TimeSpan.Zero)
        {
            _bufferFlushTimer = new Timer(FlushLogBuffer, null, _options.BufferFlushInterval, _options.BufferFlushInterval);
        }

        LogProductionLoggerInitialized(_logger, _options.EnableBuffering, _options.EnableCorrelationTracking);
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6700,
        Level = LogLevel.Information,
        Message = "Metal production logger initialized with buffering: {Buffering}, correlation tracking: {CorrelationTracking}")]
    private static partial void LogProductionLoggerInitialized(ILogger logger, bool buffering, bool correlationTracking);

    [LoggerMessage(
        EventId = 6701,
        Level = LogLevel.Trace,
        Message = "Flushed {Count} buffered log entries")]
    private static partial void LogBufferFlushed(ILogger logger, int count);

    [LoggerMessage(
        EventId = 6702,
        Level = LogLevel.Debug,
        Message = "Cleaned up {Count} expired log contexts")]
    private static partial void LogContextsCleanedUp(ILogger logger, int count);

    [LoggerMessage(
        EventId = 6703,
        Level = LogLevel.Information,
        Message = "Metal telemetry report: {TotalEvents} events, {MetricCount} metrics tracked")]
    private static partial void LogTelemetryReport(ILogger logger, int totalEvents, int metricCount);

    [LoggerMessage(
        EventId = 6704,
        Level = LogLevel.Debug,
        Message = "Top events: {Events}")]
    private static partial void LogTopEvents(ILogger logger, string events);

    [LoggerMessage(
        EventId = 6705,
        Level = LogLevel.Warning,
        Message = "Error generating periodic telemetry report")]
    private static partial void LogTelemetryReportError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6706,
        Level = LogLevel.Error,
        Message = "Failed to write structured log entry: {EventType}")]
    private static partial void LogWriteEntryFailed(ILogger logger, Exception ex, string eventType);

    [LoggerMessage(
        EventId = 6707,
        Level = LogLevel.Warning,
        Message = "Failed to write log entry to external endpoint: {Endpoint}")]
    private static partial void LogExternalEndpointFailed(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(
        EventId = 6708,
        Level = LogLevel.Information,
        Message = "Metal production logger disposed - Active contexts: {ActiveContexts}, Buffer size: {BufferSize}")]
    private static partial void LogProductionLoggerDisposed(ILogger logger, int activeContexts, int bufferSize);

    [LoggerMessage(
        EventId = 6709,
        Level = LogLevel.Information,
        Message = "Metal execution telemetry initialized with reporting interval: {Interval}")]
    private static partial void LogTelemetryInitialized(ILogger logger, string interval);

    [LoggerMessage(
        EventId = 6710,
        Level = LogLevel.Information,
        Message = "Metal execution telemetry disposed: {TotalEvents} total events, {MetricCount} metrics")]
    private static partial void LogTelemetryDisposed(ILogger logger, int totalEvents, int metricCount);

    #endregion

    /// <summary>
    /// Generates a new correlation ID for tracking related operations
    /// </summary>
    public static string GenerateCorrelationId() => $"metal_{Guid.NewGuid():N}"[..24]; // 24 character correlation ID

    /// <summary>
    /// Starts a new logging context with correlation ID
    /// </summary>
    public LogContext StartContext(string operationType, string? correlationId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        correlationId ??= GenerateCorrelationId();


        var context = new LogContext(correlationId, operationType, DateTimeOffset.UtcNow);


        if (_options.EnableCorrelationTracking)
        {
            _activeContexts[correlationId] = context;
        }

        LogStructuredEvent("ContextStarted", LogLevel.Debug, correlationId, new Dictionary<string, object>
        {
            ["operation_type"] = operationType,
            ["context_id"] = correlationId,
            ["start_time"] = context.StartTime
        });

        return context;
    }

    /// <summary>
    /// Ends a logging context
    /// </summary>
    public void EndContext(LogContext context, bool success = true, Dictionary<string, object>? additionalProperties = null)
    {
        if (_disposed)
        {
            return;
        }


        var duration = DateTimeOffset.UtcNow - context.StartTime;


        var properties = new Dictionary<string, object>
        {
            ["operation_type"] = context.OperationType,
            ["context_id"] = context.CorrelationId,
            ["duration_ms"] = duration.TotalMilliseconds,
            ["success"] = success,
            ["end_time"] = DateTimeOffset.UtcNow
        };

        if (additionalProperties != null)
        {
            foreach (var kvp in additionalProperties)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }

        var logLevel = success ? LogLevel.Debug : LogLevel.Warning;
        LogStructuredEvent("ContextEnded", logLevel, context.CorrelationId, properties);

        if (_options.EnableCorrelationTracking)
        {
            _ = _activeContexts.TryRemove(context.CorrelationId, out _);
        }
    }

    /// <summary>
    /// Logs memory allocation with structured data
    /// </summary>
    public void LogMemoryAllocation(long sizeBytes, TimeSpan duration, bool success, string? correlationId = null)
    {
        if (_disposed)
        {
            return;
        }


        correlationId ??= GenerateCorrelationId();

        var properties = new Dictionary<string, object>
        {
            ["operation"] = "memory_allocation",
            ["size_bytes"] = sizeBytes,
            ["size_mb"] = sizeBytes / (1024.0 * 1024.0),
            ["duration_ms"] = duration.TotalMilliseconds,
            ["success"] = success,
            ["allocation_rate_mbps"] = sizeBytes > 0 && duration.TotalSeconds > 0 ? (sizeBytes / (1024.0 * 1024.0)) / duration.TotalSeconds : 0,
            ["size_category"] = GetSizeCategory(sizeBytes)
        };

        var logLevel = success ? LogLevel.Debug : LogLevel.Error;


        if (duration.TotalMilliseconds > _options.SlowOperationThresholdMs)
        {
            logLevel = LogLevel.Warning;
            properties["performance_issue"] = "slow_allocation";
        }

        LogStructuredEvent("MemoryAllocation", logLevel, correlationId, properties);
    }

    /// <summary>
    /// Logs kernel execution with comprehensive metrics
    /// </summary>
    public void LogKernelExecution(string correlationId, string kernelName, TimeSpan duration, long dataSize,

        bool success, Dictionary<string, object>? additionalProperties = null)
    {
        if (_disposed)
        {
            return;
        }


        var throughputMBps = dataSize > 0 && duration.TotalSeconds > 0

            ? (dataSize / (1024.0 * 1024.0)) / duration.TotalSeconds

            : 0;

        var properties = new Dictionary<string, object>
        {
            ["operation"] = "kernel_execution",
            ["kernel_name"] = kernelName,
            ["duration_ms"] = duration.TotalMilliseconds,
            ["data_size_bytes"] = dataSize,
            ["data_size_mb"] = dataSize / (1024.0 * 1024.0),
            ["throughput_mbps"] = throughputMBps,
            ["success"] = success,
            ["performance_category"] = GetPerformanceCategory(duration.TotalMilliseconds)
        };

        if (additionalProperties != null)
        {
            foreach (var kvp in additionalProperties)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }

        var logLevel = success ? LogLevel.Information : LogLevel.Error;


        if (success && duration.TotalMilliseconds > _options.SlowOperationThresholdMs)
        {
            logLevel = LogLevel.Warning;
            properties["performance_issue"] = "slow_execution";
        }

        LogStructuredEvent("KernelExecution", logLevel, correlationId, properties);

        // Log additional performance metrics if enabled
        if (_options.EnablePerformanceLogging && success)
        {
            LogPerformanceMetrics(correlationId, kernelName, duration, throughputMBps);
        }
    }

    /// <summary>
    /// Logs device utilization metrics
    /// </summary>
    public void LogDeviceUtilization(Dictionary<string, object> utilizationMetrics, string? correlationId = null)
    {
        if (_disposed)
        {
            return;
        }


        correlationId ??= GenerateCorrelationId();

        var properties = new Dictionary<string, object>(utilizationMetrics)
        {
            ["operation"] = "device_utilization",
            ["timestamp"] = DateTimeOffset.UtcNow
        };

        LogStructuredEvent("DeviceUtilization", LogLevel.Debug, correlationId, properties);
    }

    /// <summary>
    /// Logs error with comprehensive context
    /// </summary>
    public void LogError(string correlationId, MetalError error, string context, Dictionary<string, object>? additionalContext = null)
    {
        if (_disposed)
        {
            return;
        }


        var properties = new Dictionary<string, object>
        {
            ["operation"] = "error_event",
            ["error_code"] = error.ToString(),
            ["error_category"] = GetErrorCategory(error),
            ["context"] = context,
            ["severity"] = GetErrorSeverity(error),
            ["recoverable"] = IsRecoverableError(error)
        };

        if (additionalContext != null)
        {
            foreach (var kvp in additionalContext)
            {
                properties[$"context_{kvp.Key}"] = kvp.Value;
            }
        }

        // Add context information if available
        if (_options.EnableCorrelationTracking && _activeContexts.TryGetValue(correlationId, out var logContext))
        {
            properties["context_operation_type"] = logContext.OperationType;
            properties["context_duration_ms"] = (DateTimeOffset.UtcNow - logContext.StartTime).TotalMilliseconds;
        }

        var logLevel = GetLogLevelForError(error);
        LogStructuredEvent("ErrorEvent", logLevel, correlationId, properties);

        // Log stack trace for critical errors if available
        if (error is MetalError.DeviceLost or MetalError.InternalError)
        {
            LogStackTrace(correlationId, Environment.StackTrace);
        }
    }

    /// <summary>
    /// Logs memory pressure events
    /// </summary>
    public void LogMemoryPressure(MemoryPressureLevel level, double percentage, string? correlationId = null)
    {
        if (_disposed)
        {
            return;
        }


        correlationId ??= GenerateCorrelationId();

        var properties = new Dictionary<string, object>
        {
            ["operation"] = "memory_pressure",
            ["pressure_level"] = level.ToString(),
            ["pressure_percentage"] = percentage,
            ["severity"] = GetPressureSeverity(level),
            ["action_required"] = level >= MemoryPressureLevel.High
        };

        var logLevel = level switch
        {
            MemoryPressureLevel.Critical => LogLevel.Critical,
            MemoryPressureLevel.High => LogLevel.Error,
            MemoryPressureLevel.Medium => LogLevel.Warning,
            _ => LogLevel.Information
        };

        LogStructuredEvent("MemoryPressure", logLevel, correlationId, properties);
    }

    /// <summary>
    /// Logs resource usage metrics
    /// </summary>
    public void LogResourceUsage(ResourceType type, long currentUsage, long peakUsage, long limit,

        double utilizationPercentage, string? correlationId = null)
    {
        if (_disposed)
        {
            return;
        }


        correlationId ??= GenerateCorrelationId();

        var properties = new Dictionary<string, object>
        {
            ["operation"] = "resource_usage",
            ["resource_type"] = type.ToString(),
            ["current_usage"] = currentUsage,
            ["peak_usage"] = peakUsage,
            ["limit"] = limit,
            ["utilization_percentage"] = utilizationPercentage,
            ["utilization_category"] = GetUtilizationCategory(utilizationPercentage),
            ["available"] = limit - currentUsage,
            ["peak_utilization_percentage"] = limit > 0 ? (double)peakUsage / limit * 100.0 : 0.0
        };

        var logLevel = utilizationPercentage switch
        {
            > 95 => LogLevel.Critical,
            > 85 => LogLevel.Warning,
            > 70 => LogLevel.Information,
            _ => LogLevel.Debug
        };

        LogStructuredEvent("ResourceUsage", logLevel, correlationId, properties);
    }

    /// <summary>
    /// Logs performance metrics
    /// </summary>
    private void LogPerformanceMetrics(string correlationId, string kernelName, TimeSpan duration, double throughputMBps)
    {
        var properties = new Dictionary<string, object>
        {
            ["operation"] = "performance_metrics",
            ["kernel_name"] = kernelName,
            ["execution_time_ms"] = duration.TotalMilliseconds,
            ["throughput_mbps"] = throughputMBps,
            ["performance_tier"] = GetPerformanceTier(duration.TotalMilliseconds, throughputMBps),
            ["efficiency_score"] = CalculateEfficiencyScore(duration.TotalMilliseconds, throughputMBps)
        };

        LogStructuredEvent("PerformanceMetrics", LogLevel.Debug, correlationId, properties);
    }

    /// <summary>
    /// Logs stack trace for debugging critical issues
    /// </summary>
    private void LogStackTrace(string correlationId, string stackTrace)
    {
        if (!_options.EnableStackTraceLogging)
        {
            return;
        }


        var properties = new Dictionary<string, object>
        {
            ["operation"] = "stack_trace",
            ["stack_trace"] = stackTrace,
            ["thread_id"] = Environment.CurrentManagedThreadId,
            ["process_id"] = Environment.ProcessId
        };

        LogStructuredEvent("StackTrace", LogLevel.Debug, correlationId, properties);
    }

    /// <summary>
    /// Core structured logging method
    /// </summary>
    private void LogStructuredEvent(string eventType, LogLevel logLevel, string correlationId, Dictionary<string, object> properties)
    {
        if (_disposed)
        {
            return;
        }

        // Add common properties

        properties["event_type"] = eventType;
        properties["correlation_id"] = correlationId;
        properties["timestamp"] = DateTimeOffset.UtcNow;
        properties["machine_name"] = Environment.MachineName;
        properties["process_id"] = Environment.ProcessId;
        properties["thread_id"] = Environment.CurrentManagedThreadId;

        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            LogLevel = logLevel,
            EventType = eventType,
            CorrelationId = correlationId,
            Message = FormatLogMessage(eventType, properties)
        };

        // Add properties to the collection
        foreach (var kvp in properties)
        {
            logEntry.Properties[kvp.Key] = kvp.Value;
        }

        if (_options.EnableBuffering)
        {
            BufferLogEntry(logEntry);
        }
        else
        {
            WriteLogEntry(logEntry);
        }
    }

    private void BufferLogEntry(StructuredLogEntry entry)
    {
        _logBuffer.Enqueue(entry);

        // Maintain buffer size
        while (_logBuffer.Count > _options.MaxBufferSize)
        {
            _ = _logBuffer.TryDequeue(out _);
        }

        // Immediate flush for high-priority entries
        if (entry.LogLevel >= LogLevel.Error)
        {
            FlushLogBuffer(null);
        }
    }

    private void WriteLogEntry(StructuredLogEntry entry)
    {
        try
        {
            // Format message based on configuration
            var message = _options.UseJsonFormat

                ? JsonSerializer.Serialize(entry.Properties)
                : entry.Message;

            _logger.Log(entry.LogLevel, "[{CorrelationId}] {EventType}: {Message}",
                entry.CorrelationId, entry.EventType, message);

            // Write to external systems if configured
            if (_options.ExternalLogEndpoints?.Count > 0)
            {
                _ = Task.Run(() => WriteToExternalSystemsAsync(entry));
            }
        }
        catch (Exception ex)
        {
            // Fallback logging to prevent recursive issues
            try
            {
                LogWriteEntryFailed(_logger, ex, entry.EventType);
            }
            catch
            {
                // Suppress any logging failures during error handling
            }
        }
    }

    private void FlushLogBuffer(object? state)
    {
        if (_disposed)
        {
            return;
        }


        var entriesToFlush = new List<StructuredLogEntry>();


        while (_logBuffer.TryDequeue(out var entry))
        {
            entriesToFlush.Add(entry);
        }

        foreach (var entry in entriesToFlush)
        {
            WriteLogEntry(entry);
        }

        if (entriesToFlush.Count > 0)
        {
            LogBufferFlushed(_logger, entriesToFlush.Count);
        }
    }

    private async Task WriteToExternalSystemsAsync(StructuredLogEntry entry)
    {
        if (_options.ExternalLogEndpoints == null)
        {
            return;
        }


        foreach (var endpoint in _options.ExternalLogEndpoints)
        {
            try
            {
                await WriteToExternalEndpointAsync(endpoint, entry);
            }
            catch (Exception ex)
            {
                LogExternalEndpointFailed(_logger, ex, endpoint);
            }
        }
    }

    private static async Task WriteToExternalEndpointAsync(string endpoint, StructuredLogEntry entry)
        // Placeholder for external system integration
        // Would implement specific integrations for:
        // - Elasticsearch
        // - Splunk
        // - Application Insights
        // - DataDog
        // - Custom log aggregation services



        => await Task.Delay(10); // Simulate async operation

    /// <summary>
    /// Performs cleanup of old log contexts
    /// </summary>
    public void PerformCleanup(DateTimeOffset cutoffTime)
    {
        if (_disposed)
        {
            return;
        }


        var expiredContexts = _activeContexts
            .Where(kvp => kvp.Value.StartTime < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var contextId in expiredContexts)
        {
            _ = _activeContexts.TryRemove(contextId, out _);
        }

        if (expiredContexts.Count > 0)
        {
            LogContextsCleanedUp(_logger, expiredContexts.Count);
        }
    }

    private static string FormatLogMessage(string eventType, Dictionary<string, object> properties)
    {
        return eventType switch
        {
            "MemoryAllocation" => FormatMemoryAllocationMessage(properties),
            "KernelExecution" => FormatKernelExecutionMessage(properties),
            "ErrorEvent" => FormatErrorMessage(properties),
            "MemoryPressure" => FormatMemoryPressureMessage(properties),
            "ResourceUsage" => FormatResourceUsageMessage(properties),
            _ => $"{eventType} event occurred"
        };
    }

    private static string FormatMemoryAllocationMessage(Dictionary<string, object> properties)
    {
        var success = (bool)properties["success"];
        var sizeBytes = (long)properties["size_bytes"];
        var durationMs = (double)properties["duration_ms"];

        return success

            ? $"Allocated {FormatBytes(sizeBytes)} in {durationMs:F2}ms"
            : $"Failed to allocate {FormatBytes(sizeBytes)} after {durationMs:F2}ms";
    }

    private static string FormatKernelExecutionMessage(Dictionary<string, object> properties)
    {
        var success = (bool)properties["success"];
        var kernelName = (string)properties["kernel_name"];
        var durationMs = (double)properties["duration_ms"];
        var throughputMBps = (double)properties["throughput_mbps"];

        return success
            ? $"Executed kernel '{kernelName}' in {durationMs:F2}ms ({throughputMBps:F1} MB/s)"
            : $"Failed to execute kernel '{kernelName}' after {durationMs:F2}ms";
    }

    private static string FormatErrorMessage(Dictionary<string, object> properties)
    {
        var errorCode = (string)properties["error_code"];
        var context = (string)properties["context"];
        var severity = (string)properties["severity"];

        return $"{severity} error {errorCode} in {context}";
    }

    private static string FormatMemoryPressureMessage(Dictionary<string, object> properties)
    {
        var level = (string)properties["pressure_level"];
        var percentage = (double)properties["pressure_percentage"];

        return $"Memory pressure: {level} ({percentage:F1}%)";
    }

    private static string FormatResourceUsageMessage(Dictionary<string, object> properties)
    {
        var resourceType = (string)properties["resource_type"];
        var utilizationPercentage = (double)properties["utilization_percentage"];
        var currentUsage = (long)properties["current_usage"];

        return $"{resourceType} usage: {utilizationPercentage:F1}% ({FormatBytes(currentUsage)})";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };
    }

    private static string GetSizeCategory(long sizeBytes)
    {
        return sizeBytes switch
        {
            < 4096 => "small",
            < 1024 * 1024 => "medium",
            < 100 * 1024 * 1024 => "large",
            _ => "xlarge"
        };
    }

    private static string GetPerformanceCategory(double durationMs)
    {
        return durationMs switch
        {
            < 1 => "fast",
            < 10 => "normal",
            < 100 => "slow",
            _ => "very_slow"
        };
    }

    private static string GetErrorCategory(MetalError error)
    {
        return error switch
        {
            MetalError.OutOfMemory or MetalError.ResourceLimitExceeded => "resource",
            MetalError.InvalidOperation or MetalError.InvalidArgument => "validation",
            MetalError.DeviceUnavailable or MetalError.DeviceLost => "device",
            MetalError.CompilationError => "compilation",
            MetalError.InternalError => "internal",
            _ => "unknown"
        };
    }

    private static string GetErrorSeverity(MetalError error)
    {
        return error switch
        {
            MetalError.DeviceLost => "critical",
            MetalError.OutOfMemory or MetalError.DeviceUnavailable => "high",
            MetalError.ResourceLimitExceeded or MetalError.CompilationError => "medium",
            _ => "low"
        };
    }

    private static bool IsRecoverableError(MetalError error)
    {
        return error switch
        {
            MetalError.DeviceLost => false,
            MetalError.InternalError => false,
            _ => true
        };
    }

    private static LogLevel GetLogLevelForError(MetalError error)
    {
        return error switch
        {
            MetalError.DeviceLost => LogLevel.Critical,
            MetalError.OutOfMemory or MetalError.DeviceUnavailable => LogLevel.Error,
            MetalError.ResourceLimitExceeded or MetalError.CompilationError => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }

    private static string GetPressureSeverity(MemoryPressureLevel level)
    {
        return level switch
        {
            MemoryPressureLevel.Critical => "critical",
            MemoryPressureLevel.High => "high",
            MemoryPressureLevel.Medium => "medium",
            _ => "low"
        };
    }

    private static string GetUtilizationCategory(double percentage)
    {
        return percentage switch
        {
            > 95 => "critical",
            > 85 => "high",
            > 70 => "elevated",
            > 50 => "moderate",
            _ => "low"
        };
    }

    private static string GetPerformanceTier(double durationMs, double throughputMBps)
    {
        if (durationMs < 10 && throughputMBps > 1000)
        {
            return "premium";
        }


        if (durationMs < 50 && throughputMBps > 500)
        {
            return "high";
        }


        if (durationMs < 100 && throughputMBps > 100)
        {
            return "standard";
        }


        return "basic";
    }

    private static double CalculateEfficiencyScore(double durationMs, double throughputMBps)
    {
        // Simplified efficiency calculation
        var speedScore = Math.Max(0, 100 - (durationMs / 10));
        var throughputScore = Math.Min(100, throughputMBps / 10);
        return (speedScore + throughputScore) / 2;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Flush any remaining buffered entries
            FlushLogBuffer(null);


            _bufferFlushTimer?.Dispose();

            LogProductionLoggerDisposed(_logger, _activeContexts.Count, _logBuffer.Count);
        }
    }
}