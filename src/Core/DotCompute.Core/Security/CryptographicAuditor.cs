// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using System.Text;
using System.Security;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides comprehensive audit logging and security event tracking for cryptographic operations.
/// Maintains detailed security logs for compliance and forensic analysis.
/// </summary>
internal sealed class CryptographicAuditor : IDisposable
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

        _logger.LogInfoMessage($"CryptographicAuditor initialized with audit log: {_auditLogPath}");
    }

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
                AdditionalData = additionalData != null ? JsonSerializer.Serialize(additionalData) : null,
                SourceAddress = GetSourceAddress(),
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId
            };

            _auditQueue.Enqueue(securityEvent);

            // Log immediately for high severity events
            if (severity >= SecurityEventSeverity.High)
            {
                await FlushAuditLogsAsync();
                _logger.LogWarning("High severity security event: {eventType} - {description}", eventType, description);
            }

            _logger.LogDebugMessage($"Security event logged: {eventType} - {severity}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to log security event: {eventType}");
        }
    }

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

        _logger.LogError("Security violation detected: {violationType} - {description}", violationType, description);
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

        _logger.LogInfoMessage($"Generating security audit report from {startTime} to {endTime}");

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
                SecurityIncidents = events.Where(e => e.Category == SecurityEventCategory.SecurityIncident).ToList(),
                RecommendationsGenerated = GenerateSecurityRecommendations(events)
            };

            // Calculate security metrics
            report.SecurityMetrics = CalculateSecurityMetrics(events);

            _logger.LogInfoMessage($"Security audit report generated: {report.TotalEvents} events analyzed");
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to generate security audit report");
            throw;
        }
    }

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
            $"audit_export_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.{format.ToString().ToLowerInvariant()}");

        var exportContent = format switch
        {
            AuditExportFormat.Json => JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true }),
            AuditExportFormat.Csv => ConvertToCsv(events),
            AuditExportFormat.Xml => ConvertToXml(events),
            _ => throw new NotSupportedException($"Export format not supported: {format}")
        };

        await File.WriteAllTextAsync(exportPath, exportContent);

        _logger.LogInfoMessage($"Audit logs exported to: {exportPath}");
        return exportPath;
    }

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

        _logger.LogInfoMessage($"Audit log search completed: {results.Count} results for '{searchTerm}'");
        return results;
    }

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

            _logger.LogDebugMessage($"Flushed {eventsToFlush.Count} audit log entries");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to flush audit logs");
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
            _logger.LogErrorMessage(ex, "Error during scheduled audit log flush");
        }
    }

    private string FormatLogEntry(SecurityEvent securityEvent)
    {
        var logEntry = new
        {
            securityEvent.EventId,
            securityEvent.Timestamp,
            securityEvent.EventType,
            securityEvent.Severity,
            securityEvent.Category,
            securityEvent.Description,
            securityEvent.UserId,
            securityEvent.SessionId,
            securityEvent.SourceAddress,
            securityEvent.ProcessId,
            securityEvent.ThreadId,
            securityEvent.AdditionalData
        };

        return JsonSerializer.Serialize(logEntry);
    }

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
                var eventData = JsonSerializer.Deserialize<SecurityEvent>(line);
                if (eventData != null && eventData.Timestamp >= startTime && eventData.Timestamp <= endTime)
                {
                    events.Add(eventData);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Failed to parse audit log entry: {error}", ex.Message);
            }
        }

        return events.OrderBy(e => e.Timestamp).ToList();
    }

    private static List<string> GenerateSecurityRecommendations(List<SecurityEvent> events)
    {
        var recommendations = new List<string>();

        var securityIncidents = events.Where(e => e.Category == SecurityEventCategory.SecurityIncident).Count();
        if (securityIncidents > 0)
        {
            recommendations.Add($"Review {securityIncidents} security incidents and implement preventive measures");
        }

        var failedOperations = events.Where(e => e.AdditionalData?.Contains("\"Success\":false") == true).Count();
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

    private SecurityMetrics CalculateSecurityMetrics(List<SecurityEvent> events)
    {
        return new SecurityMetrics
        {
            TotalOperations = events.Count,
            SuccessfulOperations = events.Count(e => e.AdditionalData?.Contains("\"Success\":true") == true),
            FailedOperations = events.Count(e => e.AdditionalData?.Contains("\"Success\":false") == true),
            SecurityIncidents = events.Count(e => e.Category == SecurityEventCategory.SecurityIncident),
            KeyManagementOperations = events.Count(e => e.Category == SecurityEventCategory.KeyManagement),
            CryptographicOperations = events.Count(e => e.Category == SecurityEventCategory.CryptographicOperation),
            AverageResponseTime = CalculateAverageResponseTime(events),
            SecurityScore = CalculateSecurityScore(events)
        };
    }

    private static double CalculateAverageResponseTime(List<SecurityEvent> events)
    {
        var operationsWithTiming = events.Where(e => e.AdditionalData?.Contains("ExecutionTimeMs") == true).ToList();
        if (!operationsWithTiming.Any())
        {
            return 0;
        }

        // This is a simplified calculation - in practice, you'd parse the JSON to extract execution times

        return 100.0; // Placeholder
    }

    private static double CalculateSecurityScore(List<SecurityEvent> events)
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

    private string ConvertToCsv(List<SecurityEvent> events)
    {
        var csv = new StringBuilder();
        _ = csv.AppendLine("EventId,Timestamp,EventType,Severity,Category,Description,UserId,SessionId");

        foreach (var evt in events)
        {
            _ = csv.AppendLine($"{evt.EventId},{evt.Timestamp:O},{evt.EventType},{evt.Severity},{evt.Category}," +
                          $"\"{evt.Description.Replace("\"", "\"\"")}\",{evt.UserId},{evt.SessionId}");
        }

        return csv.ToString();
    }

    private string ConvertToXml(List<SecurityEvent> events)
    {
        // Simplified XML conversion - in practice, use XmlDocument or XElement
        var xml = new StringBuilder();
        _ = xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        _ = xml.AppendLine("<SecurityEvents>");

        foreach (var evt in events)
        {
            _ = xml.AppendLine($"  <Event id=\"{evt.EventId}\" timestamp=\"{evt.Timestamp:O}\">");
            _ = xml.AppendLine($"    <Type>{evt.EventType}</Type>");
            _ = xml.AppendLine($"    <Severity>{evt.Severity}</Severity>");
            _ = xml.AppendLine($"    <Category>{evt.Category}</Category>");
            _ = xml.AppendLine($"    <Description>{SecurityElement.Escape(evt.Description)}</Description>");
            _ = xml.AppendLine("  </Event>");
        }

        _ = xml.AppendLine("</SecurityEvents>");
        return xml.ToString();
    }

    private static string? GetSourceAddress()
    {
        // In a real implementation, this would extract the source IP address
        return "localhost";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Flush any remaining audit logs
            try
            {
                _ = FlushAuditLogsAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error flushing audit logs during disposal");
            }

            _flushTimer?.Dispose();
            _flushLock?.Dispose();
            _disposed = true;
        }
    }
}

