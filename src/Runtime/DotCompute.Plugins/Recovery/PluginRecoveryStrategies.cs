// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DotCompute.Core.Recovery;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using RecoveryResult = DotCompute.Abstractions.Interfaces.Recovery.RecoveryResult;

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Implements various recovery strategies for plugin failures with adaptive strategy selection
/// </summary>
public sealed class PluginRecoveryStrategies : IDisposable
{
    private readonly ILogger<PluginRecoveryStrategies> _logger;
    private readonly PluginRecoveryConfiguration _config;
    private readonly PluginFailureAnalyzer _failureAnalyzer;
    private readonly ConcurrentDictionary<string, StrategyEffectiveness> _strategyMetrics;
    private readonly ConcurrentDictionary<string, IsolatedPluginContainer> _isolatedPlugins;
    private readonly SemaphoreSlim _recoveryLock;
    private bool _disposed;

    public PluginRecoveryStrategies(
        ILogger<PluginRecoveryStrategies> logger,
        PluginFailureAnalyzer failureAnalyzer,
        PluginRecoveryConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _failureAnalyzer = failureAnalyzer ?? throw new ArgumentNullException(nameof(failureAnalyzer));
        _config = config ?? PluginRecoveryConfiguration.Default;
        _strategyMetrics = new ConcurrentDictionary<string, StrategyEffectiveness>();
        _isolatedPlugins = new ConcurrentDictionary<string, IsolatedPluginContainer>();
        _recoveryLock = new SemaphoreSlim(1, 1);

        _logger.LogInformation("Plugin Recovery Strategies initialized with adaptive selection enabled");
    }

    /// <summary>
    /// Determines the optimal recovery strategy based on failure analysis and historical effectiveness
    /// </summary>
    public async Task<PluginRecoveryStrategy> DetermineOptimalStrategyAsync(
        Exception error,
        PluginRecoveryContext context,
        PluginHealthState healthState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(healthState);

        // Record failure for analysis
        _failureAnalyzer.RecordFailure(context.PluginId, error, context);

        // Get failure analysis
        var analysis = await _failureAnalyzer.AnalyzePluginFailuresAsync(context.PluginId, cancellationToken);

        // Check for critical conditions requiring immediate action
        if (healthState.ConsecutiveFailures >= _config.MaxConsecutiveFailures)
        {
            return _config.EnablePluginIsolation
                ? PluginRecoveryStrategy.IsolatePlugin
                : PluginRecoveryStrategy.ShutdownPlugin;
        }

        // Emergency conditions
        if (IsEmergencyCondition(error, analysis))
        {
            return PluginRecoveryStrategy.ShutdownPlugin;
        }

        // Determine strategy based on error type and historical effectiveness
        var candidates = GetStrategyCandidates(error, context, healthState, analysis);
        var optimalStrategy = await SelectOptimalStrategyAsync(context.PluginId, candidates, cancellationToken);

        _logger.LogInformation("Selected recovery strategy {Strategy} for plugin {PluginId} based on analysis (confidence: {Confidence:F2})",
            optimalStrategy, context.PluginId, analysis.Confidence);

        return optimalStrategy;
    }

