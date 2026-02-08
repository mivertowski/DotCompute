// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Security;

namespace DotCompute.Core.Security.Types;

/// <summary>
/// Configuration for security logging operations.
/// </summary>
/// <remarks>
/// <para>
/// Controls security audit logging behavior including retention, sensitive data handling,
/// and alerting for critical events.
/// </para>
/// <para>
/// Security logs are essential for compliance (SOC 2, GDPR, HIPAA) and incident response.
/// Configure retention and sensitivity settings based on your regulatory requirements.
/// </para>
/// </remarks>
public sealed class SecurityLoggingConfiguration
{
    /// <summary>Gets or sets whether audit logging is enabled.</summary>
    /// <remarks>Disable only in development. Required for production compliance.</remarks>
    public bool AuditEnabled { get; set; } = true;

    /// <summary>Gets or sets the maximum number of log entries to keep in memory.</summary>
    /// <remarks>Older entries are flushed to disk. Increase for high-throughput systems.</remarks>
    public int MaxLogEntries { get; set; } = 10000;

    /// <summary>Gets or sets the log file path for security events.</summary>
    public string LogFilePath { get; set; } = "./logs/security.log";

    /// <summary>Gets or sets whether to include sensitive data in logs.</summary>
    /// <remarks>
    /// ⚠️ WARNING: Enabling this may violate data protection regulations.
    /// Only enable for debugging in secure development environments.
    /// </remarks>
    public bool IncludeSensitiveData { get; set; }

    /// <summary>Gets or sets the log retention period.</summary>
    /// <remarks>
    /// Compliance requirements vary: GDPR (6 years), HIPAA (6 years), SOC 2 (1 year minimum).
    /// Default 90 days balances storage and compliance.
    /// </remarks>
    public TimeSpan LogRetentionPeriod { get; set; } = TimeSpan.FromDays(90);

    /// <summary>Gets or sets the minimum security level to log.</summary>
    /// <remarks>Lower levels generate more logs. Medium is recommended for production.</remarks>
    public SecurityLevel MinimumLogLevel { get; set; } = SecurityLevel.Medium;

    /// <summary>Gets or sets whether critical security event alerts are enabled.</summary>
    /// <remarks>Sends immediate notifications for critical violations.</remarks>
    public bool EnableCriticalEventAlerts { get; set; } = true;

    /// <summary>Gets or sets whether to include stack traces in log entries.</summary>
    /// <remarks>Useful for debugging but increases log size significantly.</remarks>
    public bool IncludeStackTraces { get; set; }

    /// <summary>Gets or sets whether correlation tracking is enabled.</summary>
    /// <remarks>Tracks related security events across operations. Essential for forensics.</remarks>
    public bool EnableCorrelationTracking { get; set; } = true;
}

/// <summary>
/// Represents a security log entry with comprehensive audit information.
/// </summary>
/// <remarks>
/// <para>
/// Immutable record of a security event including timestamp, event type, user context,
/// and correlation information for audit trail reconstruction.
/// </para>
/// <para>
/// Log entries are designed for SIEM (Security Information and Event Management) integration.
/// </para>
/// </remarks>
public sealed class SecurityLogEntry
{
    /// <summary>Gets the unique identifier for this log entry.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Gets the unique sequence number for this log entry.</summary>
    /// <remarks>Monotonically increasing. Used to detect log tampering.</remarks>
    public long SequenceNumber { get; init; }

    /// <summary>Gets the timestamp when this entry was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the type of security event.</summary>
    public SecurityEventType EventType { get; init; }

    /// <summary>Gets the security level of this event.</summary>
    public SecurityLevel Level { get; init; }

    /// <summary>Gets the event message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the user ID associated with this event, if any.</summary>
    public string? UserId { get; init; }

    /// <summary>Gets the resource that was accessed.</summary>
    public string? Resource { get; init; }

    /// <summary>Gets the resource ID associated with this event.</summary>
    public string? ResourceId { get; init; }

    /// <summary>Gets the operation that was performed.</summary>
    public string? Operation { get; init; }

    /// <summary>Gets the outcome of the operation.</summary>
    public string? Outcome { get; init; }

    /// <summary>Gets additional context data.</summary>
    public IReadOnlyDictionary<string, string>? Context { get; init; }

    /// <summary>Gets additional data for this log entry.</summary>
    public Dictionary<string, object>? AdditionalData { get; init; }

    /// <summary>Gets the correlation ID for related events.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Gets the source IP address if network-related.</summary>
    public string? SourceIpAddress { get; init; }

    /// <summary>Gets the stack trace if available.</summary>
    public string? StackTrace { get; init; }

    /// <summary>Gets the caller name (method or function).</summary>
    public string? CallerName { get; init; }

    /// <summary>Gets the source file path where the event was logged.</summary>
    public string? SourceFile { get; init; }

    /// <summary>Gets the line number in the source file.</summary>
    public int? LineNumber { get; init; }
}

/// <summary>
/// Result from exporting audit logs.
/// </summary>
/// <remarks>
/// Contains export statistics and any warnings or errors encountered.
/// </remarks>
public sealed class AuditExportResult
{
    /// <summary>Gets or sets whether the export succeeded.</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>Gets or sets whether the export was successful (alias for IsSuccess).</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the number of entries exported.</summary>
    public required int EntriesExported { get; init; }

    /// <summary>Gets or sets the export file path.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets or sets the export file path (alias for FilePath).</summary>
    public string? ExportFilePath { get; set; }

    /// <summary>Gets or sets the export format used.</summary>
    public string? Format { get; init; }

    /// <summary>Gets or sets the time range of exported entries.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Gets or sets the end of the time range.</summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>Gets or sets the export time.</summary>
    public DateTimeOffset? ExportTime { get; set; }

    /// <summary>Gets or sets any warnings generated during export.</summary>
    public IList<string> Warnings { get; init; } = [];

    /// <summary>Gets or sets error message if export failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the file size in bytes.</summary>
    public long FileSizeBytes { get; set; }
}

/// <summary>
/// Correlation context for tracking related security events.
/// </summary>
/// <remarks>
/// <para>
/// Enables distributed tracing of security events across operations and microservices.
/// Essential for forensic analysis and incident response.
/// </para>
/// </remarks>
public sealed class CorrelationContext
{
    /// <summary>Gets or sets the correlation ID.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Gets or sets when the correlation context was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the start time of the correlated operation.</summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>Gets or sets the initiating user.</summary>
    public string? UserId { get; init; }

    /// <summary>Gets or sets the operation being traced.</summary>
    public string? Operation { get; init; }

    /// <summary>Gets or sets parent correlation ID if nested.</summary>
    public string? ParentCorrelationId { get; init; }

    /// <summary>Gets or sets additional correlation metadata.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>Gets or sets related event IDs.</summary>
    public IList<string> RelatedEventIds { get; init; } = [];

    /// <summary>Gets or sets the number of events in this correlation context.</summary>
    public int EventCount { get; set; }
}
