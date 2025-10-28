// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using DotCompute.Abstractions.Interfaces.Recovery;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
// using DotCompute.Backends.Metal.Telemetry; // Commented out - invalid namespace

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Refactored comprehensive plugin recovery manager that orchestrates specialized recovery components
/// following clean architecture principles
/// </summary>
public sealed class PluginRecoveryManagerRefactored : BaseRecoveryStrategy<PluginRecoveryContext>, IDisposable
{
    private readonly PluginRecoveryOrchestrator _orchestrator;
    private readonly PluginHealthMonitor _healthMonitor;
    private readonly PluginFailureAnalyzer _failureAnalyzer;
    private readonly PluginRecoveryStrategies _recoveryStrategies;
    private readonly PluginCircuitBreaker _circuitBreaker;
    private readonly PluginRecoveryConfiguration _config;
    private readonly ConcurrentDictionary<string, PluginHealthState> _pluginStates;
    private readonly SemaphoreSlim _recoveryLock;
    private bool _disposed;
    /// <summary>
    /// Gets or sets the capability.
    /// </summary>
    /// <value>The capability.</value>

    public override RecoveryCapability Capability => RecoveryCapability.PluginErrors;
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    /// <value>The priority.</value>
    public override int Priority => 80;
    /// <summary>
    /// Initializes a new instance of the PluginRecoveryManagerRefactored class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="orchestrator">The orchestrator.</param>
    /// <param name="healthMonitor">The health monitor.</param>
    /// <param name="failureAnalyzer">The failure analyzer.</param>
    /// <param name="recoveryStrategies">The recovery strategies.</param>
    /// <param name="circuitBreaker">The circuit breaker.</param>
    /// <param name="config">The config.</param>

    public PluginRecoveryManagerRefactored(
        ILogger<PluginRecoveryManagerRefactored> logger,
        PluginRecoveryOrchestrator orchestrator,
        PluginHealthMonitor healthMonitor,
        PluginFailureAnalyzer failureAnalyzer,
        PluginRecoveryStrategies recoveryStrategies,
        PluginCircuitBreaker circuitBreaker,
        PluginRecoveryConfiguration? config = null)
        : base(logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        _failureAnalyzer = failureAnalyzer ?? throw new ArgumentNullException(nameof(failureAnalyzer));
        _recoveryStrategies = recoveryStrategies ?? throw new ArgumentNullException(nameof(recoveryStrategies));
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        _config = config ?? PluginRecoveryConfiguration.Default;
        _pluginStates = new ConcurrentDictionary<string, PluginHealthState>();
        _recoveryLock = new SemaphoreSlim(1, 1);

        Logger.LogInformation("Refactored Plugin Recovery Manager initialized with modular components");
    }
    /// <summary>
    /// Determines whether handle.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="context">The context.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

    public override bool CanHandle(Exception error, PluginRecoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);