    /// <summary>
    /// Executes the restart plugin recovery strategy
    /// </summary>
    public async Task<RecoveryResult> ExecuteRestartStrategyAsync(
        PluginRecoveryContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var strategyKey = $"{context.PluginId}-Restart";

        _logger.LogInformation("Executing restart strategy for plugin {PluginId}", context.PluginId);

        try
        {
            // Stop the plugin gracefully
            var stopResult = await StopPluginAsync(context.PluginId, cancellationToken);
            if (!stopResult)
            {
                _logger.LogWarning("Graceful stop failed for plugin {PluginId}, forcing termination", context.PluginId);
                await ForceStopPluginAsync(context.PluginId, cancellationToken);
            }

            // Wait for cleanup
            await Task.Delay(_config.RestartDelay, cancellationToken);

            // Start the plugin
            var startResult = await StartPluginAsync(context.PluginId, cancellationToken);

            stopwatch.Stop();

            var result = startResult
                ? new RecoveryResult { Success = true, Message = $"Plugin {context.PluginId} restarted successfully", Duration = stopwatch.Elapsed }
                : new RecoveryResult { Success = false, Message = $"Failed to restart plugin {context.PluginId}" };

            RecordStrategyResult(strategyKey, result.Success, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new RecoveryResult { Success = false, Message = $"Restart strategy failed: {ex.Message}", Exception = ex };
            RecordStrategyResult(strategyKey, false, stopwatch.Elapsed);
            return result;
        }
    }

    /// <summary>
    /// Executes the reload plugin recovery strategy
    /// </summary>
    public async Task<RecoveryResult> ExecuteReloadStrategyAsync(
        PluginRecoveryContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var strategyKey = $"{context.PluginId}-Reload";

        _logger.LogInformation("Executing reload strategy for plugin {PluginId}", context.PluginId);

        try
        {
            // Unload current version
            var unloadResult = await UnloadPluginAsync(context.PluginId, cancellationToken);
            if (!unloadResult)
            {
                _logger.LogWarning("Plugin unload failed for {PluginId}, proceeding with reload", context.PluginId);
            }

            // Clear any cached assemblies
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Reload from disk
            var reloadResult = await LoadPluginAsync(context.PluginId, context.PluginPath, cancellationToken);

            stopwatch.Stop();

            var result = reloadResult
                ? new RecoveryResult { Success = true, Message = $"Plugin {context.PluginId} reloaded successfully", Duration = stopwatch.Elapsed }
                : new RecoveryResult { Success = false, Message = $"Failed to reload plugin {context.PluginId}" };

            RecordStrategyResult(strategyKey, result.Success, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new RecoveryResult { Success = false, Message = $"Reload strategy failed: {ex.Message}", Exception = ex };
            RecordStrategyResult(strategyKey, false, stopwatch.Elapsed);
            return result;
        }
    }

    /// <summary>
    /// Executes the isolate plugin recovery strategy
    /// </summary>
    public async Task<RecoveryResult> ExecuteIsolateStrategyAsync(
        PluginRecoveryContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var strategyKey = $"{context.PluginId}-Isolate";

        if (context.Plugin is not IBackendPlugin plugin)
        {
            return new RecoveryResult { Success = false, Message = "Cannot isolate plugin - plugin instance not available or invalid type" };
        }

        _logger.LogInformation("Executing isolation strategy for plugin {PluginId}", context.PluginId);

        try
        {
            var container = new IsolatedPluginContainer(context.PluginId, plugin, _logger, _config);
            await container.InitializeAsync(cancellationToken);

            _isolatedPlugins.TryAdd(context.PluginId, container);

            stopwatch.Stop();

            var result = new RecoveryResult { Success = true, Message = $"Plugin {context.PluginId} isolated successfully", Duration = stopwatch.Elapsed };
            RecordStrategyResult(strategyKey, true, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new RecoveryResult { Success = false, Message = $"Isolation strategy failed: {ex.Message}", Exception = ex };
            RecordStrategyResult(strategyKey, false, stopwatch.Elapsed);
            return result;
        }
    }

    /// <summary>
    /// Executes the shutdown plugin recovery strategy
    /// </summary>
    public async Task<RecoveryResult> ExecuteShutdownStrategyAsync(
        PluginRecoveryContext context,
        string reason = "Recovery strategy",
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var strategyKey = $"{context.PluginId}-Shutdown";

        _logger.LogCritical("Executing shutdown strategy for plugin {PluginId}: {Reason}", context.PluginId, reason);

        try
        {
            // Remove from isolation if present
            if (_isolatedPlugins.TryRemove(context.PluginId, out var container))
            {
                await container.DisposeAsync();
            }

            // Force stop the plugin
            await ForceStopPluginAsync(context.PluginId, cancellationToken);

            // Mark as permanently failed (implementation would be context-specific)
            await MarkPluginAsFailedAsync(context.PluginId, reason, cancellationToken);

            stopwatch.Stop();

            var result = new RecoveryResult { Success = true, Message = $"Plugin {context.PluginId} shut down safely", Duration = stopwatch.Elapsed };
            RecordStrategyResult(strategyKey, true, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new RecoveryResult { Success = false, Message = $"Shutdown strategy failed: {ex.Message}", Exception = ex };
            RecordStrategyResult(strategyKey, false, stopwatch.Elapsed);
            return result;
        }
    }

    /// <summary>
    /// Executes the rollback plugin recovery strategy
    /// </summary>
    public async Task<RecoveryResult> ExecuteRollbackStrategyAsync(
        PluginRecoveryContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var strategyKey = $"{context.PluginId}-Rollback";

        _logger.LogInformation("Executing rollback strategy for plugin {PluginId}", context.PluginId);

        try
        {
            // Find previous stable version
            var stableVersion = await FindStableVersionAsync(context.PluginId, cancellationToken);
            if (stableVersion == null)
            {
                return new RecoveryResult { Success = false, Message = $"No stable version found for plugin {context.PluginId}" };
            }

            // Unload current version
            await UnloadPluginAsync(context.PluginId, cancellationToken);

            // Load stable version
            var rollbackResult = await LoadPluginVersionAsync(context.PluginId, stableVersion, cancellationToken);

            stopwatch.Stop();

            var result = rollbackResult
                ? new RecoveryResult { Success = true, Message = $"Plugin {context.PluginId} rolled back to stable version {stableVersion}", Duration = stopwatch.Elapsed }
                : new RecoveryResult { Success = false, Message = $"Failed to rollback plugin {context.PluginId}" };

            RecordStrategyResult(strategyKey, result.Success, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new RecoveryResult { Success = false, Message = $"Rollback strategy failed: {ex.Message}", Exception = ex };
            RecordStrategyResult(strategyKey, false, stopwatch.Elapsed);
            return result;
        }
    }

    /// <summary>
    /// Gets effectiveness metrics for all recovery strategies
    /// </summary>
    public Dictionary<string, StrategyEffectiveness> GetStrategyEffectiveness()
    {
        return _strategyMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Gets the most effective strategy for a specific plugin based on historical data
    /// </summary>
    public PluginRecoveryStrategy? GetMostEffectiveStrategy(string pluginId)
    {
        var pluginStrategies = _strategyMetrics
            .Where(kvp => kvp.Key.StartsWith($"{pluginId}-"))
            .OrderByDescending(kvp => kvp.Value.SuccessRate)
            .ThenBy(kvp => kvp.Value.AverageExecutionTime)
            .FirstOrDefault();

        if (pluginStrategies.Key == null)
        {
            return null;
        }


        var strategyName = pluginStrategies.Key.Split('-').LastOrDefault();
        return Enum.TryParse<PluginRecoveryStrategy>(strategyName, out var strategy) ? strategy : null;
    }

    private static bool IsEmergencyCondition(Exception error, FailureAnalysisResult analysis)
    {
        // Critical system exceptions
        if (error is OutOfMemoryException or StackOverflowException or AccessViolationException)
        {
            return true;
        }

        // High severity with rapid failure rate

        if (analysis.Severity == FailureSeverity.Critical && analysis.TotalFailures > 5)
        {

            return true;
        }


        return false;
    }

    private List<PluginRecoveryStrategy> GetStrategyCandidates(
        Exception error,
        PluginRecoveryContext context,
        PluginHealthState healthState,
        FailureAnalysisResult analysis)
    {
        var candidates = new List<PluginRecoveryStrategy>();

        // Error-specific strategy mapping
        switch (error)
        {
            case OutOfMemoryException:
                candidates.AddRange([PluginRecoveryStrategy.RestartPlugin, PluginRecoveryStrategy.IsolatePlugin]);
                break;

            case AppDomainUnloadedException:
                candidates.AddRange([PluginRecoveryStrategy.RestartPlugin, PluginRecoveryStrategy.ReloadPlugin]);
                break;

            case BadImageFormatException:
            case FileLoadException:
                candidates.AddRange([PluginRecoveryStrategy.ReloadPlugin, PluginRecoveryStrategy.RollbackVersion]);
                break;

            case TargetInvocationException:
                candidates.AddRange([PluginRecoveryStrategy.IsolatePlugin, PluginRecoveryStrategy.RestartPlugin]);
                break;

            default:
                if (_config.EnableAutoRestart)
                {
                    candidates.Add(PluginRecoveryStrategy.RestartPlugin);
                }
                else
                {
                    candidates.Add(PluginRecoveryStrategy.IsolatePlugin);
                }


                break;
        }

        // Filter based on constraints
        if (healthState.RestartCount >= _config.MaxRestarts)
        {
            candidates.Remove(PluginRecoveryStrategy.RestartPlugin);
        }

        if (!_config.EnablePluginIsolation)
        {
            candidates.Remove(PluginRecoveryStrategy.IsolatePlugin);
        }

        // Add shutdown as last resort
        if (candidates.Count == 0 || healthState.ConsecutiveFailures >= _config.MaxConsecutiveFailures)
        {
            candidates.Add(PluginRecoveryStrategy.ShutdownPlugin);
        }

        return candidates;
    }

    private async Task<PluginRecoveryStrategy> SelectOptimalStrategyAsync(
        string pluginId,
        List<PluginRecoveryStrategy> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 1)
        {

            return candidates[0];
        }

        // Score each candidate based on historical effectiveness

        var scoredCandidates = new List<(PluginRecoveryStrategy Strategy, double Score)>();

        foreach (var candidate in candidates)
        {
            var strategyKey = $"{pluginId}-{candidate}";
            var effectiveness = _strategyMetrics.GetValueOrDefault(strategyKey, new StrategyEffectiveness());

            // Calculate score based on success rate and execution time
            var score = effectiveness.SuccessRate * 0.7 +
                       (1.0 - Math.Min(1.0, effectiveness.AverageExecutionTime.TotalSeconds / 60.0)) * 0.3;

            scoredCandidates.Add((candidate, score));
        }

        // Return highest scoring strategy, with tie-breaking
        var bestStrategy = scoredCandidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => GetStrategyPriority(c.Strategy))
            .First();

        return bestStrategy.Strategy;
    }

    private static int GetStrategyPriority(PluginRecoveryStrategy strategy) => strategy switch
    {
        PluginRecoveryStrategy.RestartPlugin => 1,
        PluginRecoveryStrategy.ReloadPlugin => 2,
        PluginRecoveryStrategy.IsolatePlugin => 3,
        PluginRecoveryStrategy.RollbackVersion => 4,
        PluginRecoveryStrategy.ShutdownPlugin => 5,
        _ => 999
    };

    private void RecordStrategyResult(string strategyKey, bool success, TimeSpan executionTime)
    {
        _strategyMetrics.AddOrUpdate(strategyKey,
            new StrategyEffectiveness
            {
                TotalAttempts = 1,
                SuccessfulAttempts = success ? 1 : 0,
                SuccessRate = success ? 1.0 : 0.0,
                AverageExecutionTime = executionTime,
                LastUsed = DateTimeOffset.UtcNow
            },
            (_, existing) =>
            {
                var newAttempts = existing.TotalAttempts + 1;
                var newSuccesses = existing.SuccessfulAttempts + (success ? 1 : 0);

                return new StrategyEffectiveness
                {
                    TotalAttempts = newAttempts,
                    SuccessfulAttempts = newSuccesses,
                    SuccessRate = (double)newSuccesses / newAttempts,
                    AverageExecutionTime = TimeSpan.FromTicks(
                        (existing.AverageExecutionTime.Ticks * existing.TotalAttempts + executionTime.Ticks) / newAttempts),
                    LastUsed = DateTimeOffset.UtcNow
                };
            });
    }

    // Plugin lifecycle operations (placeholders for actual implementation)
    private async Task<bool> StopPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        _logger.LogDebug("Plugin {PluginId} stopped gracefully", pluginId);
        return true;
    }

    private async Task ForceStopPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);
        _logger.LogDebug("Plugin {PluginId} force stopped", pluginId);
    }

    private async Task<bool> StartPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        _logger.LogDebug("Plugin {PluginId} started", pluginId);
        return true;
    }

