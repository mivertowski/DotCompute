// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Security;

/// <summary>
/// Provides audit logging for security-relevant events.
/// </summary>
/// <remarks>
/// <para>
/// Audit logging is essential for enterprise compliance (SOX, HIPAA, GDPR)
/// and security monitoring. This interface captures:
/// </para>
/// <list type="bullet">
/// <item><description>Access attempts (successful and failed)</description></item>
/// <item><description>Resource allocation and release</description></item>
/// <item><description>Policy evaluations and decisions</description></item>
/// <item><description>Administrative actions</description></item>
/// <item><description>Security-relevant events</description></item>
/// </list>
/// </remarks>
public interface IAuditLog
{
    /// <summary>
    /// Logs an audit event.
    /// </summary>
    /// <param name="event">The audit event to log.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask LogAsync(
        AuditEvent @event,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an access attempt event.
    /// </summary>
    /// <param name="principal">The principal attempting access.</param>
    /// <param name="deviceId">The target device.</param>
    /// <param name="accessType">The type of access requested.</param>
    /// <param name="decision">The access decision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask LogAccessAttemptAsync(
        ISecurityPrincipal principal,
        string deviceId,
        DeviceAccessType accessType,
        AccessDecision decision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries audit events matching the specified criteria.
    /// </summary>
    /// <param name="query">The query criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit events.</returns>
    public IAsyncEnumerable<AuditEvent> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about audit events.
    /// </summary>
    /// <param name="startTime">Start of the time range.</param>
    /// <param name="endTime">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audit statistics.</returns>
    public ValueTask<AuditStatistics> GetStatisticsAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an audit event.
/// </summary>
public sealed class AuditEvent
{
    /// <summary>Gets the unique event identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets the event timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the event type.</summary>
    public required AuditEventType EventType { get; init; }

    /// <summary>Gets the event category.</summary>
    public required AuditCategory Category { get; init; }

    /// <summary>Gets the event severity.</summary>
    public AuditSeverity Severity { get; init; } = AuditSeverity.Info;

    /// <summary>Gets the principal associated with the event (if any).</summary>
    public ISecurityPrincipal? Principal { get; init; }

    /// <summary>Gets the target device (if applicable).</summary>
    public string? DeviceId { get; init; }

    /// <summary>Gets the event outcome.</summary>
    public required AuditOutcome Outcome { get; init; }

    /// <summary>Gets the event description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets detailed event data.</summary>
    public IReadOnlyDictionary<string, object>? Details { get; init; }

    /// <summary>Gets the source of the event (service, component).</summary>
    public string? Source { get; init; }

    /// <summary>Gets the correlation ID for related events.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Gets the client IP address (if applicable).</summary>
    public string? ClientAddress { get; init; }

    /// <summary>Gets the session ID (if applicable).</summary>
    public string? SessionId { get; init; }
}

/// <summary>
/// Types of audit events.
/// </summary>
public enum AuditEventType
{
    // Access Events
    /// <summary>Access was requested.</summary>
    AccessRequest,

    /// <summary>Access was granted.</summary>
    AccessGranted,

    /// <summary>Access was denied.</summary>
    AccessDenied,

    /// <summary>Access was released.</summary>
    AccessReleased,

    /// <summary>Access expired.</summary>
    AccessExpired,

    /// <summary>Access was revoked.</summary>
    AccessRevoked,

    // Resource Events
    /// <summary>Memory was allocated.</summary>
    MemoryAllocated,

    /// <summary>Memory was released.</summary>
    MemoryReleased,

    /// <summary>Kernel execution started.</summary>
    KernelStarted,

    /// <summary>Kernel execution completed.</summary>
    KernelCompleted,

    /// <summary>Kernel execution failed.</summary>
    KernelFailed,

    // Policy Events
    /// <summary>Policy was evaluated.</summary>
    PolicyEvaluated,

    /// <summary>Policy was created or updated.</summary>
    PolicyChanged,

    /// <summary>Policy was deleted.</summary>
    PolicyDeleted,

    // Quota Events
    /// <summary>Quota threshold was reached.</summary>
    QuotaWarning,

    /// <summary>Quota limit was exceeded.</summary>
    QuotaExceeded,

    /// <summary>Quota was reset.</summary>
    QuotaReset,

    // Administrative Events
    /// <summary>Configuration was changed.</summary>
    ConfigurationChanged,

    /// <summary>Device was added.</summary>
    DeviceAdded,

