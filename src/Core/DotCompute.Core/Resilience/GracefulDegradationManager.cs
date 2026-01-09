// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Resilience;

/// <summary>
/// Manages graceful degradation when system resources are constrained.
/// </summary>
/// <remarks>
/// <para>
/// Graceful degradation provides:
/// </para>
/// <list type="bullet">
/// <item><description>Automatic fallback to lower-precision computation</description></item>
/// <item><description>Workload shedding under extreme load</description></item>
/// <item><description>Quality-of-service management</description></item>
/// <item><description>Automatic recovery when resources become available</description></item>
/// </list>
/// </remarks>
public sealed class GracefulDegradationManager : IGracefulDegradationManager, IDisposable
{
    private readonly ILogger<GracefulDegradationManager> _logger;
    private readonly GracefulDegradationOptions _options;
    private readonly ConcurrentDictionary<string, DegradationPolicy> _policies = new();
    private readonly Timer _monitorTimer;
    private DegradationLevel _currentLevel = DegradationLevel.None;
    private SystemHealthMetrics _lastMetrics = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when degradation level changes.
    /// </summary>
    public event EventHandler<DegradationLevelChangedEventArgs>? DegradationLevelChanged;

    /// <summary>
    /// Event raised when a fallback strategy is activated.
    /// </summary>
    public event EventHandler<FallbackActivatedEventArgs>? FallbackActivated;

    /// <summary>
    /// Initializes a new instance of the GracefulDegradationManager.
    /// </summary>
    public GracefulDegradationManager(
        ILogger<GracefulDegradationManager> logger,
        GracefulDegradationOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new GracefulDegradationOptions();

        // Register default policies
        RegisterDefaultPolicies();

        // Start health monitoring
        _monitorTimer = new Timer(
            MonitorSystemHealth,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));