    private async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        _logger.LogDebug("Plugin {PluginId} unloaded", pluginId);
        return true;
    }

    private async Task<bool> LoadPluginAsync(string pluginId, string? pluginPath, CancellationToken cancellationToken)
    {
        await Task.Delay(300, cancellationToken);
        _logger.LogDebug("Plugin {PluginId} loaded from {Path}", pluginId, pluginPath ?? "default");
        return true;
    }

    private static async Task<string?> FindStableVersionAsync(string pluginId, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);
        return "1.0.0"; // Placeholder
    }

    private async Task<bool> LoadPluginVersionAsync(string pluginId, string version, CancellationToken cancellationToken)
    {
        await Task.Delay(250, cancellationToken);
        _logger.LogDebug("Plugin {PluginId} version {Version} loaded", pluginId, version);
        return true;
    }

    private async Task MarkPluginAsFailedAsync(string pluginId, string reason, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);
        _logger.LogWarning("Plugin {PluginId} marked as permanently failed: {Reason}", pluginId, reason);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _recoveryLock?.Dispose();

            // Dispose all isolated containers
            foreach (var container in _isolatedPlugins.Values)
            {
                try
                {
                    container.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing isolated container");
                }
            }

            _disposed = true;
            _logger.LogInformation("Plugin Recovery Strategies disposed");
        }
    }
}

/// <summary>
/// Tracks effectiveness metrics for recovery strategies
/// </summary>
public sealed record StrategyEffectiveness
{
    public int TotalAttempts { get; init; }
    public int SuccessfulAttempts { get; init; }
    public double SuccessRate { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public DateTimeOffset LastUsed { get; init; }
}

// PluginRecoveryStrategy enum already defined in PluginRecoveryTypes.cs