        return error switch
        {
            ArgumentException when error.Source?.Contains("plugin", StringComparison.OrdinalIgnoreCase) == true => true,
            ReflectionTypeLoadException => true,
            FileLoadException => true,
            BadImageFormatException => true,
            TargetInvocationException => true,
            AppDomainUnloadedException => true,
            _ when IsPluginRelatedError(error, context) => true,
            _ => false
        };
    }
    /// <summary>
    /// Gets recover asynchronously.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="context">The context.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override async Task<RecoveryResult> RecoverAsync(
        Exception error,
        PluginRecoveryContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        Logger.LogWarning("Plugin error detected for {PluginId}: {Error}", context.PluginId, error.Message);

        // Check circuit breaker state
        if (_circuitBreaker.IsOpen(context.PluginId))
        {
            Logger.LogWarning("Circuit breaker is open for plugin {PluginId}, blocking recovery attempt", context.PluginId);
            return RecoveryResult.CreateFailure("Circuit breaker is open - plugin in failure state");
        }

        await _recoveryLock.WaitAsync(cancellationToken);

        try
        {
            // Get or create plugin health state
            var healthState = _pluginStates.GetOrAdd(context.PluginId,
                id => new PluginHealthState(id, _config));
            healthState.RecordError(error);

            // Update health monitoring
            _healthMonitor.RecordFailure(context.PluginId, error);

            // Determine optimal recovery strategy using the strategies component
            var strategy = await _recoveryStrategies.DetermineOptimalStrategyAsync(
                error, context, healthState, cancellationToken);

            Logger.LogInformation("Using recovery strategy: {Strategy} for {PluginId}", strategy, context.PluginId);

            // Execute recovery through orchestrator
            var result = await _orchestrator.ExecuteRecoveryAsync(
                strategy, context, healthState, cancellationToken);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            // Update health state and circuit breaker based on result
            if (result.Success)
            {
                healthState.RecordSuccessfulRecovery();
                _circuitBreaker.RecordSuccess(context.PluginId);
                _healthMonitor.RecordRecovery(context.PluginId);

                Logger.LogInformation("Plugin recovery successful for {PluginId} using {Strategy} in {Duration}ms",
                    context.PluginId, strategy, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                healthState.RecordFailedRecovery();
                _circuitBreaker.RecordFailure(context.PluginId);

                Logger.LogError("Plugin recovery failed for {PluginId} using {Strategy}: {Message}",
                    context.PluginId, strategy, result.Message);
            }

            return result;
        }
        finally
        {
            _ = _recoveryLock.Release();
        }
    }

    /// <summary>
    /// Isolates a plugin in its own container for crash protection
    /// </summary>
    public async Task<IsolatedPluginContainer> IsolatePluginAsync(
        string pluginId,
        IBackendPlugin plugin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(plugin);

        Logger.LogInformation("Delegating plugin isolation to recovery strategies for {PluginId}", pluginId);

        var context = new PluginRecoveryContext(pluginId, plugin);
        var isolateResult = await _recoveryStrategies.ExecuteIsolateStrategyAsync(context, cancellationToken);

        if (!isolateResult.Success)
        {
            throw new InvalidOperationException($"Failed to isolate plugin: {isolateResult.Message}");
        }

        // The container would be tracked within the strategies component
        // For now, return a new container (in real implementation, get from strategies)
        var container = new IsolatedPluginContainer(pluginId, plugin, Logger, _config);
        await container.InitializeAsync(cancellationToken);

        var healthState = _pluginStates.GetOrAdd(pluginId, id => new PluginHealthState(id, _config));
        healthState.SetIsolated();

        return container;
    }

    /// <summary>
    /// Restarts a failed plugin with proper cleanup
    /// </summary>
    public async Task<bool> RestartPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        Logger.LogInformation("Delegating plugin restart to recovery strategies for {PluginId}", pluginId);

        var context = new PluginRecoveryContext(pluginId);
        var restartResult = await _recoveryStrategies.ExecuteRestartStrategyAsync(context, cancellationToken);

        if (restartResult.Success)
        {
            var healthState = _pluginStates.GetOrAdd(pluginId, id => new PluginHealthState(id, _config));
            healthState.RecordRestart();
            _healthMonitor.RecordRecovery(pluginId);
        }

        return restartResult.Success;
    }

    /// <summary>
    /// Gets comprehensive health information for all monitored plugins
    /// </summary>
    public PluginHealthReport GetHealthReport()
    {
        var healthReport = PluginHealthMonitor.GenerateHealthReport(_pluginStates.Values);
        var pluginHealth = new Dictionary<string, PluginHealthInfo>();

        foreach (var kvp in _pluginStates)
        {
            var pluginId = kvp.Key;
            var state = kvp.Value;

            pluginHealth[pluginId] = new PluginHealthInfo
            {
                PluginId = pluginId,
                IsHealthy = state.IsHealthy,
                IsIsolated = state.IsIsolated,
                ErrorCount = state.ErrorCount,
                RestartCount = state.RestartCount,
                LastError = state.GetRecentErrors().LastOrDefault(),
                LastErrorTime = state.LastError,
                LastRestart = state.LastRestart,
                LastHealthCheck = state.LastHealthCheck,
                ConsecutiveFailures = state.ConsecutiveFailures,
                UptimePercent = state.CalculateUptimePercent()
            };
        }

        // Combine with circuit breaker status
        foreach (var pluginId in pluginHealth.Keys.ToList())
        {
            var info = pluginHealth[pluginId];
            var circuitBreakerState = _circuitBreaker.GetState(pluginId);

            // Update health info with circuit breaker data
            if (circuitBreakerState?.IsOpen == true)
            {
                info.CustomMetrics["CircuitBreakerOpen"] = true;
                info.CustomMetrics["CircuitBreakerFailureCount"] = circuitBreakerState.FailureCount;
            }

            pluginHealth[pluginId] = info;
        }

        return new PluginHealthReport
        {
            PluginId = "All Plugins",
            Status = pluginHealth.Values.All(p => p.IsHealthy)
                ? PluginHealthStatus.Healthy
                : PluginHealthStatus.Warning,
            Timestamp = DateTimeOffset.UtcNow,
            MemoryUsageBytes = pluginHealth.Values.Sum(p => p.MemoryUsageBytes),
            CpuUsagePercent = pluginHealth.Values.Where(p => p.CpuUsagePercent > 0)
                .DefaultIfEmpty(new PluginHealthInfo()).Average(p => p.CpuUsagePercent),
            ActiveOperations = pluginHealth.Values.Sum(p => p.ActiveOperations),
            OverallHealth = CalculateOverallHealth(pluginHealth),
            Metrics = new Dictionary<string, object>
            {
                ["PluginHealth"] = pluginHealth,
                ["TotalPlugins"] = _pluginStates.Count,
                ["HealthyPlugins"] = pluginHealth.Values.Count(p => p.IsHealthy),
                ["IsolatedPlugins"] = pluginHealth.Values.Count(p => p.IsIsolated),
                ["CircuitBreakersOpen"] = pluginHealth.Values.Count(p =>
                    p.CustomMetrics.ContainsKey("CircuitBreakerOpen")),
                ["StrategyEffectiveness"] = _recoveryStrategies.GetStrategyEffectiveness()
            }
        };
    }

    /// <summary>
    /// Performs emergency shutdown of a crashed plugin
    /// </summary>
    public async Task<bool> EmergencyShutdownAsync(
        string pluginId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(reason);

        Logger.LogCritical("Performing emergency shutdown of plugin {PluginId}: {Reason}", pluginId, reason);

        // Mark as unhealthy immediately
        if (_pluginStates.TryGetValue(pluginId, out var healthState))
        {
            healthState.SetEmergencyShutdown();
        }

        // Open circuit breaker to prevent further operations
        _circuitBreaker.ForceOpen(pluginId, reason);

        // Delegate to recovery strategies
        var context = new PluginRecoveryContext(pluginId);
        var shutdownResult = await _recoveryStrategies.ExecuteShutdownStrategyAsync(
            context, reason, cancellationToken);

        if (shutdownResult.Success)
        {
            _healthMonitor.RecordShutdown(pluginId, reason);
            Logger.LogWarning("Emergency shutdown completed for plugin {PluginId}", pluginId);
        }
        else
        {
            Logger.LogError("Emergency shutdown failed for plugin {PluginId}: {Error}",
                pluginId, shutdownResult.Message);
        }

        return shutdownResult.Success;
    }

    /// <summary>
    /// Gets failure analysis for a specific plugin
    /// </summary>
    public async Task<FailureAnalysisResult> GetFailureAnalysisAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        return await _failureAnalyzer.AnalyzePluginFailuresAsync(pluginId, cancellationToken);
    }

    /// <summary>
    /// Gets failure prediction for a specific plugin
    /// </summary>
    public FailurePrediction PredictFailureRisk(string pluginId, TimeSpan lookAhead)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        return _failureAnalyzer.PredictFailureRisk(pluginId, lookAhead);
    }

    /// <summary>
    /// Gets the most effective recovery strategy for a specific plugin
    /// </summary>
    public PluginRecoveryStrategy? GetRecommendedStrategy(string pluginId)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        return _recoveryStrategies.GetMostEffectiveStrategy(pluginId);
    }

    /// <summary>
    /// Gets circuit breaker status for a specific plugin
    /// </summary>
    public CircuitBreakerState? GetCircuitBreakerStatus(string pluginId)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        var status = _circuitBreaker.GetState(pluginId);
        if (status == null) return null;

        return new CircuitBreakerState
        {
            PluginId = status.PluginId,
            State = status.State,
            ConsecutiveFailures = status.ConsecutiveFailures,
            TotalFailures = status.TotalFailures,
            LastFailureTime = status.LastFailureTime,
            LastSuccessTime = status.LastSuccessTime,
            OpenTime = status.OpenTime,
            HalfOpenAttempts = status.HalfOpenAttempts
        };
    }

    private static bool IsPluginRelatedError(Exception error, PluginRecoveryContext context)
    {
        var message = error.Message.ToLowerInvariant();
        return message.Contains("plugin", StringComparison.CurrentCulture) ||
               message.Contains(context.PluginId.ToLowerInvariant(), StringComparison.CurrentCulture) ||
               error.StackTrace?.Contains("plugin", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static double CalculateOverallHealth(Dictionary<string, PluginHealthInfo> pluginHealth)
    {
        if (pluginHealth.Count == 0)
        {
            return 1.0;
        }


        var healthyCount = pluginHealth.Values.Count(p => p.IsHealthy);
        return (double)healthyCount / pluginHealth.Count;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _recoveryLock?.Dispose();
            _orchestrator?.Dispose();
            _healthMonitor?.Dispose();
            _failureAnalyzer?.Dispose();
            _recoveryStrategies?.Dispose();
            _circuitBreaker?.Dispose();

            _disposed = true;
            Logger.LogInformation("Refactored Plugin Recovery Manager disposed");
        }
    }
}