        _logger.LogInformation("GracefulDegradationManager initialized");
    }

    /// <inheritdoc />
    public DegradationLevel CurrentLevel => _currentLevel;

    /// <inheritdoc />
    public ValueTask<DegradationDecision> EvaluateAsync(
        DegradationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var decision = EvaluateInternal(context);

        if (decision.ShouldDegrade)
        {
            _logger.LogWarning(
                "Degradation recommended for '{Operation}': {Strategy} (level: {Level})",
                context.OperationName,
                decision.RecommendedStrategy,
                _currentLevel);
        }

        return ValueTask.FromResult(decision);
    }

    /// <inheritdoc />
    public ValueTask<T> ExecuteWithFallbackAsync<T>(
        Func<CancellationToken, ValueTask<T>> primaryAction,
        Func<CancellationToken, ValueTask<T>> fallbackAction,
        DegradationContext context,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithFallbackInternalAsync(primaryAction, fallbackAction, context, cancellationToken);
    }

    private async ValueTask<T> ExecuteWithFallbackInternalAsync<T>(
        Func<CancellationToken, ValueTask<T>> primaryAction,
        Func<CancellationToken, ValueTask<T>> fallbackAction,
        DegradationContext context,
        CancellationToken cancellationToken)
    {
        var decision = await EvaluateAsync(context, cancellationToken);

        if (decision.ShouldDegrade && decision.RecommendedStrategy == DegradationStrategy.UseFallback)
        {
            OnFallbackActivated(new FallbackActivatedEventArgs(
                context.OperationName ?? "Unknown",
                "System under pressure, using fallback"));

            return await fallbackAction(cancellationToken);
        }

        try
        {
            return await primaryAction(cancellationToken);
        }
        catch (Exception ex) when (ShouldFallback(ex))
        {
            _logger.LogWarning(ex,
                "Primary action failed for '{Operation}', falling back",
                context.OperationName);

            OnFallbackActivated(new FallbackActivatedEventArgs(
                context.OperationName ?? "Unknown",
                $"Primary action failed: {ex.Message}"));

            return await fallbackAction(cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask RegisterPolicyAsync(
        string policyName,
        DegradationPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policyName);
        ArgumentNullException.ThrowIfNull(policy);

        _policies[policyName] = policy;

        _logger.LogInformation(
            "Registered degradation policy '{PolicyName}'",
            policyName);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<DegradationPolicy?> GetPolicyAsync(
        string policyName,
        CancellationToken cancellationToken = default)
    {
        _policies.TryGetValue(policyName, out var policy);
        return ValueTask.FromResult(policy);
    }

    /// <inheritdoc />
    public ValueTask UpdateHealthMetricsAsync(
        SystemHealthMetrics metrics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        _lastMetrics = metrics;
        UpdateDegradationLevel(metrics);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<SystemHealthMetrics> GetHealthMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_lastMetrics);
    }

    private void RegisterDefaultPolicies()
    {
        // High memory pressure policy
        _policies["memory-pressure"] = new DegradationPolicy
        {
            Name = "memory-pressure",
            Triggers =
            [
                new DegradationTrigger
                {
                    MetricName = "memory_usage_percent",
                    Threshold = 85,
                    Operator = TriggerOperator.GreaterThan
                }
            ],
            Strategy = DegradationStrategy.ReducePrecision,
            Priority = 10
        };

        // High GPU utilization policy
        _policies["gpu-pressure"] = new DegradationPolicy
        {
            Name = "gpu-pressure",
            Triggers =
            [
                new DegradationTrigger
                {
                    MetricName = "gpu_utilization_percent",
                    Threshold = 95,
                    Operator = TriggerOperator.GreaterThan
                }
            ],
            Strategy = DegradationStrategy.ThrottleRequests,
            Priority = 20
        };

        // Queue depth policy
        _policies["queue-depth"] = new DegradationPolicy
        {
            Name = "queue-depth",
            Triggers =
            [
                new DegradationTrigger
                {
                    MetricName = "queue_depth",
                    Threshold = 100,
                    Operator = TriggerOperator.GreaterThan
                }
            ],
            Strategy = DegradationStrategy.ShedLoad,
            Priority = 30
        };

        // Error rate policy
        _policies["error-rate"] = new DegradationPolicy
        {
            Name = "error-rate",
            Triggers =
            [
                new DegradationTrigger
                {
                    MetricName = "error_rate_percent",
                    Threshold = 10,
                    Operator = TriggerOperator.GreaterThan
                }
            ],
            Strategy = DegradationStrategy.UseFallback,
            Priority = 40
        };
    }

    private DegradationDecision EvaluateInternal(DegradationContext context)
    {
        // Check current degradation level
        if (_currentLevel == DegradationLevel.None)
        {
            return DegradationDecision.NoAction();
        }

        // Find applicable policy
        var applicablePolicy = FindApplicablePolicy(context);
        if (applicablePolicy == null)
        {
            return DegradationDecision.NoAction();
        }

        // Determine strategy based on level and policy
        var strategy = _currentLevel switch
        {
            DegradationLevel.Minor => applicablePolicy.Strategy,
            DegradationLevel.Moderate => GetEscalatedStrategy(applicablePolicy.Strategy),
            DegradationLevel.Severe => DegradationStrategy.ShedLoad,
            DegradationLevel.Critical => DegradationStrategy.RejectAll,
            _ => DegradationStrategy.None
        };

        return new DegradationDecision
        {
            ShouldDegrade = strategy != DegradationStrategy.None,
            RecommendedStrategy = strategy,
            PolicyName = applicablePolicy.Name,
            Level = _currentLevel,
            Reason = GetDegradationReason()
        };
    }

    private DegradationPolicy? FindApplicablePolicy(DegradationContext context)
    {
        // Check for operation-specific policy
        if (!string.IsNullOrEmpty(context.OperationName) &&
            _policies.TryGetValue(context.OperationName, out var specificPolicy))
        {
            return specificPolicy;
        }

        // Find triggered policies
        var triggeredPolicies = _policies.Values
            .Where(p => IsPolicyTriggered(p))
            .OrderByDescending(p => p.Priority)
            .ToList();

        return triggeredPolicies.FirstOrDefault();
    }

    private bool IsPolicyTriggered(DegradationPolicy policy)
    {
        foreach (var trigger in policy.Triggers)
        {
            var metricValue = GetMetricValue(trigger.MetricName);
            if (metricValue == null) continue;

            var triggered = trigger.Operator switch
            {
                TriggerOperator.GreaterThan => metricValue > trigger.Threshold,
                TriggerOperator.LessThan => metricValue < trigger.Threshold,
                TriggerOperator.Equal => Math.Abs(metricValue.Value - trigger.Threshold) < 0.001,
                _ => false
            };

            if (triggered) return true;
        }

        return false;
    }

    private double? GetMetricValue(string metricName)
    {
        return metricName switch
        {
            "memory_usage_percent" => _lastMetrics.MemoryUsagePercent,
            "gpu_utilization_percent" => _lastMetrics.GpuUtilizationPercent,
            "queue_depth" => _lastMetrics.QueueDepth,
            "error_rate_percent" => _lastMetrics.ErrorRatePercent,
            "latency_ms" => _lastMetrics.AverageLatencyMs,
            _ => null
        };
    }

    private static DegradationStrategy GetEscalatedStrategy(DegradationStrategy baseStrategy)
    {
        return baseStrategy switch
        {
            DegradationStrategy.ReducePrecision => DegradationStrategy.UseFallback,
            DegradationStrategy.ThrottleRequests => DegradationStrategy.ShedLoad,
            DegradationStrategy.UseFallback => DegradationStrategy.ShedLoad,
            DegradationStrategy.ShedLoad => DegradationStrategy.RejectAll,
            _ => DegradationStrategy.ShedLoad
        };
    }

    private string GetDegradationReason()
    {
        var reasons = new List<string>();

        if (_lastMetrics.MemoryUsagePercent > 80)
            reasons.Add($"High memory usage ({_lastMetrics.MemoryUsagePercent:F1}%)");

        if (_lastMetrics.GpuUtilizationPercent > 90)
            reasons.Add($"High GPU utilization ({_lastMetrics.GpuUtilizationPercent:F1}%)");

        if (_lastMetrics.QueueDepth > 50)
            reasons.Add($"High queue depth ({_lastMetrics.QueueDepth})");

        if (_lastMetrics.ErrorRatePercent > 5)
            reasons.Add($"Elevated error rate ({_lastMetrics.ErrorRatePercent:F1}%)");

        return reasons.Count > 0 ? string.Join("; ", reasons) : "System under pressure";
    }

    private static bool ShouldFallback(Exception ex)
    {
        // Determine if exception warrants fallback
        return ex is OutOfMemoryException
            or TimeoutException
            or OperationCanceledException
            or InvalidOperationException;
    }

    private void MonitorSystemHealth(object? state)
    {
        if (_disposed) return;

        // In production, this would collect real metrics
        // Here we just check for level changes based on last metrics
        UpdateDegradationLevel(_lastMetrics);
    }

    private void UpdateDegradationLevel(SystemHealthMetrics metrics)
    {
        var newLevel = CalculateDegradationLevel(metrics);

        if (newLevel != _currentLevel)
        {
            var oldLevel = _currentLevel;
            _currentLevel = newLevel;

            _logger.LogWarning(
                "Degradation level changed: {OldLevel} → {NewLevel}",
                oldLevel, newLevel);

            OnDegradationLevelChanged(new DegradationLevelChangedEventArgs(oldLevel, newLevel));
        }
    }

    private DegradationLevel CalculateDegradationLevel(SystemHealthMetrics metrics)
    {
        // Calculate composite health score (0-100, lower is worse)
        var healthScore = 100.0;

        // Memory impact (30% weight)
        healthScore -= Math.Min(30, metrics.MemoryUsagePercent * 0.3);

        // GPU impact (30% weight)
        healthScore -= Math.Min(30, metrics.GpuUtilizationPercent * 0.3);

        // Queue depth impact (20% weight)
        healthScore -= Math.Min(20, metrics.QueueDepth / 5.0);

        // Error rate impact (20% weight)
        healthScore -= Math.Min(20, metrics.ErrorRatePercent * 2);

        return healthScore switch
        {
            > 80 => DegradationLevel.None,
            > 60 => DegradationLevel.Minor,
            > 40 => DegradationLevel.Moderate,
            > 20 => DegradationLevel.Severe,
            _ => DegradationLevel.Critical
        };
    }

    private void OnDegradationLevelChanged(DegradationLevelChangedEventArgs args) =>
        DegradationLevelChanged?.Invoke(this, args);

    private void OnFallbackActivated(FallbackActivatedEventArgs args) =>
        FallbackActivated?.Invoke(this, args);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _monitorTimer.Dispose();
        _policies.Clear();
    }
}