// Supporting classes and enums
public enum SecurityEventSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum SecurityEventCategory
{
    General,
    KeyManagement,
    CryptographicOperation,
    Validation,
    Configuration,
    SecurityIncident
}

public enum AuditExportFormat
{
    Json,
    Csv,
    Xml
}

public class SecurityEvent
{
    public required string EventId { get; set; }
    public required string EventType { get; set; }
    public SecurityEventSeverity Severity { get; set; }
    public SecurityEventCategory Category { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public string? SourceAddress { get; set; }
    public int ProcessId { get; set; }
    public int ThreadId { get; set; }
    public string? AdditionalData { get; set; }
}

public class SecurityAuditReport
{
    public required string ReportId { get; set; }
    public DateTimeOffset GenerationTime { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<SecurityEventSeverity, int> EventsBySeverity { get; set; } = new();
    public Dictionary<SecurityEventCategory, int> EventsByCategory { get; set; } = new();
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public List<SecurityEvent> SecurityIncidents { get; set; } = new();
    public List<string> RecommendationsGenerated { get; set; } = new();
    public SecurityMetrics? SecurityMetrics { get; set; }
}

public class SecurityMetrics
{
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public int SecurityIncidents { get; set; }
    public int KeyManagementOperations { get; set; }
    public int CryptographicOperations { get; set; }
    public double AverageResponseTime { get; set; }
    public double SecurityScore { get; set; }
    public int ActiveCorrelations { get; set; }
    public double AverageEventsPerCorrelation { get; set; }

    // Properties expected by SecurityMetricsLogger
    public Dictionary<SecurityEventType, long> EventsByType { get; set; } = new();
    public long AuthenticationSuccessCount { get; set; }
    public long AuthenticationFailureCount { get; set; }
    public long AccessGrantedCount { get; set; }
    public long AccessDeniedCount { get; set; }
    public long SecurityViolationCount { get; set; }
    public long DataAccessCount { get; set; }
    public long DataModificationCount { get; set; }
    public long DataDeletionCount { get; set; }
    public Dictionary<DotCompute.Core.Security.SecurityLevel, long> EventsByLevel { get; set; } = new();
    public long CriticalEventCount { get; set; }
    public long HighEventCount { get; set; }
    public long MediumEventCount { get; set; }
    public long LowEventCount { get; set; }
    public long InformationalEventCount { get; set; }
    public long TotalEventCount { get; set; }
    public int UniqueUsersCount { get; set; }
    public ConcurrentDictionary<string, long> UserEventCounts { get; set; } = new();
    public ConcurrentDictionary<string, long> ResourceEventCounts { get; set; } = new();
    public DateTimeOffset FirstEventTime { get; set; }
    public DateTimeOffset LastEventTime { get; set; }
}
