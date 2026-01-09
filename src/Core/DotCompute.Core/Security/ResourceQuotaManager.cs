// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Manages resource quotas for principals, tracking usage and enforcing limits.
/// </summary>
/// <remarks>
/// <para>
/// The resource quota manager provides:
/// </para>
/// <list type="bullet">
/// <item><description>Per-principal quota tracking</description></item>
/// <item><description>Real-time usage monitoring</description></item>
/// <item><description>Quota enforcement with configurable policies</description></item>
/// <item><description>Automatic quota reset based on period</description></item>
/// <item><description>Warning thresholds and notifications</description></item>
/// </list>
/// </remarks>
public sealed class ResourceQuotaManager : IResourceQuotaManager, IDisposable
{
    private readonly ILogger<ResourceQuotaManager> _logger;
    private readonly ConcurrentDictionary<string, QuotaState> _quotaStates = new();
    private readonly ConcurrentDictionary<string, ResourceLimits> _defaultLimits = new();
    private readonly Timer _resetTimer;
    private readonly ResourceQuotaOptions _options;
    private bool _disposed;

    /// <summary>
    /// Event raised when a quota warning threshold is reached.
    /// </summary>
    public event EventHandler<QuotaWarningEventArgs>? QuotaWarning;

    /// <summary>
    /// Event raised when a quota limit is exceeded.
    /// </summary>
    public event EventHandler<QuotaExceededEventArgs>? QuotaExceeded;

    /// <summary>
    /// Initializes a new instance of the ResourceQuotaManager.
    /// </summary>
    public ResourceQuotaManager(
        ILogger<ResourceQuotaManager> logger,
        ResourceQuotaOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ResourceQuotaOptions();

        // Start periodic quota reset timer
        _resetTimer = new Timer(
            CheckQuotaResets,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));

