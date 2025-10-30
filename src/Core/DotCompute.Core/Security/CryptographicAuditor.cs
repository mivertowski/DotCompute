// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using DotCompute.Abstractions.Security;
using DotCompute.Core.Aot;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides comprehensive audit logging and security event tracking for cryptographic operations.
/// Maintains detailed security logs for compliance and forensic analysis.
/// </summary>
internal sealed partial class CryptographicAuditor : IDisposable
{
    private readonly ILogger<CryptographicAuditor> _logger;
    private readonly CryptographicConfiguration _configuration;
    private readonly ConcurrentQueue<SecurityEvent> _auditQueue;
    private readonly SemaphoreSlim _flushLock;
    private readonly Timer _flushTimer;
    private readonly string _auditLogPath;
    private bool _disposed;

    // Security event categories for classification
    private static readonly Dictionary<string, SecurityEventCategory> EventCategories = new()
    {
        ["KEY_GENERATION"] = SecurityEventCategory.KeyManagement,
        ["KEY_ROTATION"] = SecurityEventCategory.KeyManagement,
        ["KEY_DELETION"] = SecurityEventCategory.KeyManagement,
        ["ENCRYPTION"] = SecurityEventCategory.CryptographicOperation,
        ["DECRYPTION"] = SecurityEventCategory.CryptographicOperation,
        ["SIGNING"] = SecurityEventCategory.CryptographicOperation,
        ["VERIFICATION"] = SecurityEventCategory.CryptographicOperation,
        ["ALGORITHM_VALIDATION"] = SecurityEventCategory.Validation,
        ["CONFIGURATION_CHANGE"] = SecurityEventCategory.Configuration,
        ["SECURITY_VIOLATION"] = SecurityEventCategory.SecurityIncident,
        ["AUTHENTICATION_FAILURE"] = SecurityEventCategory.SecurityIncident,
        ["UNAUTHORIZED_ACCESS"] = SecurityEventCategory.SecurityIncident
    };
    /// <summary>
    /// Initializes a new instance of the CryptographicAuditor class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="auditLogPath">The audit log path.</param>

