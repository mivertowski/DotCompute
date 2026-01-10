// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.MultiTenancy;

/// <summary>
/// Represents a tenant in a multi-tenant compute environment.
/// </summary>
public interface ITenant
{
    /// <summary>
    /// Gets the unique tenant identifier.
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the tenant display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether the tenant is currently active.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Gets the tenant's resource quota.
    /// </summary>
    public TenantQuota Quota { get; }

    /// <summary>
    /// Gets the tenant's current resource usage.
    /// </summary>
    public TenantUsage CurrentUsage { get; }

    /// <summary>
    /// Gets optional metadata associated with the tenant.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; }
}

/// <summary>
/// Resource quota limits for a tenant.
/// </summary>
public sealed record TenantQuota
{
    /// <summary>
    /// Maximum memory allocation in bytes.
    /// </summary>
    public long MaxMemoryBytes { get; init; } = long.MaxValue;

    /// <summary>
    /// Maximum GPU memory allocation in bytes.
    /// </summary>
    public long MaxGpuMemoryBytes { get; init; } = long.MaxValue;

    /// <summary>
    /// Maximum concurrent kernel executions.
    /// </summary>
    public int MaxConcurrentKernels { get; init; } = 100;

    /// <summary>
    /// Maximum kernel execution time.
    /// </summary>
    public TimeSpan MaxKernelExecutionTime { get; init; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Maximum number of accelerators the tenant can use.
    /// </summary>
    public int MaxAccelerators { get; init; } = int.MaxValue;

    /// <summary>
    /// Maximum compute operations per second.
    /// </summary>
    public double MaxOperationsPerSecond { get; init; } = double.MaxValue;

    /// <summary>
    /// Priority level for resource scheduling (higher = more priority).
    /// </summary>
    public int Priority { get; init; } = 50;

    /// <summary>
    /// Whether the tenant is allowed to use GPU resources.
    /// </summary>
    public bool AllowGpuAccess { get; init; } = true;

    /// <summary>
    /// Whether the tenant is allowed to use multi-GPU P2P transfers.
    /// </summary>
    public bool AllowP2PTransfers { get; init; } = true;

    /// <summary>
    /// Default quota with no restrictions.
    /// </summary>
    public static TenantQuota Unlimited { get; } = new();

    /// <summary>
    /// Creates a restricted quota for trial/limited tenants.
    /// </summary>
    public static TenantQuota Trial { get; } = new()
    {
        MaxMemoryBytes = 1L * 1024 * 1024 * 1024, // 1 GB
        MaxGpuMemoryBytes = 512L * 1024 * 1024, // 512 MB
        MaxConcurrentKernels = 5,
        MaxKernelExecutionTime = TimeSpan.FromMinutes(5),
        MaxAccelerators = 1,
        MaxOperationsPerSecond = 1000,
        Priority = 10,
        AllowP2PTransfers = false
    };
}

/// <summary>
/// Current resource usage for a tenant.
/// </summary>
public sealed record TenantUsage
{
    /// <summary>
    /// Current memory allocation in bytes.
    /// </summary>
    public long CurrentMemoryBytes { get; init; }

    /// <summary>
    /// Current GPU memory allocation in bytes.
    /// </summary>
    public long CurrentGpuMemoryBytes { get; init; }

    /// <summary>
    /// Current number of active kernels.
    /// </summary>
    public int ActiveKernels { get; init; }

    /// <summary>
    /// Current number of accelerators in use.
    /// </summary>
    public int AcceleratorsInUse { get; init; }

    /// <summary>
    /// Operations executed in the current period.
    /// </summary>
    public long OperationsExecuted { get; init; }

    /// <summary>
    /// Total compute time used in the current billing period.
    /// </summary>
    public TimeSpan TotalComputeTime { get; init; }

    /// <summary>
    /// Last activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivity { get; init; }