        _logger.LogInformation("ResourceQuotaManager initialized with {Period} quota periods",
            _options.DefaultPeriod);
    }

    /// <inheritdoc />
    public ValueTask<ResourceQuota> GetQuotaAsync(
        ISecurityPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var state = GetOrCreateQuotaState(principal);
        var quota = new ResourceQuota
        {
            Principal = principal,
            Limits = state.Limits,
            CurrentUsage = state.GetCurrentUsage(),
            Period = state.Period,
            ResetAt = state.ResetAt
        };

        return ValueTask.FromResult(quota);
    }

    /// <inheritdoc />
    public ValueTask<QuotaCheckResult> CheckQuotaAsync(
        ISecurityPrincipal principal,
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(request);

        var state = GetOrCreateQuotaState(principal);
        var result = CheckQuotaInternal(state, request);

        if (!result.IsAllowed)
        {
            _logger.LogWarning(
                "Quota check failed for principal '{PrincipalId}': {Reason}",
                principal.Id, result.Reason);

            OnQuotaExceeded(new QuotaExceededEventArgs(principal, request, result.Reason ?? "Quota exceeded"));
        }
        else if (result.WarningThresholdReached)
        {
            _logger.LogInformation(
                "Quota warning for principal '{PrincipalId}': {UsagePercent}% used",
                principal.Id, result.UsagePercentage);

            OnQuotaWarning(new QuotaWarningEventArgs(principal, result.UsagePercentage));
        }

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask RecordUsageAsync(
        ISecurityPrincipal principal,
        ResourceUsageRecord usage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(usage);

        var state = GetOrCreateQuotaState(principal);
        state.RecordUsage(usage);

        _logger.LogDebug(
            "Recorded usage for principal '{PrincipalId}': Memory={Memory}, Compute={Compute}ms",
            principal.Id, usage.MemoryBytes, usage.ComputeTimeMs);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ReleaseUsageAsync(
        ISecurityPrincipal principal,
        ResourceReleaseRecord release,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(release);

        var state = GetOrCreateQuotaState(principal);
        state.ReleaseUsage(release);

        _logger.LogDebug(
            "Released usage for principal '{PrincipalId}': Memory={Memory}",
            principal.Id, release.MemoryBytes);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SetLimitsAsync(
        ISecurityPrincipal principal,
        ResourceLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(limits);

        var state = GetOrCreateQuotaState(principal);
        state.Limits = limits;

        _logger.LogInformation(
            "Updated limits for principal '{PrincipalId}': Memory={Memory}, Compute={Compute}ms",
            principal.Id, limits.MaxMemoryBytes, limits.MaxComputeTimeMs);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SetDefaultLimitsAsync(
        string role,
        ResourceLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(limits);

        _defaultLimits[role] = limits;

        _logger.LogInformation(
            "Set default limits for role '{Role}': Memory={Memory}, Compute={Compute}ms",
            role, limits.MaxMemoryBytes, limits.MaxComputeTimeMs);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResetQuotaAsync(
        ISecurityPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (_quotaStates.TryGetValue(principal.Id, out var state))
        {
            state.Reset();
            _logger.LogInformation("Reset quota for principal '{PrincipalId}'", principal.Id);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ResourceQuota>> GetAllQuotasAsync(
        CancellationToken cancellationToken = default)
    {
        var quotas = _quotaStates.Values
            .Select(s => new ResourceQuota
            {
                Principal = s.Principal,
                Limits = s.Limits,
                CurrentUsage = s.GetCurrentUsage(),
                Period = s.Period,
                ResetAt = s.ResetAt
            })
            .ToList();

        return ValueTask.FromResult<IReadOnlyList<ResourceQuota>>(quotas);
    }

    private QuotaState GetOrCreateQuotaState(ISecurityPrincipal principal)
    {
        return _quotaStates.GetOrAdd(principal.Id, _ =>
        {
            var limits = GetDefaultLimitsForPrincipal(principal);
            return new QuotaState(principal, limits, _options.DefaultPeriod);
        });
    }

    private ResourceLimits GetDefaultLimitsForPrincipal(ISecurityPrincipal principal)
    {
        // Check for role-based limits
        foreach (var role in principal.Roles)
        {
            if (_defaultLimits.TryGetValue(role, out var limits))
            {
                return limits;
            }
        }

        // Return global defaults
        return _options.GlobalDefaultLimits;
    }

    private static QuotaCheckResult CheckQuotaInternal(QuotaState state, ResourceRequest request)
    {
        var usage = state.GetCurrentUsage();
        var limits = state.Limits;

        // Check memory limit
        if (limits.MaxMemoryBytes.HasValue && request.MemoryBytes > 0)
        {
            var projectedMemory = usage.MemoryBytesAllocated + request.MemoryBytes;
            if (projectedMemory > limits.MaxMemoryBytes.Value)
            {
                return QuotaCheckResult.Denied(
                    $"Memory quota exceeded: {projectedMemory} > {limits.MaxMemoryBytes.Value}",
                    QuotaType.Memory);
            }
        }

        // Check compute time limit
        if (limits.MaxComputeTimeMs.HasValue)
        {
            var projectedCompute = usage.ComputeTimeUsedMs + request.EstimatedComputeTimeMs;
            if (projectedCompute > limits.MaxComputeTimeMs.Value)
            {
                return QuotaCheckResult.Denied(
                    $"Compute time quota exceeded: {projectedCompute}ms > {limits.MaxComputeTimeMs.Value}ms",
                    QuotaType.ComputeTime);
            }
        }

        // Check concurrent kernels limit
        if (limits.MaxConcurrentKernels.HasValue)
        {
            if (usage.ActiveKernels >= limits.MaxConcurrentKernels.Value)
            {
                return QuotaCheckResult.Denied(
                    $"Concurrent kernel limit reached: {usage.ActiveKernels} >= {limits.MaxConcurrentKernels.Value}",
                    QuotaType.ConcurrentKernels);
            }
        }

        // Calculate usage percentage for warnings
        var usagePercent = CalculateUsagePercentage(usage, limits);
        var warningReached = usagePercent >= 80; // 80% warning threshold

        return QuotaCheckResult.Allowed(usagePercent, warningReached);
    }

    private static double CalculateUsagePercentage(ResourceUsage usage, ResourceLimits limits)
    {
        var percentages = new List<double>();

        if (limits.MaxMemoryBytes.HasValue && limits.MaxMemoryBytes.Value > 0)
        {
            percentages.Add((double)usage.MemoryBytesAllocated / limits.MaxMemoryBytes.Value * 100);
        }

        if (limits.MaxComputeTimeMs.HasValue && limits.MaxComputeTimeMs.Value > 0)
        {
            percentages.Add((double)usage.ComputeTimeUsedMs / limits.MaxComputeTimeMs.Value * 100);
        }

        return percentages.Count > 0 ? percentages.Max() : 0;
    }

    private void CheckQuotaResets(object? state)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var kvp in _quotaStates)
        {
            var quotaState = kvp.Value;
            if (quotaState.ResetAt.HasValue && quotaState.ResetAt.Value <= now)
            {
                quotaState.Reset();
                _logger.LogInformation(
                    "Auto-reset quota for principal '{PrincipalId}'",
                    quotaState.Principal.Id);
            }
        }
    }

    private void OnQuotaWarning(QuotaWarningEventArgs args) =>
        QuotaWarning?.Invoke(this, args);

    private void OnQuotaExceeded(QuotaExceededEventArgs args) =>
        QuotaExceeded?.Invoke(this, args);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _resetTimer.Dispose();
        _quotaStates.Clear();
    }

    /// <summary>
    /// Internal state tracking for a principal's quota.
    /// </summary>
    private sealed class QuotaState
    {
        private long _memoryBytesAllocated;
        private long _computeTimeUsedMs;
        private int _activeKernels;
        private long _peakMemoryBytes;
        private long _totalExecutions;
        private readonly object _lock = new();

        public ISecurityPrincipal Principal { get; }
        public ResourceLimits Limits { get; set; }
        public QuotaPeriod Period { get; }
        public DateTimeOffset? ResetAt { get; private set; }

        public QuotaState(ISecurityPrincipal principal, ResourceLimits limits, QuotaPeriod period)
        {
            Principal = principal;
            Limits = limits;
            Period = period;
            ResetAt = CalculateNextReset(period);
        }

        public ResourceUsage GetCurrentUsage()
        {
            lock (_lock)
            {
                return new ResourceUsage
                {
                    MemoryBytesAllocated = _memoryBytesAllocated,
                    ComputeTimeUsedMs = _computeTimeUsedMs,
                    ActiveKernels = _activeKernels,
                    PeakMemoryBytes = _peakMemoryBytes,
                    TotalExecutions = _totalExecutions
                };
            }
        }

        public void RecordUsage(ResourceUsageRecord record)
        {
            lock (_lock)
            {
                _memoryBytesAllocated += record.MemoryBytes;
                _computeTimeUsedMs += record.ComputeTimeMs;
                _activeKernels += record.KernelStarted ? 1 : 0;
                _totalExecutions += record.KernelCompleted ? 1 : 0;

                if (_memoryBytesAllocated > _peakMemoryBytes)
                {
                    _peakMemoryBytes = _memoryBytesAllocated;
                }
            }
        }

        public void ReleaseUsage(ResourceReleaseRecord record)
        {
            lock (_lock)
            {
                _memoryBytesAllocated = Math.Max(0, _memoryBytesAllocated - record.MemoryBytes);
                _activeKernels = Math.Max(0, _activeKernels - (record.KernelEnded ? 1 : 0));
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                // Reset periodic counters but keep current allocation tracking
                _computeTimeUsedMs = 0;
                _totalExecutions = 0;
                _peakMemoryBytes = _memoryBytesAllocated;
                ResetAt = CalculateNextReset(Period);
            }
        }

        private static DateTimeOffset? CalculateNextReset(QuotaPeriod period)
        {
            var now = DateTimeOffset.UtcNow;
            return period switch
            {
                QuotaPeriod.Hourly => now.AddHours(1),
                QuotaPeriod.Daily => now.Date.AddDays(1),
                QuotaPeriod.Weekly => now.Date.AddDays(7 - (int)now.DayOfWeek),
                QuotaPeriod.Monthly => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset).AddMonths(1),
                QuotaPeriod.Lifetime => null,
                _ => null
            };
        }
    }
}

/// <summary>
/// Interface for resource quota management.
/// </summary>
public interface IResourceQuotaManager
{
    /// <summary>Gets the quota for a principal.</summary>
    ValueTask<ResourceQuota> GetQuotaAsync(ISecurityPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>Checks if a resource request is within quota.</summary>
    ValueTask<QuotaCheckResult> CheckQuotaAsync(ISecurityPrincipal principal, ResourceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Records resource usage.</summary>
    ValueTask RecordUsageAsync(ISecurityPrincipal principal, ResourceUsageRecord usage, CancellationToken cancellationToken = default);

    /// <summary>Releases resource usage.</summary>
    ValueTask ReleaseUsageAsync(ISecurityPrincipal principal, ResourceReleaseRecord release, CancellationToken cancellationToken = default);

    /// <summary>Sets limits for a principal.</summary>
    ValueTask SetLimitsAsync(ISecurityPrincipal principal, ResourceLimits limits, CancellationToken cancellationToken = default);

    /// <summary>Sets default limits for a role.</summary>
    ValueTask SetDefaultLimitsAsync(string role, ResourceLimits limits, CancellationToken cancellationToken = default);

    /// <summary>Resets quota for a principal.</summary>
    ValueTask ResetQuotaAsync(ISecurityPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>Gets all quotas.</summary>
    ValueTask<IReadOnlyList<ResourceQuota>> GetAllQuotasAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for the resource quota manager.
/// </summary>
public sealed class ResourceQuotaOptions
{
    /// <summary>Gets or sets the default quota period.</summary>
    public QuotaPeriod DefaultPeriod { get; set; } = QuotaPeriod.Monthly;

    /// <summary>Gets or sets the global default limits.</summary>
    public ResourceLimits GlobalDefaultLimits { get; set; } = new()
    {
        MaxMemoryBytes = 4L * 1024 * 1024 * 1024, // 4 GB
        MaxComputeTimeMs = 3600000, // 1 hour
        MaxConcurrentKernels = 8,
        MaxUtilizationPercent = 80
    };

    /// <summary>Gets or sets the warning threshold percentage.</summary>
    public int WarningThresholdPercent { get; set; } = 80;
}

/// <summary>
/// A request for resources.
/// </summary>
public sealed class ResourceRequest
{
    /// <summary>Gets or sets the requested memory in bytes.</summary>
    public long MemoryBytes { get; init; }

    /// <summary>Gets or sets the estimated compute time in milliseconds.</summary>
    public long EstimatedComputeTimeMs { get; init; }

    /// <summary>Gets or sets whether a new kernel will be started.</summary>
    public bool StartsKernel { get; init; }
}

/// <summary>
/// A record of resource usage.
/// </summary>
public sealed class ResourceUsageRecord
{
    /// <summary>Gets or sets the memory allocated in bytes.</summary>
    public long MemoryBytes { get; init; }

    /// <summary>Gets or sets the compute time used in milliseconds.</summary>
    public long ComputeTimeMs { get; init; }

    /// <summary>Gets or sets whether a kernel was started.</summary>
    public bool KernelStarted { get; init; }

    /// <summary>Gets or sets whether a kernel was completed.</summary>
    public bool KernelCompleted { get; init; }
}

/// <summary>
/// A record of resource release.
/// </summary>
public sealed class ResourceReleaseRecord
{
    /// <summary>Gets or sets the memory released in bytes.</summary>
    public long MemoryBytes { get; init; }

    /// <summary>Gets or sets whether a kernel ended.</summary>
    public bool KernelEnded { get; init; }
}

/// <summary>
/// Result of a quota check.
/// </summary>
public sealed class QuotaCheckResult
{
    /// <summary>Gets whether the request is allowed.</summary>
    public bool IsAllowed { get; init; }

    /// <summary>Gets the reason if denied.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the quota type that was exceeded.</summary>
    public QuotaType? ExceededQuotaType { get; init; }

    /// <summary>Gets the current usage percentage.</summary>
    public double UsagePercentage { get; init; }

    /// <summary>Gets whether warning threshold was reached.</summary>
    public bool WarningThresholdReached { get; init; }

    /// <summary>Creates an allowed result.</summary>
    public static QuotaCheckResult Allowed(double usagePercent, bool warningReached) => new()
    {
        IsAllowed = true,
        UsagePercentage = usagePercent,
        WarningThresholdReached = warningReached
    };

    /// <summary>Creates a denied result.</summary>
    public static QuotaCheckResult Denied(string reason, QuotaType quotaType) => new()
    {
        IsAllowed = false,
        Reason = reason,
        ExceededQuotaType = quotaType
    };
}

/// <summary>
/// Types of quotas.
/// </summary>
public enum QuotaType
{
    /// <summary>Memory quota.</summary>
    Memory,

    /// <summary>Compute time quota.</summary>
    ComputeTime,

    /// <summary>Concurrent kernels quota.</summary>
    ConcurrentKernels,

    /// <summary>Bandwidth quota.</summary>
    Bandwidth,

    /// <summary>GPU utilization quota.</summary>
    Utilization
}

/// <summary>
/// Event args for quota warning.
/// </summary>
public sealed class QuotaWarningEventArgs : EventArgs
{
    /// <summary>Gets the principal.</summary>
    public ISecurityPrincipal Principal { get; }

    /// <summary>Gets the usage percentage.</summary>
    public double UsagePercentage { get; }

    /// <summary>Initializes a new instance.</summary>
    public QuotaWarningEventArgs(ISecurityPrincipal principal, double usagePercentage)
    {
        Principal = principal;
        UsagePercentage = usagePercentage;
    }
}

/// <summary>
/// Event args for quota exceeded.
/// </summary>
public sealed class QuotaExceededEventArgs : EventArgs
{
    /// <summary>Gets the principal.</summary>
    public ISecurityPrincipal Principal { get; }

    /// <summary>Gets the request that was denied.</summary>
    public ResourceRequest Request { get; }

    /// <summary>Gets the reason.</summary>
    public string Reason { get; }

    /// <summary>Initializes a new instance.</summary>
    public QuotaExceededEventArgs(ISecurityPrincipal principal, ResourceRequest request, string reason)
    {
        Principal = principal;
        Request = request;
        Reason = reason;
    }
}