    /// <summary>Device was removed.</summary>
    DeviceRemoved,

    /// <summary>Device was reset.</summary>
    DeviceReset,

    // Security Events
    /// <summary>Authentication succeeded.</summary>
    AuthenticationSuccess,

    /// <summary>Authentication failed.</summary>
    AuthenticationFailure,

    /// <summary>Suspicious activity detected.</summary>
    SuspiciousActivity,

    /// <summary>Security alert triggered.</summary>
    SecurityAlert
}

/// <summary>
/// Categories of audit events.
/// </summary>
public enum AuditCategory
{
    /// <summary>Access control events.</summary>
    Access,

    /// <summary>Resource usage events.</summary>
    Resource,

    /// <summary>Policy and authorization events.</summary>
    Policy,

    /// <summary>Quota and limit events.</summary>
    Quota,

    /// <summary>Administrative events.</summary>
    Admin,

    /// <summary>Security and authentication events.</summary>
    Security,

    /// <summary>System events.</summary>
    System
}

/// <summary>
/// Severity levels for audit events.
/// </summary>
public enum AuditSeverity
{
    /// <summary>Debug-level detail.</summary>
    Debug = 0,

    /// <summary>Informational event.</summary>
    Info = 1,

    /// <summary>Warning - potential issue.</summary>
    Warning = 2,

    /// <summary>Error - operation failed.</summary>
    Error = 3,

    /// <summary>Critical - requires immediate attention.</summary>
    Critical = 4
}

/// <summary>
/// Outcome of an audited operation.
/// </summary>
public enum AuditOutcome
{
    /// <summary>Operation succeeded.</summary>
    Success,

    /// <summary>Operation failed.</summary>
    Failure,

    /// <summary>Operation was denied.</summary>
    Denied,

    /// <summary>Informational only - no specific outcome.</summary>
    Informational
}

/// <summary>
/// Query criteria for audit events.
/// </summary>
public sealed class AuditQuery
{
    /// <summary>Gets the start time for the query range.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Gets the end time for the query range.</summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>Gets event types to include.</summary>
    public IReadOnlySet<AuditEventType>? EventTypes { get; init; }

    /// <summary>Gets categories to include.</summary>
    public IReadOnlySet<AuditCategory>? Categories { get; init; }

    /// <summary>Gets minimum severity level.</summary>
    public AuditSeverity? MinSeverity { get; init; }

    /// <summary>Gets outcomes to include.</summary>
    public IReadOnlySet<AuditOutcome>? Outcomes { get; init; }

    /// <summary>Gets principal ID to filter by.</summary>
    public string? PrincipalId { get; init; }

    /// <summary>Gets device ID to filter by.</summary>
    public string? DeviceId { get; init; }

    /// <summary>Gets correlation ID to filter by.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Gets text to search in description.</summary>
    public string? SearchText { get; init; }

    /// <summary>Gets maximum number of results.</summary>
    public int? Limit { get; init; }

    /// <summary>Gets number of results to skip.</summary>
    public int? Offset { get; init; }

    /// <summary>Gets the sort order.</summary>
    public AuditSortOrder SortOrder { get; init; } = AuditSortOrder.TimestampDescending;
}

/// <summary>
/// Sort order for audit queries.
/// </summary>
public enum AuditSortOrder
{
    /// <summary>Newest first.</summary>
    TimestampDescending,

    /// <summary>Oldest first.</summary>
    TimestampAscending,

    /// <summary>Highest severity first.</summary>
    SeverityDescending,

    /// <summary>Lowest severity first.</summary>
    SeverityAscending
}

/// <summary>
/// Statistics about audit events.
/// </summary>
public sealed class AuditStatistics
{
    /// <summary>Gets the start of the statistics period.</summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>Gets the end of the statistics period.</summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Gets total number of events.</summary>
    public required long TotalEvents { get; init; }

    /// <summary>Gets events by category.</summary>
    public required IReadOnlyDictionary<AuditCategory, long> EventsByCategory { get; init; }

    /// <summary>Gets events by severity.</summary>
    public required IReadOnlyDictionary<AuditSeverity, long> EventsBySeverity { get; init; }

    /// <summary>Gets events by outcome.</summary>
    public required IReadOnlyDictionary<AuditOutcome, long> EventsByOutcome { get; init; }

    /// <summary>Gets access denied count.</summary>
    public long AccessDeniedCount { get; init; }

    /// <summary>Gets authentication failure count.</summary>
    public long AuthenticationFailureCount { get; init; }