    public CryptographicAuditor(
        ILogger<CryptographicAuditor> logger,
        CryptographicConfiguration configuration,
        string? auditLogPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _auditQueue = new ConcurrentQueue<SecurityEvent>();
        _flushLock = new SemaphoreSlim(1, 1);

        _auditLogPath = auditLogPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotCompute", "Security", "audit.log");

        // Ensure audit log directory exists
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_auditLogPath)!);

        // Set up periodic audit log flushing
        _flushTimer = new Timer(FlushAuditLogs, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        LogCryptographicAuditorInitialized(_logger, _auditLogPath);
    }

    [LoggerMessage(EventId = 8000, Level = LogLevel.Information, Message = "CryptographicAuditor initialized with audit log: {AuditLogPath}")]
    private static partial void LogCryptographicAuditorInitialized(ILogger logger, string auditLogPath);

    /// <summary>
    /// Logs a security event for audit purposes.
    /// </summary>
    public async Task LogSecurityEventAsync(
        string eventType,
        SecurityEventSeverity severity,
        string description,
        object? additionalData = null,
        string? userId = null,
        string? sessionId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        try
        {
            var securityEvent = new SecurityEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                Severity = severity,
                Description = description,
                Timestamp = DateTimeOffset.UtcNow,
                UserId = userId,
                SessionId = sessionId,
                Category = EventCategories.GetValueOrDefault(eventType.ToUpperInvariant(), SecurityEventCategory.General),
                AdditionalData = additionalData != null ? JsonSerializer.Serialize(additionalData, DotComputeCompactJsonContext.Default.Object) : null,
                SourceAddress = GetSourceAddress(),
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId
            };

            _auditQueue.Enqueue(securityEvent);

            // Log immediately for high severity events
            if (severity >= SecurityEventSeverity.High)
            {
                await FlushAuditLogsAsync();
                LogHighSeveritySecurityEvent(_logger, eventType, description);
            }

            LogSecurityEventRecorded(_logger, eventType, severity);
        }
        catch (Exception ex)
        {
            LogFailedToLogSecurityEvent(_logger, eventType, ex);
        }
    }

    [LoggerMessage(EventId = 8001, Level = LogLevel.Debug, Message = "Security event logged: {EventType} - {Severity}")]
    private static partial void LogSecurityEventRecorded(ILogger logger, string eventType, SecurityEventSeverity severity);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Error, Message = "Failed to log security event: {EventType}")]
    private static partial void LogFailedToLogSecurityEvent(ILogger logger, string eventType, Exception ex);

    /// <summary>
    /// Logs a cryptographic operation for audit purposes.
    /// </summary>
    public async Task LogCryptographicOperationAsync(
        string operation,
        string algorithm,
        int keySize,
        bool success,
        string? keyIdentifier = null,
        TimeSpan? executionTime = null,
        string? errorMessage = null)
    {
        var additionalData = new
        {
            Algorithm = algorithm,
            KeySize = keySize,
            Success = success,
            KeyIdentifier = keyIdentifier,
            ExecutionTimeMs = executionTime?.TotalMilliseconds,
            ErrorMessage = errorMessage
        };

        var severity = success ? SecurityEventSeverity.Low : SecurityEventSeverity.Medium;
        var description = success
            ? $"Cryptographic operation completed: {operation} using {algorithm}"
            : $"Cryptographic operation failed: {operation} using {algorithm} - {errorMessage}";

        await LogSecurityEventAsync(operation.ToUpperInvariant(), severity, description, additionalData);
    }

    /// <summary>
    /// Logs a key management operation for audit purposes.
    /// </summary>
    public async Task LogKeyManagementOperationAsync(
        string operation,
        string keyIdentifier,
        KeyType keyType,
        int keySize,
        bool success,
        string? errorMessage = null,
        string? userId = null)
    {
        var additionalData = new
        {
            KeyIdentifier = keyIdentifier,
            KeyType = keyType.ToString(),
            KeySize = keySize,
            Success = success,
            ErrorMessage = errorMessage
        };

        var severity = operation.ToUpperInvariant() switch
        {
            "KEY_DELETION" => SecurityEventSeverity.High,
            "KEY_ROTATION" => SecurityEventSeverity.Medium,
            "KEY_GENERATION" => SecurityEventSeverity.Medium,
            _ => SecurityEventSeverity.Low
        };

        var description = success
            ? $"Key management operation completed: {operation} for key {keyIdentifier}"
            : $"Key management operation failed: {operation} for key {keyIdentifier} - {errorMessage}";

        await LogSecurityEventAsync(operation.ToUpperInvariant(), severity, description, additionalData, userId);
    }

    /// <summary>
    /// Logs a security violation or incident.
    /// </summary>
    public async Task LogSecurityViolationAsync(
        string violationType,
        string description,
        object? evidence = null,
        string? userId = null,
        string? sessionId = null)
    {
        var additionalData = new
        {
            ViolationType = violationType,
            Evidence = evidence,
            DetectionTime = DateTimeOffset.UtcNow,
            SystemInfo = new
            {
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                OSVersion = Environment.OSVersion.ToString()
            }
        };

        await LogSecurityEventAsync("SECURITY_VIOLATION", SecurityEventSeverity.Critical,
            $"Security violation detected: {violationType} - {description}", additionalData, userId, sessionId);

        // Immediately flush critical security events
        await FlushAuditLogsAsync();

        LogSecurityViolationDetected(_logger, violationType, description);
    }

    /// <summary>
    /// Generates a security audit report for a specified time period.
    /// </summary>
    public async Task<SecurityAuditReport> GenerateAuditReportAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        SecurityEventSeverity? minimumSeverity = null,
        string? eventTypeFilter = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogGeneratingSecurityAuditReport(_logger, startTime, endTime);

        try
        {
            // First, ensure all pending events are written to log
            await FlushAuditLogsAsync();

            // Read and parse audit log entries
            var logEntries = await ReadAuditLogEntriesAsync(startTime, endTime);

            // Apply filters
            var filteredEntries = logEntries.AsEnumerable();

            if (minimumSeverity.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.Severity >= minimumSeverity.Value);
            }

            if (!string.IsNullOrEmpty(eventTypeFilter))
            {
                filteredEntries = filteredEntries.Where(e => e.EventType.Contains(eventTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            var events = filteredEntries.ToList();

            // Generate report
            var report = new SecurityAuditReport
            {
                ReportId = Guid.NewGuid().ToString(),
                GenerationTime = DateTimeOffset.UtcNow,
                StartTime = startTime,
                EndTime = endTime,
                TotalEvents = events.Count,
                EventsBySeverity = events.GroupBy(e => e.Severity)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EventsByCategory = events.GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EventsByType = events.GroupBy(e => e.EventType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                SecurityIncidents = [.. events.Where(e => e.Category == SecurityEventCategory.SecurityIncident)],
                RecommendationsGenerated = GenerateSecurityRecommendations(events),
                // Calculate security metrics
                SecurityMetrics = CalculateSecurityMetrics(events)
            };

            LogSecurityAuditReportGenerated(_logger, report.TotalEvents);
            return report;
        }
        catch (Exception ex)
        {
            LogFailedToGenerateSecurityAuditReport(_logger, ex);
            throw;
        }
    }

    [LoggerMessage(EventId = 8003, Level = LogLevel.Information, Message = "Generating security audit report from {StartTime} to {EndTime}")]
    private static partial void LogGeneratingSecurityAuditReport(ILogger logger, DateTimeOffset startTime, DateTimeOffset endTime);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Information, Message = "Security audit report generated: {TotalEvents} events analyzed")]
    private static partial void LogSecurityAuditReportGenerated(ILogger logger, int totalEvents);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Error, Message = "Failed to generate security audit report")]
    private static partial void LogFailedToGenerateSecurityAuditReport(ILogger logger, Exception ex);

    /// <summary>
    /// Exports audit logs in the specified format.
    /// </summary>
    public async Task<string> ExportAuditLogsAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        AuditExportFormat format,
        string? outputPath = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var events = await ReadAuditLogEntriesAsync(startTime, endTime);
        var exportPath = outputPath ?? Path.Combine(Path.GetDirectoryName(_auditLogPath)!,
            $"audit_export_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.{format.ToString().ToUpper(CultureInfo.InvariantCulture)}");

        var exportContent = format switch
        {
            AuditExportFormat.Json => JsonSerializer.Serialize(events, DotComputeJsonContext.Default.ListSecurityEvent),
            AuditExportFormat.Csv => ConvertToCsv(events),
            AuditExportFormat.Xml => ConvertToXml(events),
            _ => throw new NotSupportedException($"Export format not supported: {format}")
        };

        await File.WriteAllTextAsync(exportPath, exportContent);

        LogAuditLogsExported(_logger, exportPath);
        return exportPath;
    }

    [LoggerMessage(EventId = 8006, Level = LogLevel.Information, Message = "Audit logs exported to: {ExportPath}")]
    private static partial void LogAuditLogsExported(ILogger logger, string exportPath);

    /// <summary>
    /// Searches audit logs for specific patterns or events.
    /// </summary>
    public async Task<List<SecurityEvent>> SearchAuditLogsAsync(
        string searchTerm,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int maxResults = 1000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        var searchStartTime = startTime ?? DateTimeOffset.UtcNow.AddDays(-30);
        var searchEndTime = endTime ?? DateTimeOffset.UtcNow;

        var events = await ReadAuditLogEntriesAsync(searchStartTime, searchEndTime);

        var results = events
            .Where(e => e.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                       e.EventType.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                       (e.AdditionalData?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true))
            .Take(maxResults)
            .ToList();

        LogAuditLogSearchCompleted(_logger, results.Count, searchTerm);
        return results;
    }

    [LoggerMessage(EventId = 8007, Level = LogLevel.Information, Message = "Audit log search completed: {ResultCount} results for '{SearchTerm}'")]
    private static partial void LogAuditLogSearchCompleted(ILogger logger, int resultCount, string searchTerm);

    // Private implementation methods

    private async Task FlushAuditLogsAsync()
    {
        await _flushLock.WaitAsync();
        try
        {
            var eventsToFlush = new List<SecurityEvent>();

            // Dequeue all pending events
            while (_auditQueue.TryDequeue(out var securityEvent))
            {
                eventsToFlush.Add(securityEvent);
            }

            if (eventsToFlush.Count == 0)
            {
                return;
            }

            // Write events to audit log file

            var logEntries = eventsToFlush.Select(FormatLogEntry);
            await File.AppendAllLinesAsync(_auditLogPath, logEntries);

            LogFlushedAuditLogEntries(_logger, eventsToFlush.Count);
        }
        catch (Exception ex)
        {
            LogFailedToFlushAuditLogs(_logger, ex);
        }
        finally
        {
            _ = _flushLock.Release();
        }
    }

    private void FlushAuditLogs(object? state)
    {
        try
        {
            _ = Task.Run(FlushAuditLogsAsync);
        }
        catch (Exception ex)
        {
            LogErrorDuringScheduledAuditLogFlush(_logger, ex);
        }
    }

    [LoggerMessage(EventId = 8008, Level = LogLevel.Debug, Message = "Flushed {EventCount} audit log entries")]
    private static partial void LogFlushedAuditLogEntries(ILogger logger, int eventCount);

    [LoggerMessage(EventId = 8009, Level = LogLevel.Error, Message = "Failed to flush audit logs")]
    private static partial void LogFailedToFlushAuditLogs(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 8010, Level = LogLevel.Error, Message = "Error during scheduled audit log flush")]
    private static partial void LogErrorDuringScheduledAuditLogFlush(ILogger logger, Exception ex);

    private string FormatLogEntry(SecurityEvent securityEvent)
        // For compact log format, serialize the SecurityEvent directly
        => JsonSerializer.Serialize(securityEvent, DotComputeCompactJsonContext.Default.SecurityEvent);

    private async Task<List<SecurityEvent>> ReadAuditLogEntriesAsync(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var events = new List<SecurityEvent>();

        if (!File.Exists(_auditLogPath))
        {
            return events;
        }

        var lines = await File.ReadAllLinesAsync(_auditLogPath);

        foreach (var line in lines)
        {
            try
            {
                var eventData = JsonSerializer.Deserialize(line, DotComputeCompactJsonContext.Default.SecurityEvent);
                if (eventData != null && eventData.Timestamp >= startTime && eventData.Timestamp <= endTime)
                {
                    events.Add(eventData);
                }
            }
            catch (JsonException ex)
            {
                LogFailedToParseAuditLogEntry(_logger, ex.Message);
            }
        }

        return [.. events.OrderBy(e => e.Timestamp)];
    }

    private static List<string> GenerateSecurityRecommendations(IReadOnlyList<SecurityEvent> events)
    {
        var recommendations = new List<string>();

        var securityIncidents = events.Where(e => e.Category == SecurityEventCategory.SecurityIncident).Count();
        if (securityIncidents > 0)
        {
            recommendations.Add($"Review {securityIncidents} security incidents and implement preventive measures");
        }

        var failedOperations = events.Where(e => e.AdditionalData?.Contains("\"Success\":false", StringComparison.Ordinal) == true).Count();
        if (failedOperations > events.Count * 0.1) // More than 10% failure rate
        {
            recommendations.Add("High failure rate detected - review system configuration and error handling");
        }

        var keyDeletions = events.Where(e => e.EventType == "KEY_DELETION").Count();
        if (keyDeletions > 0)
        {
            recommendations.Add("Key deletion events detected - ensure proper key lifecycle management");
        }

        return recommendations;
    }

    private static SecurityMetrics CalculateSecurityMetrics(IReadOnlyList<SecurityEvent> events)
    {
        return new SecurityMetrics
        {
            TotalOperations = events.Count,
            SuccessfulOperations = events.Count(e => e.AdditionalData?.Contains("\"Success\":true", StringComparison.Ordinal) == true),
            FailedOperations = events.Count(e => e.AdditionalData?.Contains("\"Success\":false", StringComparison.Ordinal) == true),
            SecurityIncidents = events.Count(e => e.Category == SecurityEventCategory.SecurityIncident),
            KeyManagementOperations = events.Count(e => e.Category == SecurityEventCategory.KeyManagement),
            CryptographicOperations = events.Count(e => e.Category == SecurityEventCategory.CryptographicOperation),
            AverageResponseTime = CalculateAverageResponseTime(events),
            SecurityScore = CalculateSecurityScore(events)
        };
    }

    private static double CalculateAverageResponseTime(IReadOnlyList<SecurityEvent> events)
    {
        var operationsWithTiming = events.Where(e => e.AdditionalData?.Contains("ExecutionTimeMs", StringComparison.Ordinal) == true).ToList();
        if (operationsWithTiming.Count == 0)
        {
            return 0;
        }

        // This is a simplified calculation - in practice, you'd parse the JSON to extract execution times

        return 100.0; // Placeholder
    }

    private static double CalculateSecurityScore(IReadOnlyList<SecurityEvent> events)
    {
        if (events.Count == 0)
        {
            return 100.0;
        }


        var criticalEvents = events.Count(e => e.Severity == SecurityEventSeverity.Critical);
        var highEvents = events.Count(e => e.Severity == SecurityEventSeverity.High);
        var mediumEvents = events.Count(e => e.Severity == SecurityEventSeverity.Medium);

        // Simple scoring: start at 100, subtract points for severity
        var score = 100.0 - (criticalEvents * 10) - (highEvents * 5) - (mediumEvents * 2);
        return Math.Max(0, Math.Min(100, score));
    }

    private static string ConvertToCsv(IReadOnlyList<SecurityEvent> events)
    {
        var csv = new StringBuilder();
        _ = csv.AppendLine("EventId,Timestamp,EventType,Severity,Category,Description,UserId,SessionId");

        foreach (var evt in events)
        {
            _ = csv.AppendLine(CultureInfo.InvariantCulture, $"{evt.EventId},{evt.Timestamp:O},{evt.EventType},{evt.Severity},{evt.Category}," +
                          $"\"{evt.Description.Replace("\"", "\"\"", StringComparison.Ordinal)}\",{evt.UserId},{evt.SessionId}");
        }

        return csv.ToString();
    }

    private static string ConvertToXml(IReadOnlyList<SecurityEvent> events)
    {
        // Simplified XML conversion - in practice, use XmlDocument or XElement
        var xml = new StringBuilder();
        _ = xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        _ = xml.AppendLine("<SecurityEvents>");

        foreach (var evt in events)
        {
            _ = xml.AppendLine(CultureInfo.InvariantCulture, $"  <Event id=\"{evt.EventId}\" timestamp=\"{evt.Timestamp:O}\">");
            _ = xml.AppendLine(CultureInfo.InvariantCulture, $"    <Type>{evt.EventType}</Type>");
            _ = xml.AppendLine(CultureInfo.InvariantCulture, $"    <Severity>{evt.Severity}</Severity>");
            _ = xml.AppendLine(CultureInfo.InvariantCulture, $"    <Category>{evt.Category}</Category>");
            _ = xml.AppendLine(CultureInfo.InvariantCulture, $"    <Description>{SecurityElement.Escape(evt.Description)}</Description>");
            _ = xml.AppendLine("  </Event>");
        }

        _ = xml.AppendLine("</SecurityEvents>");
        return xml.ToString();
    }

    private static string? GetSourceAddress()
        // In a real implementation, this would extract the source IP address

        => "localhost";
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            // Flush any remaining audit logs
            try
            {
                // VSTHRD002: Synchronous wait is necessary here because IDisposable.Dispose() cannot be async.
                // Using a timeout to prevent indefinite blocking during disposal.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                _ = FlushAuditLogsAsync().Wait(TimeSpan.FromSeconds(5));
#pragma warning restore VSTHRD002
            }
            catch (Exception ex)
            {
                LogErrorFlushingAuditLogsDuringDisposal(_logger, ex);
            }

            _flushTimer?.Dispose();
            _flushLock?.Dispose();
            _disposed = true;
        }
    }

    [LoggerMessage(EventId = 8011, Level = LogLevel.Error, Message = "Error flushing audit logs during disposal")]
    private static partial void LogErrorFlushingAuditLogsDuringDisposal(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 8012, Level = LogLevel.Warning, Message = "High severity security event: {EventType} - {Description}")]
    private static partial void LogHighSeveritySecurityEvent(ILogger logger, string eventType, string description);

    [LoggerMessage(EventId = 8013, Level = LogLevel.Error, Message = "Security violation detected: {ViolationType} - {Description}")]
    private static partial void LogSecurityViolationDetected(ILogger logger, string violationType, string description);

    [LoggerMessage(EventId = 8014, Level = LogLevel.Warning, Message = "Failed to parse audit log entry: {Error}")]
    private static partial void LogFailedToParseAuditLogEntry(ILogger logger, string error);
}
/// <summary>
/// An security event severity enumeration.
/// </summary>