/// <summary>
/// Interface for graceful degradation management.
/// </summary>
public interface IGracefulDegradationManager
{
    /// <summary>Gets the current degradation level.</summary>
    DegradationLevel CurrentLevel { get; }

    /// <summary>Evaluates whether degradation is needed.</summary>
    ValueTask<DegradationDecision> EvaluateAsync(DegradationContext context, CancellationToken cancellationToken = default);

    /// <summary>Executes with automatic fallback.</summary>
    ValueTask<T> ExecuteWithFallbackAsync<T>(
        Func<CancellationToken, ValueTask<T>> primaryAction,
        Func<CancellationToken, ValueTask<T>> fallbackAction,
        DegradationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Registers a degradation policy.</summary>
    ValueTask RegisterPolicyAsync(string policyName, DegradationPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>Gets a policy by name.</summary>
    ValueTask<DegradationPolicy?> GetPolicyAsync(string policyName, CancellationToken cancellationToken = default);

    /// <summary>Updates health metrics.</summary>
    ValueTask UpdateHealthMetricsAsync(SystemHealthMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>Gets current health metrics.</summary>
    ValueTask<SystemHealthMetrics> GetHealthMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for graceful degradation.
/// </summary>
public sealed class GracefulDegradationOptions
{
    /// <summary>Gets or sets whether automatic recovery is enabled.</summary>
    public bool EnableAutoRecovery { get; set; } = true;

    /// <summary>Gets or sets recovery check interval.</summary>
    public TimeSpan RecoveryCheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets minimum recovery duration before level decrease.</summary>
    public TimeSpan MinRecoveryDuration { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Degradation levels.
/// </summary>
public enum DegradationLevel
{
    /// <summary>No degradation.</summary>
    None = 0,

    /// <summary>Minor degradation - reduce quality.</summary>
    Minor = 1,

    /// <summary>Moderate degradation - use fallbacks.</summary>
    Moderate = 2,

    /// <summary>Severe degradation - shed load.</summary>
    Severe = 3,

    /// <summary>Critical degradation - reject most requests.</summary>
    Critical = 4
}

/// <summary>
/// Degradation strategies.
/// </summary>
public enum DegradationStrategy
{
    /// <summary>No action needed.</summary>
    None,

    /// <summary>Reduce computation precision.</summary>
    ReducePrecision,

    /// <summary>Throttle incoming requests.</summary>
    ThrottleRequests,

    /// <summary>Use fallback implementation.</summary>
    UseFallback,

    /// <summary>Shed non-critical load.</summary>
    ShedLoad,

    /// <summary>Reject all requests.</summary>
    RejectAll
}

/// <summary>
/// Context for degradation evaluation.
/// </summary>
public sealed class DegradationContext
{
    /// <summary>Gets or sets the operation name.</summary>
    public string? OperationName { get; init; }

    /// <summary>Gets or sets the priority.</summary>
    public int Priority { get; init; }

    /// <summary>Gets or sets whether this is a critical operation.</summary>
    public bool IsCritical { get; init; }

    /// <summary>Gets or sets custom context.</summary>
    public IReadOnlyDictionary<string, object>? CustomContext { get; init; }
}

/// <summary>
/// Result of degradation evaluation.
/// </summary>
public sealed class DegradationDecision
{
    /// <summary>Gets whether degradation is recommended.</summary>
    public bool ShouldDegrade { get; init; }

    /// <summary>Gets the recommended strategy.</summary>
    public DegradationStrategy RecommendedStrategy { get; init; }

    /// <summary>Gets the policy that triggered this decision.</summary>
    public string? PolicyName { get; init; }

    /// <summary>Gets the current degradation level.</summary>
    public DegradationLevel Level { get; init; }

    /// <summary>Gets the reason for degradation.</summary>
    public string? Reason { get; init; }

    /// <summary>Creates a no-action decision.</summary>
    public static DegradationDecision NoAction() => new()
    {
        ShouldDegrade = false,
        RecommendedStrategy = DegradationStrategy.None,
        Level = DegradationLevel.None
    };
}

/// <summary>
/// A degradation policy.
/// </summary>
public sealed class DegradationPolicy
{
    /// <summary>Gets or sets the policy name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets triggers for this policy.</summary>
    public required IReadOnlyList<DegradationTrigger> Triggers { get; init; }

    /// <summary>Gets or sets the strategy to apply.</summary>
    public DegradationStrategy Strategy { get; init; }

    /// <summary>Gets or sets the priority (higher = more important).</summary>
    public int Priority { get; init; }
}

/// <summary>
/// A trigger for degradation.
/// </summary>
public sealed class DegradationTrigger
{
    /// <summary>Gets or sets the metric name.</summary>
    public required string MetricName { get; init; }

    /// <summary>Gets or sets the threshold value.</summary>
    public double Threshold { get; init; }

    /// <summary>Gets or sets the comparison operator.</summary>
    public TriggerOperator Operator { get; init; }
}

/// <summary>
/// Trigger comparison operators.
/// </summary>
public enum TriggerOperator
{
    /// <summary>Greater than threshold.</summary>
    GreaterThan,

    /// <summary>Less than threshold.</summary>
    LessThan,

    /// <summary>Equal to threshold.</summary>
    Equal
}

/// <summary>
/// System health metrics.
/// </summary>
public sealed class SystemHealthMetrics
{
    /// <summary>Gets or sets memory usage percentage.</summary>
    public double MemoryUsagePercent { get; init; }

    /// <summary>Gets or sets GPU utilization percentage.</summary>
    public double GpuUtilizationPercent { get; init; }

    /// <summary>Gets or sets queue depth.</summary>
    public int QueueDepth { get; init; }

    /// <summary>Gets or sets error rate percentage.</summary>
    public double ErrorRatePercent { get; init; }

    /// <summary>Gets or sets average latency in milliseconds.</summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>Gets or sets timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event args for degradation level change.
/// </summary>
public sealed class DegradationLevelChangedEventArgs : EventArgs
{
    /// <summary>Gets the previous level.</summary>
    public DegradationLevel PreviousLevel { get; }

    /// <summary>Gets the new level.</summary>
    public DegradationLevel NewLevel { get; }

    /// <summary>Initializes a new instance.</summary>
    public DegradationLevelChangedEventArgs(DegradationLevel previous, DegradationLevel current)
    {
        PreviousLevel = previous;
        NewLevel = current;
    }
}

/// <summary>
/// Event args for fallback activation.
/// </summary>
public sealed class FallbackActivatedEventArgs : EventArgs
{
    /// <summary>Gets the operation name.</summary>
    public string OperationName { get; }

    /// <summary>Gets the reason.</summary>
    public string Reason { get; }

    /// <summary>Initializes a new instance.</summary>
    public FallbackActivatedEventArgs(string operationName, string reason)
    {
        OperationName = operationName;
        Reason = reason;
    }
}
