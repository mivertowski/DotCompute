using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Logging;

/// <summary>
/// Production-grade log enricher that adds correlation IDs, contextual data, and security features.
/// Provides automatic sensitive data redaction, performance context injection, and distributed tracing integration.
/// </summary>
public sealed class LogEnricher : IDisposable
{
    private readonly ILogger<LogEnricher> _logger;
    private readonly LogEnricherOptions _options;
    private readonly ConcurrentDictionary<string, object> _contextualData;
    private readonly ThreadLocal<Dictionary<string, object>> _threadLocalContext;
    private readonly Timer _contextCleanupTimer;
    private volatile bool _disposed;

    // Sensitive data patterns for redaction

    private static readonly Regex[] SensitivePatterns =

    {
        new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled), // Email
        new(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled), // Credit card
        new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled), // SSN
        new(@"\bBearer\s+[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]*\b", RegexOptions.Compiled), // JWT
        new(@"\b[A-Za-z0-9]{32,}\b", RegexOptions.Compiled), // API keys (32+ chars)
        new(@"password[""']?\s*[:=]\s*[""']?[^""'\s]+[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"secret[""']?\s*[:=]\s*[""']?[^""'\s]+[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"token[""']?\s*[:=]\s*[""']?[^""'\s]+[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"key[""']?\s*[:=]\s*[""']?[^""'\s]+[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    public LogEnricher(ILogger<LogEnricher> logger, IOptions<LogEnricherOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new LogEnricherOptions();


        _contextualData = new ConcurrentDictionary<string, object>();
        _threadLocalContext = new ThreadLocal<Dictionary<string, object>>(() => []);

        // Initialize system context

        InitializeSystemContext();

        // Start context cleanup timer

        _contextCleanupTimer = new Timer(CleanupExpiredContext, null,

            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Enriches a log entry with correlation IDs, contextual data, and performance metrics.
    /// </summary>
    /// <param name="logEntry">The log entry to enrich</param>
    public void EnrichLogEntry(StructuredLogEntry logEntry)
    {
        ThrowIfDisposed();


        try
        {
            // Add correlation context
            EnrichWithCorrelationContext(logEntry);

            // Add distributed tracing context

            EnrichWithDistributedTracingContext(logEntry);

            // Add system context

            EnrichWithSystemContext(logEntry);

            // Add performance context

            EnrichWithPerformanceContext(logEntry);

            // Add thread context

            EnrichWithThreadContext(logEntry);

            // Add security context

            EnrichWithSecurityContext(logEntry);

            // Add custom contextual data

            EnrichWithContextualData(logEntry);

            // Redact sensitive data if enabled

            if (_options.EnableSensitiveDataRedaction)
            {
                RedactSensitiveData(logEntry);
            }

            // Add metadata

            EnrichWithMetadata(logEntry);
        }
        catch (Exception ex)
        {
            // Never throw from enrichment - log the error
            _logger.LogError(ex, "Failed to enrich log entry");
        }
    }

    /// <summary>
    /// Adds correlation context for the current request/operation.
    /// </summary>
    /// <param name="correlationId">Unique correlation identifier</param>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="userId">Optional user identifier</param>
    /// <param name="sessionId">Optional session identifier</param>
    public void AddCorrelationContext(string correlationId, string? operationName = null,

        string? userId = null, string? sessionId = null)
    {
        ThrowIfDisposed();


        var context = new CorrelationContext
        {
            CorrelationId = correlationId,
            OperationName = operationName,
            UserId = userId,
            SessionId = sessionId,
            Timestamp = DateTimeOffset.UtcNow
        };


        _ = _contextualData.AddOrUpdate($"correlation_{correlationId}", context, (k, v) => context);

        // Also add to thread-local context for immediate access

        if (_threadLocalContext.Value != null)
        {
            _threadLocalContext.Value["CorrelationId"] = correlationId;
            if (!string.IsNullOrEmpty(operationName))
            {
                _threadLocalContext.Value["OperationName"] = operationName;
            }

            if (!string.IsNullOrEmpty(userId))
            {
                _threadLocalContext.Value["UserId"] = userId;
            }

            if (!string.IsNullOrEmpty(sessionId))
            {
                _threadLocalContext.Value["SessionId"] = sessionId;
            }
        }
    }

    /// <summary>
    /// Removes correlation context for a specific correlation ID.
    /// </summary>
    /// <param name="correlationId">Correlation ID to remove</param>
    public void RemoveCorrelationContext(string correlationId)
    {
        ThrowIfDisposed();


        _ = _contextualData.TryRemove($"correlation_{correlationId}", out _);

        // Remove from thread-local context if it matches

        if (_threadLocalContext.Value?.TryGetValue("CorrelationId", out var currentId) == true &&
            currentId?.ToString() == correlationId)
        {
            _ = _threadLocalContext.Value.Remove("CorrelationId");
            _ = _threadLocalContext.Value.Remove("OperationName");
            _ = _threadLocalContext.Value.Remove("UserId");
            _ = _threadLocalContext.Value.Remove("SessionId");
        }
    }

    /// <summary>
    /// Adds custom contextual data that will be included in all log entries.
    /// </summary>
    /// <param name="key">Context key</param>
    /// <param name="value">Context value</param>
    /// <param name="scope">Scope of the context (Global, Thread, or Request)</param>
    public void AddContextualData(string key, object value, ContextScope scope = ContextScope.Global)
    {
        ThrowIfDisposed();


        switch (scope)
        {
            case ContextScope.Global:
                _ = _contextualData.AddOrUpdate(key, value, (k, v) => value);
                break;


            case ContextScope.Thread:
                if (_threadLocalContext.Value != null)
                {
                    _threadLocalContext.Value[key] = value;
                }
                break;


            case ContextScope.Request:
                // Request scope would typically use HttpContext or similar
                // For now, treat as thread-local
                if (_threadLocalContext.Value != null)
                {
                    _threadLocalContext.Value[$"req_{key}"] = value;
                }
                break;
        }
    }

    /// <summary>
    /// Removes contextual data.
    /// </summary>
    /// <param name="key">Context key to remove</param>
    /// <param name="scope">Scope to remove from</param>
    public void RemoveContextualData(string key, ContextScope scope = ContextScope.Global)
    {
        ThrowIfDisposed();


        switch (scope)
        {
            case ContextScope.Global:
                _ = _contextualData.TryRemove(key, out _);
                break;


            case ContextScope.Thread:
                _ = (_threadLocalContext.Value?.Remove(key));
                break;


            case ContextScope.Request:
                _ = (_threadLocalContext.Value?.Remove($"req_{key}"));
                break;
        }
    }

    /// <summary>
    /// Adds device-specific context for hardware operations.
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="deviceType">Type of device</param>
    /// <param name="capabilities">Device capabilities</param>
    public void AddDeviceContext(string deviceId, string deviceType, Dictionary<string, object>? capabilities = null)
    {
        ThrowIfDisposed();


        var deviceContext = new DeviceContext
        {
            DeviceId = deviceId,
            DeviceType = deviceType,
            Capabilities = capabilities ?? [],
            Timestamp = DateTimeOffset.UtcNow
        };


        _ = _contextualData.AddOrUpdate($"device_{deviceId}", deviceContext, (k, v) => deviceContext);
    }

    /// <summary>
    /// Adds kernel execution context for compute operations.
    /// </summary>
    /// <param name="kernelName">Name of the kernel</param>
    /// <param name="compilationInfo">Kernel compilation information</param>
    public void AddKernelContext(string kernelName, KernelCompilationInfo compilationInfo)
    {
        ThrowIfDisposed();


        var kernelContext = new KernelExecutionContext
        {
            KernelName = kernelName,
            CompilationInfo = compilationInfo,
            Timestamp = DateTimeOffset.UtcNow
        };


        if (_threadLocalContext.Value != null)
        {
            _threadLocalContext.Value["CurrentKernel"] = kernelName;
            _threadLocalContext.Value["KernelCompileTime"] = compilationInfo.CompilationTime;
            _threadLocalContext.Value["KernelOptimizationLevel"] = compilationInfo.OptimizationLevel;
        }
    }

    private void InitializeSystemContext()
    {
        try
        {
            // System information
            _contextualData["Environment.MachineName"] = Environment.MachineName;
            _contextualData["Environment.ProcessorCount"] = Environment.ProcessorCount;
            _contextualData["Environment.WorkingSet"] = Environment.WorkingSet;
            _contextualData["Environment.OSVersion"] = Environment.OSVersion.ToString();
            _contextualData["Environment.ProcessId"] = Environment.ProcessId;
            _contextualData["Environment.Is64BitProcess"] = Environment.Is64BitProcess;

            // Runtime information

            _contextualData["Runtime.Framework"] = RuntimeInformation.FrameworkDescription;
            _contextualData["Runtime.Architecture"] = RuntimeInformation.ProcessArchitecture.ToString();
            _contextualData["Runtime.OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString();
            _contextualData["Runtime.OSDescription"] = RuntimeInformation.OSDescription;

            // Application context

            var assembly = typeof(LogEnricher).Assembly;
            _contextualData["Application.Name"] = "DotCompute";
            _contextualData["Application.Version"] = assembly.GetName().Version?.ToString() ?? "Unknown";
            _contextualData["Application.Location"] = assembly.Location;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize some system context information");
        }
    }

    private void EnrichWithCorrelationContext(StructuredLogEntry logEntry)
    {
        // Check for existing correlation ID in log entry
        if (string.IsNullOrEmpty(logEntry.CorrelationId))
        {
            // Try to get from thread-local context
            if (_threadLocalContext.Value?.TryGetValue("CorrelationId", out var correlationId) == true)
            {
                logEntry.CorrelationId = correlationId.ToString();
            }
            else
            {
                // Generate a new correlation ID
                logEntry.CorrelationId = Guid.NewGuid().ToString("N")[..12];
            }
        }

        // Find and add correlation context

        if (_contextualData.TryGetValue($"correlation_{logEntry.CorrelationId}", out var contextObj) &&
            contextObj is CorrelationContext context)
        {
            if (!string.IsNullOrEmpty(context.OperationName))
            {
                logEntry.Properties["Operation"] = context.OperationName;
            }

            if (!string.IsNullOrEmpty(context.UserId))
            {
                logEntry.Properties["UserId"] = context.UserId;
            }

            if (!string.IsNullOrEmpty(context.SessionId))
            {
                logEntry.Properties["SessionId"] = context.SessionId;
            }
        }
    }

    private static void EnrichWithDistributedTracingContext(StructuredLogEntry logEntry)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEntry.TraceId = activity.TraceId.ToString();
            logEntry.SpanId = activity.SpanId.ToString();


            if (activity.Parent != null)
            {
                logEntry.Properties["ParentSpanId"] = activity.Parent.SpanId.ToString();
            }

            // Add activity tags

            foreach (var tag in activity.Tags)
            {
                if (!logEntry.Properties.ContainsKey($"trace.{tag.Key}"))
                {
                    logEntry.Properties[$"trace.{tag.Key}"] = tag.Value ?? string.Empty;
                }
            }

            // Add baggage

            foreach (var baggage in activity.Baggage)
            {
                if (!logEntry.Properties.ContainsKey($"baggage.{baggage.Key}"))
                {
                    logEntry.Properties[$"baggage.{baggage.Key}"] = baggage.Value ?? string.Empty;
                }
            }
        }
    }

    private void EnrichWithSystemContext(StructuredLogEntry logEntry)
    {
        // Add selected system context (avoid overwhelming the log)
        var systemKeys = new[] { "Environment.MachineName", "Environment.ProcessId", "Runtime.Framework" };


        foreach (var key in systemKeys)
        {
            if (_contextualData.TryGetValue(key, out var value))
            {
                logEntry.Properties[key] = value;
            }
        }
    }

    private void EnrichWithPerformanceContext(StructuredLogEntry logEntry)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Memory information

            var memoryBefore = GC.GetTotalMemory(false);
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);


            logEntry.Properties["Performance.MemoryUsage"] = memoryBefore;
            logEntry.Properties["Performance.Gen0Collections"] = gen0;
            logEntry.Properties["Performance.Gen1Collections"] = gen1;
            logEntry.Properties["Performance.Gen2Collections"] = gen2;

            // Thread information

            logEntry.Properties["Performance.ThreadId"] = Thread.CurrentThread.ManagedThreadId;
            logEntry.Properties["Performance.IsThreadPoolThread"] = Thread.CurrentThread.IsThreadPoolThread;


            stopwatch.Stop();
            logEntry.Properties["Performance.EnrichmentTimeMs"] = stopwatch.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to add performance context");
        }
    }

    private void EnrichWithThreadContext(StructuredLogEntry logEntry)
    {
        if (_threadLocalContext.Value != null)
        {
            foreach (var item in _threadLocalContext.Value)
            {
                if (!logEntry.Properties.ContainsKey(item.Key))
                {
                    logEntry.Properties[item.Key] = item.Value;
                }
            }
        }
    }

    private void EnrichWithSecurityContext(StructuredLogEntry logEntry)
    {
        try
        {
            // Add security-relevant context
            logEntry.Properties["Security.IsElevated"] = Environment.IsPrivilegedProcess;

            // Add user context if available (in a secure way)

            if (!string.IsNullOrEmpty(Environment.UserName))
            {
                var hashedUser = HashString(Environment.UserName);
                logEntry.Properties["Security.UserHash"] = hashedUser[..8]; // Only first 8 chars for privacy
            }
        }
        catch (SecurityException)
        {
            // Security context not available - this is fine
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to add security context");
        }
    }

    private void EnrichWithContextualData(StructuredLogEntry logEntry)
    {
        // Add global contextual data
        foreach (var item in _contextualData)
        {
            // Skip internal context items
            if (item.Key.StartsWith("correlation_") || item.Key.StartsWith("device_"))
            {
                continue;
            }


            if (!logEntry.Properties.ContainsKey(item.Key))
            {
                logEntry.Properties[item.Key] = item.Value;
            }
        }
    }

    private static void EnrichWithMetadata(StructuredLogEntry logEntry)
    {
        // Add enrichment metadata
        logEntry.Properties["_enriched"] = true;
        logEntry.Properties["_enrichedAt"] = DateTimeOffset.UtcNow;
        logEntry.Properties["_enricherVersion"] = typeof(LogEnricher).Assembly.GetName().Version?.ToString() ?? "Unknown";

        // Add log size estimation for monitoring

        var estimatedSize = EstimateLogEntrySize(logEntry);
        logEntry.Properties["_estimatedSizeBytes"] = estimatedSize;
    }

    private static void RedactSensitiveData(StructuredLogEntry logEntry)
    {
        // Redact in formatted message
        logEntry.FormattedMessage = RedactString(logEntry.FormattedMessage);

        // Redact in message

        logEntry.Message = RedactString(logEntry.Message);

        // Redact in properties

        var keysToRedact = new List<string>();
        var redactedProperties = new Dictionary<string, object>();


        foreach (var prop in logEntry.Properties)
        {
            if (prop.Value is string stringValue)
            {
                var redacted = RedactString(stringValue);
                if (redacted != stringValue)
                {
                    redactedProperties[prop.Key] = redacted;
                }
            }
            else if (IsSensitivePropertyName(prop.Key))
            {
                redactedProperties[prop.Key] = "[REDACTED]";
            }
        }


        foreach (var redacted in redactedProperties)
        {
            logEntry.Properties[redacted.Key] = redacted.Value;
        }

        // Redact exception details if present

        if (logEntry.Exception != null)
        {
            // Create a sanitized exception message
            logEntry.Properties["Exception.Type"] = logEntry.Exception.GetType().Name;
            logEntry.Properties["Exception.Message"] = RedactString(logEntry.Exception.Message);
            // Don't include full stack trace in production logs for security
        }
    }

    private static string RedactString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }


        var result = input;
        foreach (var pattern in SensitivePatterns)
        {
            result = pattern.Replace(result, "[REDACTED]");
        }


        return result;
    }

    private static bool IsSensitivePropertyName(string propertyName)
    {
        var lowerName = propertyName.ToLowerInvariant();
        return lowerName.Contains("password") ||
               lowerName.Contains("secret") ||
               lowerName.Contains("token") ||
               lowerName.Contains("key") ||
               lowerName.Contains("credential") ||
               lowerName.Contains("auth");
    }

    private static string HashString(string input)
        // Simple hash for user identification (not cryptographically secure)

        => input.GetHashCode().ToString("X8");

    private static int EstimateLogEntrySize(StructuredLogEntry logEntry)
    {
        var size = logEntry.FormattedMessage?.Length ?? 0;
        size += logEntry.Message?.Length ?? 0;
        size += logEntry.Category?.Length ?? 0;
        size += logEntry.CorrelationId?.Length ?? 0;
        size += logEntry.TraceId?.Length ?? 0;
        size += logEntry.SpanId?.Length ?? 0;


        foreach (var prop in logEntry.Properties)
        {
            size += prop.Key.Length;
            size += prop.Value?.ToString()?.Length ?? 0;
        }


        return size;
    }

    private void CleanupExpiredContext(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var expiredThreshold = DateTimeOffset.UtcNow - TimeSpan.FromHours(_options.ContextRetentionHours);
            var keysToRemove = new List<string>();


            foreach (var item in _contextualData)
            {
                if (item.Value is CorrelationContext context && context.Timestamp < expiredThreshold)
                {
                    keysToRemove.Add(item.Key);
                }
                else if (item.Value is DeviceContext deviceContext && deviceContext.Timestamp < expiredThreshold)
                {
                    keysToRemove.Add(item.Key);
                }
            }


            foreach (var key in keysToRemove)
            {
                _ = _contextualData.TryRemove(key, out _);
            }


            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired context entries", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired context");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(LogEnricher));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        _contextCleanupTimer?.Dispose();
        _threadLocalContext?.Dispose();
    }
}

// Supporting data structures and enums
public sealed class LogEnricherOptions
{
    public bool EnableSensitiveDataRedaction { get; set; } = true;
    public int ContextRetentionHours { get; set; } = 24;
    public bool EnablePerformanceContext { get; set; } = true;
    public bool EnableSecurityContext { get; set; } = true;
    public List<string> AdditionalSensitivePatterns { get; set; } = [];
}

public enum ContextScope
{
    Global,
    Thread,
    Request
}

public sealed class CorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
    public string? OperationName { get; set; }
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class DeviceContext
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public Dictionary<string, object> Capabilities { get; set; } = [];
    public DateTimeOffset Timestamp { get; set; }
}



public sealed class KernelCompilationInfo
{
    public TimeSpan CompilationTime { get; set; }
    public string OptimizationLevel { get; set; } = string.Empty;
    public Dictionary<string, object> CompilerFlags { get; set; } = [];
}