// Supporting classes and enums
public enum SecurityEventSeverity
{
    /// <summary>No event or unknown severity level.</summary>
    None = 0,
    /// <summary>Low severity level.</summary>
    Low = 1,
    /// <summary>Medium severity level.</summary>
    Medium = 2,
    /// <summary>High severity level.</summary>
    High = 3,
    /// <summary>Critical severity level.</summary>
    Critical = 4
}
/// <summary>
/// An security event category enumeration.
/// </summary>

public enum SecurityEventCategory
{
    /// <summary>General category for uncategorized events.</summary>
    General,
    /// <summary>Key management operations category.</summary>
    KeyManagement,
    /// <summary>Cryptographic operation category.</summary>
    CryptographicOperation,
    /// <summary>Validation operations category.</summary>
    Validation,
    /// <summary>Configuration changes category.</summary>
    Configuration,
    /// <summary>Security incident category.</summary>
    SecurityIncident
}
/// <summary>
/// An audit export format enumeration.
/// </summary>

public enum AuditExportFormat
{
    /// <summary>JavaScript Object Notation format.</summary>
    Json,
    /// <summary>Comma-Separated Values format.</summary>
    Csv,
    /// <summary>Extensible Markup Language format.</summary>
    Xml
}
/// <summary>
/// A class that represents security event.
/// </summary>

