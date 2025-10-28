// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Security;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Handles security event logging with context and audit trail.
/// Provides methods for logging various types of security events.
/// </summary>
public sealed partial class SecurityEventLogger(ILogger<SecurityEventLogger> logger,
    SecurityLoggingConfiguration configuration,
    ConcurrentQueue<SecurityLogEntry> auditQueue,
    SemaphoreSlim logWriteLock,
    ConcurrentDictionary<string, CorrelationContext> correlationContexts)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SecurityLoggingConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ConcurrentQueue<SecurityLogEntry> _auditQueue = auditQueue ?? throw new ArgumentNullException(nameof(auditQueue));
    private readonly SemaphoreSlim _logWriteLock = logWriteLock ?? throw new ArgumentNullException(nameof(logWriteLock));
    private readonly ConcurrentDictionary<string, CorrelationContext> _correlationContexts = correlationContexts ?? throw new ArgumentNullException(nameof(correlationContexts));
    private long _sequenceNumber;

    // LoggerMessage delegates - Event ID range 18500-18515 for SecurityEventLogger
    private static readonly Action<ILogger, string, Exception?> _logSecurityCritical =
        LoggerMessage.Define<string>(
            MsLogLevel.Critical,
            new EventId(18500, nameof(LogSecurityCritical)),
            "[SECURITY-CRITICAL] {Message}");

    private static void LogSecurityCritical(ILogger logger, string message)
        => _logSecurityCritical(logger, message, null);

    private static readonly Action<ILogger, string, Exception?> _logSecurityHigh =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(18501, nameof(LogSecurityHigh)),
            "[SECURITY-HIGH] {Message}");

    private static void LogSecurityHigh(ILogger logger, string message)
        => _logSecurityHigh(logger, message, null);

    private static readonly Action<ILogger, string, Exception?> _logSecurityMedium =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18502, nameof(LogSecurityMedium)),
            "[SECURITY-MEDIUM] {Message}");

    private static void LogSecurityMedium(ILogger logger, string message)
        => _logSecurityMedium(logger, message, null);

    private static readonly Action<ILogger, string, Exception?> _logSecurityLow =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(18503, nameof(LogSecurityLow)),
            "[SECURITY-LOW] {Message}");

    private static void LogSecurityLow(ILogger logger, string message)
        => _logSecurityLow(logger, message, null);

    /// <summary>
    /// Logs a security event with full context and audit trail.
    /// </summary>
    public async Task LogSecurityEventAsync(SecurityEventType eventType, string message,
        SecurityLevel level, string? userId = null, string? resourceId = null,
        IDictionary<string, object>? additionalData = null, string? correlationId = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var entry = CreateSecurityLogEntry(eventType, message, level, userId, resourceId,
            additionalData, correlationId, callerName, sourceFile, lineNumber);

        await _logWriteLock.WaitAsync();
        try
        {
            _auditQueue.Enqueue(entry);

            // Log based on severity
            LogSecurityEventByLevel(entry);
        }
        finally
        {
            _ = _logWriteLock.Release();
        }
    }

    /// <summary>
    /// Logs a security event synchronously.
    /// </summary>
    public void LogSecurityEvent(SecurityEventType eventType, string message,
        SecurityLevel level, string? userId = null, string? resourceId = null,
        IDictionary<string, object>? additionalData = null, string? correlationId = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var entry = CreateSecurityLogEntry(eventType, message, level, userId, resourceId,
            additionalData, correlationId, callerName, sourceFile, lineNumber);

        _auditQueue.Enqueue(entry);
        LogSecurityEventByLevel(entry);
    }

    /// <summary>
    /// Logs a security violation with detailed context.
    /// </summary>
    public async Task LogSecurityViolationAsync(SecurityViolationType violationType,
        string details, string? source = null, string? userId = null,
        IDictionary<string, object>? additionalData = null, string? correlationId = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var enhancedData = new Dictionary<string, object>
        {
            ["ViolationType"] = violationType.ToString(),
            ["Source"] = source ?? "Unknown"
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                enhancedData[kvp.Key] = kvp.Value;
            }
        }

        await LogSecurityEventAsync(SecurityEventType.SecurityViolation, details,
            SecurityLevel.High, userId, source, enhancedData, correlationId,
            callerName, sourceFile, lineNumber);
    }

    /// <summary>
    /// Logs a successful authentication event.
    /// </summary>
    public async Task LogAuthenticationSuccessAsync(string userId, string authenticationMethod,
        string? ipAddress = null, IDictionary<string, object>? additionalData = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var data = new Dictionary<string, object>
        {
            ["AuthenticationMethod"] = authenticationMethod,
            ["IpAddress"] = ipAddress ?? "Unknown"
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        await LogSecurityEventAsync(SecurityEventType.AuthenticationSuccess,
            $"User '{userId}' authenticated successfully using {authenticationMethod}",
            SecurityLevel.Low, userId, null, data, null, callerName, sourceFile, lineNumber);
    }

    /// <summary>
    /// Logs a failed authentication event.
    /// </summary>
    public async Task LogAuthenticationFailureAsync(string userId, string failureReason,
        string? ipAddress = null, IDictionary<string, object>? additionalData = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var data = new Dictionary<string, object>
        {
            ["FailureReason"] = failureReason,
            ["IpAddress"] = ipAddress ?? "Unknown"
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        await LogSecurityEventAsync(SecurityEventType.AuthenticationFailure,
            $"Authentication failed for user '{userId}': {failureReason}",
            SecurityLevel.Medium, userId, null, data, null, callerName, sourceFile, lineNumber);
    }

    /// <summary>
    /// Logs an access control event.
    /// </summary>
    public async Task LogAccessControlAsync(string userId, string resource, string action,
        AccessResult result, string? reason = null, IDictionary<string, object>? additionalData = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var eventType = result.Granted
            ? SecurityEventType.AccessGranted
            : SecurityEventType.AccessDenied;

        var level = result.Granted
            ? SecurityLevel.Low
            : SecurityLevel.Medium;

        var data = new Dictionary<string, object>
        {
            ["Action"] = action,
            ["Result"] = result.Granted ? "Granted" : "Denied",
            ["Reason"] = reason ?? "Not specified"
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        await LogSecurityEventAsync(eventType,
            $"Access {(result.Granted ? "granted" : "denied")} for user '{userId}' to resource '{resource}' for action '{action}'",
            level, userId, resource, data, null, callerName, sourceFile, lineNumber);
    }

    /// <summary>
    /// Logs a data access event with operation details.
    /// </summary>
    public async Task LogDataAccessAsync(string userId, DataOperation operation, string dataType,
        string? recordId = null, bool isSuccessful = true, string? failureReason = null,
        IDictionary<string, object>? additionalData = null,
        [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var eventType = operation switch
        {
            DataOperation.Read => SecurityEventType.DataAccess,
            DataOperation.Create or DataOperation.Update => SecurityEventType.DataModification,
            DataOperation.Delete => SecurityEventType.DataDeletion,
            _ => SecurityEventType.DataAccess
        };

        var level = isSuccessful ? SecurityLevel.Low : SecurityLevel.Medium;
        var message = isSuccessful
            ? $"Data {operation.ToString().ToUpper(CultureInfo.InvariantCulture)} operation successful for user '{userId}' on {dataType}"
            : $"Data {operation.ToString().ToUpper(CultureInfo.InvariantCulture)} operation failed for user '{userId}' on {dataType}: {failureReason}";

        var data = new Dictionary<string, object>
        {
            ["Operation"] = operation.ToString(),
            ["DataType"] = dataType,
            ["IsSuccessful"] = isSuccessful
        };

        if (!string.IsNullOrEmpty(recordId))
        {
            data["RecordId"] = recordId;
        }

        if (!string.IsNullOrEmpty(failureReason))
        {
            data["FailureReason"] = failureReason;
        }

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        await LogSecurityEventAsync(eventType, message, level, userId, recordId, data, null,
            callerName, sourceFile, lineNumber);
    }

    private SecurityLogEntry CreateSecurityLogEntry(SecurityEventType eventType, string message,
        SecurityLevel level, string? userId, string? resourceId,
        IDictionary<string, object>? additionalData, string? correlationId,
        string callerName, string sourceFile, int lineNumber)
    {
        correlationId ??= Guid.NewGuid().ToString();
        var sequenceId = Interlocked.Increment(ref _sequenceNumber);

        var entry = new SecurityLogEntry
        {
            Id = Guid.NewGuid().ToString(),
            SequenceNumber = sequenceId,
            Timestamp = DateTimeOffset.UtcNow,
            EventType = eventType,
            Level = level,
            Message = message,
            UserId = userId,
            ResourceId = resourceId,
            CorrelationId = correlationId,
            CallerName = callerName,
            SourceFile = _configuration.IncludeStackTraces ? sourceFile : string.Empty,
            LineNumber = _configuration.IncludeStackTraces ? lineNumber : 0,
            AdditionalData = additionalData?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? []
        };

        // Update correlation context if enabled
        if (_configuration.EnableCorrelationTracking && !string.IsNullOrEmpty(correlationId))
        {
            _ = _correlationContexts.AddOrUpdate(correlationId,
                new CorrelationContext { StartTime = entry.Timestamp, EventCount = 1 },
                (key, existing) => new CorrelationContext
                {
                    StartTime = existing.StartTime,
                    EventCount = existing.EventCount + 1
                });
        }

        return entry;
    }

    private void LogSecurityEventByLevel(SecurityLogEntry entry)
    {
        var message = FormatLogMessage(entry);

        switch (entry.Level)
        {
            case SecurityLevel.Critical:
                LogSecurityCritical(_logger, message);
                break;
            case SecurityLevel.High:
                LogSecurityHigh(_logger, message);
                break;
            case SecurityLevel.Medium:
                LogSecurityMedium(_logger, message);
                break;
            case SecurityLevel.Low:
                LogSecurityLow(_logger, message);
                break;
        }
    }

    private static string FormatLogMessage(SecurityLogEntry entry)
    {
        var parts = new List<string>
        {
            $"Event: {entry.EventType}",
            $"Message: {entry.Message}",
            $"CorrelationId: {entry.CorrelationId}"
        };

        if (!string.IsNullOrEmpty(entry.UserId))
        {
            parts.Add($"UserId: {entry.UserId}");
        }

        if (!string.IsNullOrEmpty(entry.ResourceId))
        {
            parts.Add($"ResourceId: {entry.ResourceId}");
        }

        return string.Join(" | ", parts);
    }
}