    /// <summary>Gets quota exceeded count.</summary>
    public long QuotaExceededCount { get; init; }

    /// <summary>Gets most active principals.</summary>
    public IReadOnlyList<(string PrincipalId, long EventCount)>? TopPrincipals { get; init; }

    /// <summary>Gets most accessed devices.</summary>
    public IReadOnlyList<(string DeviceId, long AccessCount)>? TopDevices { get; init; }
}

/// <summary>
/// Factory for creating audit events.
/// </summary>
public static class AuditEventFactory
{
    /// <summary>Creates an access request event.</summary>
    public static AuditEvent AccessRequest(
        ISecurityPrincipal principal,
        string deviceId,
        DeviceAccessType accessType) => new()
        {
            EventType = AuditEventType.AccessRequest,
            Category = AuditCategory.Access,
            Principal = principal,
            DeviceId = deviceId,
            Outcome = AuditOutcome.Informational,
            Description = $"Access request: {accessType} on device {deviceId}",
            Details = new Dictionary<string, object>
            {
                ["accessType"] = accessType.ToString()
            }
        };

    /// <summary>Creates an access granted event.</summary>
    public static AuditEvent AccessGranted(
        ISecurityPrincipal principal,
        string deviceId,
        DeviceAccessType accessType,
        Guid grantId) => new()
        {
            EventType = AuditEventType.AccessGranted,
            Category = AuditCategory.Access,
            Principal = principal,
            DeviceId = deviceId,
            Outcome = AuditOutcome.Success,
            Description = $"Access granted: {accessType} on device {deviceId}",
            Details = new Dictionary<string, object>
            {
                ["accessType"] = accessType.ToString(),
                ["grantId"] = grantId.ToString()
            }
        };

    /// <summary>Creates an access denied event.</summary>
    public static AuditEvent AccessDenied(
        ISecurityPrincipal principal,
        string deviceId,
        DeviceAccessType accessType,
        string reason) => new()
        {
            EventType = AuditEventType.AccessDenied,
            Category = AuditCategory.Access,
            Severity = AuditSeverity.Warning,
            Principal = principal,
            DeviceId = deviceId,
            Outcome = AuditOutcome.Denied,
            Description = $"Access denied: {accessType} on device {deviceId}. Reason: {reason}",
            Details = new Dictionary<string, object>
            {
                ["accessType"] = accessType.ToString(),
                ["reason"] = reason
            }
        };

    /// <summary>Creates a kernel execution event.</summary>
    public static AuditEvent KernelExecution(
        ISecurityPrincipal principal,
        string deviceId,
        string kernelName,
        bool success,
        TimeSpan duration) => new()
        {
            EventType = success ? AuditEventType.KernelCompleted : AuditEventType.KernelFailed,
            Category = AuditCategory.Resource,
            Severity = success ? AuditSeverity.Info : AuditSeverity.Error,
            Principal = principal,
            DeviceId = deviceId,
            Outcome = success ? AuditOutcome.Success : AuditOutcome.Failure,
            Description = $"Kernel '{kernelName}' {(success ? "completed" : "failed")} in {duration.TotalMilliseconds:F2}ms",
            Details = new Dictionary<string, object>
            {
                ["kernelName"] = kernelName,
                ["durationMs"] = duration.TotalMilliseconds
            }
        };

    /// <summary>Creates a quota exceeded event.</summary>
    public static AuditEvent QuotaExceeded(
        ISecurityPrincipal principal,
        string quotaType,
        long limit,
        long current) => new()
        {
            EventType = AuditEventType.QuotaExceeded,
            Category = AuditCategory.Quota,
            Severity = AuditSeverity.Warning,
            Principal = principal,
            Outcome = AuditOutcome.Denied,
            Description = $"Quota exceeded: {quotaType} (limit: {limit}, current: {current})",
            Details = new Dictionary<string, object>
            {
                ["quotaType"] = quotaType,
                ["limit"] = limit,
                ["current"] = current
            }
        };

    /// <summary>Creates a security alert event.</summary>
    public static AuditEvent SecurityAlert(
        string description,
        ISecurityPrincipal? principal = null,
        string? deviceId = null) => new()
        {
            EventType = AuditEventType.SecurityAlert,
            Category = AuditCategory.Security,
            Severity = AuditSeverity.Critical,
            Principal = principal,
            DeviceId = deviceId,
            Outcome = AuditOutcome.Informational,
            Description = description
        };
}