    /// <summary>
    /// Empty usage.
    /// </summary>
    public static TenantUsage Empty { get; } = new();
}

/// <summary>
/// Manages tenant isolation and resource allocation.
/// </summary>
public interface ITenantManager : IAsyncDisposable
{
    /// <summary>
    /// Registers a new tenant.
    /// </summary>
    /// <param name="tenantId">Unique tenant identifier.</param>
    /// <param name="name">Tenant display name.</param>
    /// <param name="quota">Optional quota (defaults to unlimited).</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created tenant.</returns>
    public Task<ITenant> RegisterTenantAsync(
        string tenantId,
        string name,
        TenantQuota? quota = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant, or null if not found.</returns>
    public Task<ITenant?> GetTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a tenant's quota.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="quota">New quota configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task UpdateQuotaAsync(
        string tenantId,
        TenantQuota quota,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a tenant, releasing all resources.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeactivateTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously deactivated tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ReactivateTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a tenant completely.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task RemoveTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all registered tenants.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive tenants.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tenants.</returns>
    public Task<IReadOnlyList<ITenant>> ListTenantsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage statistics for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current usage statistics.</returns>
    public Task<TenantUsage> GetUsageAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a tenant can allocate the requested resources.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="request">The resource request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    public Task<TenantResourceValidation> ValidateResourceRequestAsync(
        string tenantId,
        TenantResourceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Occurs when a tenant exceeds their quota.
    /// </summary>
    public event EventHandler<TenantQuotaExceededEventArgs>? QuotaExceeded;

    /// <summary>
    /// Occurs when a tenant is registered.
    /// </summary>
    public event EventHandler<TenantEventArgs>? TenantRegistered;

    /// <summary>
    /// Occurs when a tenant is deactivated.
    /// </summary>
    public event EventHandler<TenantEventArgs>? TenantDeactivated;
}

/// <summary>
/// Resource request for validation.
/// </summary>
public sealed record TenantResourceRequest
{
    /// <summary>
    /// Requested memory in bytes.
    /// </summary>
    public long MemoryBytes { get; init; }

    /// <summary>
    /// Requested GPU memory in bytes.
    /// </summary>
    public long GpuMemoryBytes { get; init; }

    /// <summary>
    /// Number of kernels to launch.
    /// </summary>
    public int KernelCount { get; init; }

    /// <summary>
    /// Number of accelerators needed.
    /// </summary>
    public int AcceleratorCount { get; init; }

    /// <summary>
    /// Estimated execution time.
    /// </summary>
    public TimeSpan EstimatedDuration { get; init; }
}

/// <summary>
/// Result of resource validation.
/// </summary>
public sealed record TenantResourceValidation
{
    /// <summary>
    /// Whether the request is allowed.
    /// </summary>
    public required bool IsAllowed { get; init; }

    /// <summary>
    /// Reason for denial if not allowed.
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// Which quota was exceeded.
    /// </summary>
    public TenantQuotaType? ExceededQuota { get; init; }

    /// <summary>
    /// Current value of exceeded quota.
    /// </summary>
    public long? CurrentValue { get; init; }

    /// <summary>
    /// Limit value of exceeded quota.
    /// </summary>
    public long? LimitValue { get; init; }

    /// <summary>
    /// Suggested wait time if rate limited.
    /// </summary>
    public TimeSpan? SuggestedWait { get; init; }

    /// <summary>
    /// Creates an allowed result.
    /// </summary>
    public static TenantResourceValidation Allowed { get; } = new() { IsAllowed = true };

    /// <summary>
    /// Creates a denied result.
    /// </summary>
    public static TenantResourceValidation Denied(
        string reason,
        TenantQuotaType quotaType,
        long current,
        long limit) => new()
    {
        IsAllowed = false,
        DenialReason = reason,
        ExceededQuota = quotaType,
        CurrentValue = current,
        LimitValue = limit
    };
}

/// <summary>
/// Types of tenant quotas.
/// </summary>
public enum TenantQuotaType
{
    /// <summary>CPU/Host memory.</summary>
    Memory,
    /// <summary>GPU memory.</summary>
    GpuMemory,
    /// <summary>Concurrent kernel limit.</summary>
    ConcurrentKernels,
    /// <summary>Kernel execution time.</summary>
    ExecutionTime,
    /// <summary>Accelerator count.</summary>
    Accelerators,
    /// <summary>Operations per second.</summary>
    OperationsPerSecond
}

/// <summary>
/// Event args for quota exceeded events.
/// </summary>
public sealed class TenantQuotaExceededEventArgs : EventArgs
{
    /// <summary>
    /// Gets the tenant ID.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the quota type that was exceeded.
    /// </summary>
    public required TenantQuotaType QuotaType { get; init; }

    /// <summary>
    /// Gets the current usage value.
    /// </summary>
    public required long CurrentValue { get; init; }

    /// <summary>
    /// Gets the limit value.
    /// </summary>
    public required long LimitValue { get; init; }

    /// <summary>
    /// Gets when the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Event args for tenant lifecycle events.
/// </summary>
public sealed class TenantEventArgs : EventArgs
{
    /// <summary>
    /// Gets the tenant.
    /// </summary>
    public required ITenant Tenant { get; init; }

    /// <summary>
    /// Gets when the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Provides tenant context for the current execution scope.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant, or null if not in a tenant context.
    /// </summary>
    public ITenant? CurrentTenant { get; }

    /// <summary>
    /// Gets whether a tenant context is active.
    /// </summary>
    public bool HasTenant { get; }

    /// <summary>
    /// Sets the current tenant for the scope.
    /// </summary>
    /// <param name="tenant">The tenant to set.</param>
    /// <returns>Disposable scope that resets the tenant when disposed.</returns>
    public IDisposable SetTenant(ITenant tenant);
}

/// <summary>
/// Isolated execution environment for a tenant.
/// </summary>
public interface ITenantExecutionEnvironment : IAsyncDisposable
{
    /// <summary>
    /// Gets the owning tenant.
    /// </summary>
    public ITenant Tenant { get; }

    /// <summary>
    /// Gets the environment identifier.
    /// </summary>
    public string EnvironmentId { get; }

    /// <summary>
    /// Gets whether the environment is active.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Gets the allocated accelerators for this environment.
    /// </summary>
    public IReadOnlyList<IAccelerator> Accelerators { get; }

    /// <summary>
    /// Gets the memory manager for this environment.
    /// </summary>
    public IUnifiedMemoryManager MemoryManager { get; }

    /// <summary>
    /// Allocates an accelerator for this environment.
    /// </summary>
    /// <param name="type">Preferred accelerator type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The allocated accelerator.</returns>
    public Task<IAccelerator> AllocateAcceleratorAsync(
        AcceleratorType type = AcceleratorType.Auto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an accelerator back to the pool.
    /// </summary>
    /// <param name="accelerator">The accelerator to release.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ReleaseAcceleratorAsync(
        IAccelerator accelerator,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current resource usage for this environment.
    /// </summary>
    public TenantUsage GetUsage();

    /// <summary>
    /// Suspends execution in this environment.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SuspendAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes execution in this environment.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ResumeAsync(CancellationToken cancellationToken = default);
}
