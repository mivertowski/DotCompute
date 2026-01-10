// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Security;

/// <summary>
/// Provides access control for compute devices and accelerators.
/// Enables enterprise security policies for GPU resource management.
/// </summary>
/// <remarks>
/// <para>
/// Device access control is essential for multi-tenant environments where
/// different users, applications, or workloads need isolated access to
/// compute resources. This interface supports:
/// </para>
/// <list type="bullet">
/// <item><description>Per-device access permissions</description></item>
/// <item><description>Resource quotas and limits</description></item>
/// <item><description>Audit logging of access attempts</description></item>
/// <item><description>Policy-based authorization</description></item>
/// </list>
/// </remarks>
public interface IDeviceAccessControl
{
    /// <summary>
    /// Checks if the specified principal has permission to access a device.
    /// </summary>
    /// <param name="principal">The security principal (user, service, or application).</param>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="accessType">The type of access requested.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An access decision with details.</returns>
    public ValueTask<AccessDecision> CheckAccessAsync(
        ISecurityPrincipal principal,
        string deviceId,
        DeviceAccessType accessType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests access to a device, potentially acquiring a lease or reservation.
    /// </summary>
    /// <param name="request">The access request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An access grant with lease information if approved.</returns>
    public ValueTask<AccessGrant> RequestAccessAsync(
        DeviceAccessRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases access to a device, freeing any held resources or leases.
    /// </summary>
    /// <param name="grantId">The access grant identifier to release.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask ReleaseAccessAsync(
        Guid grantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current access grants for a principal.
    /// </summary>
    /// <param name="principal">The security principal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active access grants.</returns>
    public ValueTask<IReadOnlyList<AccessGrant>> GetActiveGrantsAsync(
        ISecurityPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the resource quota for a principal.
    /// </summary>
    /// <param name="principal">The security principal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resource quota and current usage.</returns>
    public ValueTask<ResourceQuota> GetQuotaAsync(
        ISecurityPrincipal principal,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a security principal (user, service, or application).
/// </summary>
public interface ISecurityPrincipal
{
    /// <summary>Gets the unique identifier for this principal.</summary>
    public string Id { get; }

    /// <summary>Gets the principal name.</summary>
    public string Name { get; }

    /// <summary>Gets the principal type.</summary>
    public PrincipalType Type { get; }

    /// <summary>Gets the roles assigned to this principal.</summary>
    public IReadOnlySet<string> Roles { get; }

    /// <summary>Gets the claims associated with this principal.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; }
}

/// <summary>
/// Types of security principals.
/// </summary>
public enum PrincipalType
{
    /// <summary>A human user.</summary>
    User,

    /// <summary>A service account or daemon.</summary>
    Service,

    /// <summary>An application or workload.</summary>
    Application,

    /// <summary>A system process.</summary>
    System
}

/// <summary>
/// Types of device access.
/// </summary>
[Flags]
public enum DeviceAccessType
{
    /// <summary>No access.</summary>
    None = 0,

    /// <summary>Read-only access (query device info, read memory).</summary>
    Read = 1,

    /// <summary>Write access (allocate memory, execute kernels).</summary>
    Write = 2,

    /// <summary>Exclusive access (no sharing with other principals).</summary>
    Exclusive = 4,

    /// <summary>Administrative access (configure device, reset).</summary>
    Admin = 8,

    /// <summary>Standard compute access (read + write).</summary>
    Compute = Read | Write,

    /// <summary>Full access.</summary>
    Full = Read | Write | Exclusive | Admin
}

/// <summary>
/// Result of an access check.
/// </summary>
public sealed class AccessDecision
{
    /// <summary>Gets whether access is allowed.</summary>
    public required bool IsAllowed { get; init; }

    /// <summary>Gets the reason for the decision.</summary>
    public required string Reason { get; init; }

    /// <summary>Gets any conditions that must be met.</summary>
    public IReadOnlyList<string> Conditions { get; init; } = [];

    /// <summary>Gets the policy that made the decision.</summary>
    public string? PolicyId { get; init; }

    /// <summary>Gets the time when this decision was made.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Creates an allowed decision.</summary>
    public static AccessDecision Allow(string reason, string? policyId = null)
        => new() { IsAllowed = true, Reason = reason, PolicyId = policyId };

    /// <summary>Creates a denied decision.</summary>
    public static AccessDecision Deny(string reason, string? policyId = null)
        => new() { IsAllowed = false, Reason = reason, PolicyId = policyId };
}

/// <summary>
/// A request for device access.
/// </summary>
public sealed class DeviceAccessRequest
{
    /// <summary>Gets the requesting principal.</summary>
    public required ISecurityPrincipal Principal { get; init; }

    /// <summary>Gets the device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Gets the type of access requested.</summary>
    public required DeviceAccessType AccessType { get; init; }

    /// <summary>Gets the requested duration (null for indefinite).</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Gets the priority of the request.</summary>
    public AccessPriority Priority { get; init; } = AccessPriority.Normal;

    /// <summary>Gets the purpose/reason for the access request.</summary>
    public string? Purpose { get; init; }

    /// <summary>Gets additional context for the request.</summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }
}

/// <summary>
/// Priority levels for access requests.
/// </summary>
public enum AccessPriority
{
    /// <summary>Low priority - can be preempted.</summary>
    Low = 0,

    /// <summary>Normal priority.</summary>
    Normal = 1,

    /// <summary>High priority - preferred scheduling.</summary>
    High = 2,

    /// <summary>Critical priority - cannot be preempted.</summary>
    Critical = 3
}

/// <summary>
/// A granted access permission with lease information.
/// </summary>
public sealed class AccessGrant
{
    /// <summary>Gets the unique grant identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the granted principal.</summary>
    public required ISecurityPrincipal Principal { get; init; }

    /// <summary>Gets the device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Gets the granted access type.</summary>
    public required DeviceAccessType AccessType { get; init; }

    /// <summary>Gets when the grant was created.</summary>
    public required DateTimeOffset GrantedAt { get; init; }

    /// <summary>Gets when the grant expires (null for indefinite).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the current status of the grant.</summary>
    public GrantStatus Status { get; init; } = GrantStatus.Active;

    /// <summary>Gets resource limits applied to this grant.</summary>
    public ResourceLimits? Limits { get; init; }

    /// <summary>Checks if the grant is currently valid.</summary>
    public bool IsValid => Status == GrantStatus.Active &&
                          (ExpiresAt == null || ExpiresAt > DateTimeOffset.UtcNow);
}

/// <summary>
/// Status of an access grant.
/// </summary>
public enum GrantStatus
{
    /// <summary>Grant is pending approval.</summary>
    Pending,

    /// <summary>Grant is active and valid.</summary>
    Active,

    /// <summary>Grant has expired.</summary>
    Expired,

    /// <summary>Grant was revoked.</summary>
    Revoked,

    /// <summary>Grant was released by the holder.</summary>
    Released
}

/// <summary>
/// Resource limits for an access grant.
/// </summary>
public sealed class ResourceLimits
{
    /// <summary>Gets the maximum memory allocation in bytes.</summary>
    public long? MaxMemoryBytes { get; init; }

    /// <summary>Gets the maximum compute time per execution in milliseconds.</summary>
    public long? MaxComputeTimeMs { get; init; }

    /// <summary>Gets the maximum number of concurrent kernel executions.</summary>
    public int? MaxConcurrentKernels { get; init; }

    /// <summary>Gets the maximum memory bandwidth usage (bytes/second).</summary>
    public long? MaxBandwidthBytesPerSecond { get; init; }

    /// <summary>Gets the maximum GPU utilization percentage (0-100).</summary>
    public int? MaxUtilizationPercent { get; init; }
}

/// <summary>
/// Resource quota for a principal.
/// </summary>
public sealed class ResourceQuota
{
    /// <summary>Gets the principal this quota applies to.</summary>
    public required ISecurityPrincipal Principal { get; init; }

    /// <summary>Gets the quota limits.</summary>
    public required ResourceLimits Limits { get; init; }

    /// <summary>Gets the current usage.</summary>
    public required ResourceUsage CurrentUsage { get; init; }

    /// <summary>Gets the quota period (daily, monthly, etc.).</summary>
    public QuotaPeriod Period { get; init; } = QuotaPeriod.Monthly;

    /// <summary>Gets when the current period resets.</summary>
    public DateTimeOffset? ResetAt { get; init; }
}

/// <summary>
/// Current resource usage.
/// </summary>
public sealed class ResourceUsage
{
    /// <summary>Gets the memory currently allocated in bytes.</summary>
    public long MemoryBytesAllocated { get; init; }

    /// <summary>Gets the total compute time used in milliseconds.</summary>
    public long ComputeTimeUsedMs { get; init; }

    /// <summary>Gets the number of active kernel executions.</summary>
    public int ActiveKernels { get; init; }

    /// <summary>Gets the peak memory usage in bytes.</summary>
    public long PeakMemoryBytes { get; init; }

    /// <summary>Gets the total number of kernel executions.</summary>
    public long TotalExecutions { get; init; }
}

/// <summary>
/// Quota period types.
/// </summary>
public enum QuotaPeriod
{
    /// <summary>Quota resets hourly.</summary>
    Hourly,

    /// <summary>Quota resets daily.</summary>
    Daily,

    /// <summary>Quota resets weekly.</summary>
    Weekly,

    /// <summary>Quota resets monthly.</summary>
    Monthly,

    /// <summary>No automatic reset (lifetime quota).</summary>
    Lifetime
}