public class SecurityEvent
{
    /// <summary>
    /// Gets or sets the event identifier.
    /// </summary>
    /// <value>The event id.</value>
    public required string EventId { get; set; }
    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    /// <value>The event type.</value>
    public required string EventType { get; set; }
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public SecurityEventSeverity Severity { get; set; }
    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    /// <value>The category.</value>
    public SecurityEventCategory Category { get; set; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public required string Description { get; set; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    /// <value>The user id.</value>
    public string? UserId { get; set; }
    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    /// <value>The session id.</value>
    public string? SessionId { get; set; }
    /// <summary>
    /// Gets or sets the source address.
    /// </summary>
    /// <value>The source address.</value>
    public string? SourceAddress { get; set; }
    /// <summary>
    /// Gets or sets the process identifier.
    /// </summary>
    /// <value>The process id.</value>
    public int ProcessId { get; set; }
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    /// <value>The thread id.</value>
    public int ThreadId { get; set; }
    /// <summary>
    /// Gets or sets the additional data.
    /// </summary>
    /// <value>The additional data.</value>
    public string? AdditionalData { get; set; }
}
/// <summary>
/// A class that represents security audit report.
/// </summary>

public class SecurityAuditReport
{
    /// <summary>
    /// Gets or sets the report identifier.
    /// </summary>
    /// <value>The report id.</value>
    public required string ReportId { get; set; }
    /// <summary>
    /// Gets or sets the generation time.
    /// </summary>
    /// <value>The generation time.</value>
    public DateTimeOffset GenerationTime { get; set; }
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset EndTime { get; set; }
    /// <summary>
    /// Gets or sets the total events.
    /// </summary>
    /// <value>The total events.</value>
    public int TotalEvents { get; set; }
    /// <summary>
    /// Gets or sets the events by severity.
    /// </summary>
    /// <value>The events by severity.</value>
    public Dictionary<SecurityEventSeverity, int> EventsBySeverity { get; init; } = [];
    /// <summary>
    /// Gets or sets the events by category.
    /// </summary>
    /// <value>The events by category.</value>
    public Dictionary<SecurityEventCategory, int> EventsByCategory { get; init; } = [];
    /// <summary>
    /// Gets or sets the events by type.
    /// </summary>
    /// <value>The events by type.</value>
    public Dictionary<string, int> EventsByType { get; init; } = [];
    /// <summary>
    /// Gets or sets the security incidents.
    /// </summary>
    /// <value>The security incidents.</value>
    public IList<SecurityEvent> SecurityIncidents { get; init; } = [];
    /// <summary>
    /// Gets or sets the recommendations generated.
    /// </summary>
    /// <value>The recommendations generated.</value>
    public IList<string> RecommendationsGenerated { get; init; } = [];
    /// <summary>
    /// Gets or sets the security metrics.
    /// </summary>
    /// <value>The security metrics.</value>
    public SecurityMetrics? SecurityMetrics { get; set; }
}
/// <summary>
/// A class that represents security metrics.
/// </summary>

public class SecurityMetrics
{
    /// <summary>
    /// Gets or sets the total operations.
    /// </summary>
    /// <value>The total operations.</value>
    public int TotalOperations { get; set; }
    /// <summary>
    /// Gets or sets the successful operations.
    /// </summary>
    /// <value>The successful operations.</value>
    public int SuccessfulOperations { get; set; }
    /// <summary>
    /// Gets or sets the failed operations.
    /// </summary>
    /// <value>The failed operations.</value>
    public int FailedOperations { get; set; }
    /// <summary>
    /// Gets or sets the security incidents.
    /// </summary>
    /// <value>The security incidents.</value>
    public int SecurityIncidents { get; set; }
    /// <summary>
    /// Gets or sets the key management operations.
    /// </summary>
    /// <value>The key management operations.</value>
    public int KeyManagementOperations { get; set; }
    /// <summary>
    /// Gets or sets the cryptographic operations.
    /// </summary>
    /// <value>The cryptographic operations.</value>
    public int CryptographicOperations { get; set; }
    /// <summary>
    /// Gets or sets the average response time.
    /// </summary>
    /// <value>The average response time.</value>
    public double AverageResponseTime { get; set; }
    /// <summary>
    /// Gets or sets the security score.
    /// </summary>
    /// <value>The security score.</value>
    public double SecurityScore { get; set; }

    // Event counting properties
    /// <summary>Gets or sets the total number of security events recorded.</summary>
    public long TotalEventCount { get; set; }
    /// <summary>Gets or sets the count of successful authentication attempts.</summary>
    public long AuthenticationSuccessCount { get; set; }
    /// <summary>Gets or sets the count of failed authentication attempts.</summary>
    public long AuthenticationFailureCount { get; set; }
    /// <summary>Gets or sets the count of access granted events.</summary>
    public long AccessGrantedCount { get; set; }
    /// <summary>Gets or sets the count of access denied events.</summary>
    public long AccessDeniedCount { get; set; }
    /// <summary>Gets or sets the count of security violation events.</summary>
    public long SecurityViolationCount { get; set; }
    /// <summary>Gets or sets the count of data access events.</summary>
    public long DataAccessCount { get; set; }
    /// <summary>Gets or sets the count of data modification events.</summary>
    public long DataModificationCount { get; set; }
    /// <summary>Gets or sets the count of data deletion events.</summary>
    public long DataDeletionCount { get; set; }
    /// <summary>Gets or sets the count of critical severity events.</summary>
    public long CriticalEventCount { get; set; }
    /// <summary>Gets or sets the count of high severity events.</summary>
    public long HighEventCount { get; set; }
    /// <summary>Gets or sets the count of medium severity events.</summary>
    public long MediumEventCount { get; set; }
    /// <summary>Gets or sets the count of low severity events.</summary>
    public long LowEventCount { get; set; }
    /// <summary>Gets or sets the count of informational events.</summary>
    public long InformationalEventCount { get; set; }

    // User and resource tracking
    /// <summary>Gets or sets the number of unique users involved in security events.</summary>
    public int UniqueUsersCount { get; set; }
    /// <summary>Gets or sets the number of active event correlations.</summary>
    public int ActiveCorrelations { get; set; }
    /// <summary>Gets or sets the average number of events per correlation.</summary>
    public double AverageEventsPerCorrelation { get; set; }
    /// <summary>Gets or sets the timestamp of the first recorded event.</summary>
    public DateTimeOffset FirstEventTime { get; set; }
    /// <summary>Gets or sets the timestamp of the last recorded event.</summary>
    public DateTimeOffset LastEventTime { get; set; }

    // Collections
    /// <summary>Gets the count of events per user.</summary>
    public ConcurrentDictionary<string, long> UserEventCounts { get; } = new();
    /// <summary>Gets the count of events per resource.</summary>
    public ConcurrentDictionary<string, long> ResourceEventCounts { get; } = new();
    /// <summary>Gets the count of events by type.</summary>
    public Dictionary<SecurityEventType, long> EventsByType { get; } = [];
    /// <summary>Gets the count of events by security level.</summary>
    public Dictionary<SecurityLevel, long> EventsByLevel { get; } = [];
}
