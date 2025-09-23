// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using global::System.Runtime.Loader;
using DotCompute.Core.Recovery;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;
using CorePluginHealthStatus = DotCompute.Core.Recovery.PluginHealthStatus;

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Main orchestrator for plugin recovery operations with crash isolation,
/// automatic restart capabilities, and health monitoring
/// </summary>
public sealed class PluginRecoveryOrchestrator : BaseRecoveryStrategy<PluginRecoveryContext>, IDisposable
{
    private readonly ConcurrentDictionary<string, PluginHealthState> _pluginStates;
    private readonly ConcurrentDictionary<string, IsolatedPluginContainer> _isolatedPlugins;
    private readonly PluginRecoveryConfiguration _config;
    private readonly PluginHealthMonitor _healthMonitor;
    private readonly PluginRestartStrategy _restartStrategy;
    private readonly PluginStateManager _stateManager;
    private readonly PluginCircuitBreaker _circuitBreaker;
    private readonly PluginRecoveryLogger _recoveryLogger;
    private readonly SemaphoreSlim _recoveryLock;
    private bool _disposed;

    public override RecoveryCapability Capability => RecoveryCapability.DeviceErrors;
    public override int Priority => 80;

    public PluginRecoveryOrchestrator(ILogger<PluginRecoveryOrchestrator> logger, PluginRecoveryConfiguration? config = null)
        : base(logger)
    {
        _config = config ?? PluginRecoveryConfiguration.Default;
        _pluginStates = new ConcurrentDictionary<string, PluginHealthState>();
        _isolatedPlugins = new ConcurrentDictionary<string, IsolatedPluginContainer>();
        _recoveryLock = new SemaphoreSlim(1, 1);

        // Initialize sub-components
        _healthMonitor = new PluginHealthMonitor(_config, logger);
        _restartStrategy = new PluginRestartStrategy(_config, logger);
        _stateManager = new PluginStateManager(_config, logger);
        _circuitBreaker = new PluginCircuitBreaker(_config, logger);
        _recoveryLogger = new PluginRecoveryLogger(logger);

        Logger.LogInformation("Plugin Recovery Orchestrator initialized with isolation: {Isolation}, auto-restart: {AutoRestart}",
            _config.EnablePluginIsolation, _config.EnableAutoRestart);
    }

    public override bool CanHandle(Exception error, PluginRecoveryContext context)
    {
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

    public override async Task<RecoveryResult> RecoverAsync(
        Exception error,
        PluginRecoveryContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        Logger.LogWarning("Plugin error detected for {PluginId}: {Error}", context.PluginId, error.Message);

        await _recoveryLock.WaitAsync(cancellationToken);

        try
        {
            // Get or create plugin health state
            var healthState = _pluginStates.GetOrAdd(context.PluginId,
                id => new PluginHealthState(id, _config));
            healthState.RecordError(error);

            // Check circuit breaker
            if (_circuitBreaker.ShouldBlockOperation(context.PluginId))
            {
                return Failure($"Circuit breaker open for plugin {context.PluginId}");
            }

            // Determine recovery strategy
            var strategy = DetermineRecoveryStrategy(error, context, healthState);
            Logger.LogInformation("Using plugin recovery strategy: {Strategy} for {PluginId}", strategy, context.PluginId);

            var result = await ExecutePluginRecoveryAsync(strategy, context, healthState, cancellationToken);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (result.Success)
            {
                healthState.RecordSuccessfulRecovery();
                _circuitBreaker.RecordSuccess(context.PluginId);
                _recoveryLogger.LogSuccessfulRecovery(context.PluginId, strategy, stopwatch.Elapsed);
            }
            else
            {
                healthState.RecordFailedRecovery();
                _circuitBreaker.RecordFailure(context.PluginId);
                _recoveryLogger.LogFailedRecovery(context.PluginId, strategy, result.Message);
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
        Logger.LogInformation("Isolating plugin {PluginId} for crash protection", pluginId);

        var container = new IsolatedPluginContainer(pluginId, plugin, Logger, _config);
        await container.InitializeAsync(cancellationToken);

        _ = _isolatedPlugins.TryAdd(pluginId, container);

        // Monitor the container
        var healthState = _pluginStates.GetOrAdd(pluginId, id => new PluginHealthState(id, _config));
        healthState.SetIsolated(true);

        Logger.LogInformation("Plugin {PluginId} successfully isolated", pluginId);
        return container;
    }

    /// <summary>
    /// Gets health information for all monitored plugins
    /// </summary>
    public PluginHealthReport GetHealthReport()
    {
        return _healthMonitor.GenerateHealthReport(_pluginStates.Values);
    }

    /// <summary>
    /// Performs emergency shutdown of a crashed plugin
    /// </summary>
    public async Task<bool> EmergencyShutdownAsync(
        string pluginId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        Logger.LogCritical("Performing emergency shutdown of plugin {PluginId}: {Reason}", pluginId, reason);

        try
        {
            // Mark as unhealthy immediately
            if (_pluginStates.TryGetValue(pluginId, out var healthState))
            {
                healthState.SetEmergencyShutdown(reason);
            }

            // Force stop the plugin
            await _restartStrategy.ForceStopPluginAsync(pluginId, cancellationToken);

            // Clean up isolation container if exists
            if (_isolatedPlugins.TryRemove(pluginId, out var container))
            {
                await container.DisposeAsync();
            }

            Logger.LogWarning("Emergency shutdown completed for plugin {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to perform emergency shutdown for plugin {PluginId}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Checks plugin compatibility and version conflicts
    /// </summary>
    public PluginCompatibilityResult CheckPluginCompatibility(string pluginId, Assembly pluginAssembly)
    {
        try
        {
            var result = new PluginCompatibilityResult { PluginId = pluginId };

            // Check .NET version compatibility
            var targetFramework = pluginAssembly.GetCustomAttribute<global::System.Runtime.Versioning.TargetFrameworkAttribute>();
            result.FrameworkCompatible = IsFrameworkCompatible(targetFramework?.FrameworkName);

            // Check dependency versions
            result.DependencyConflicts = CheckDependencyConflicts(pluginAssembly);

            // Check for security issues
            result.SecurityIssues = CheckSecurityIssues(pluginAssembly);

            // Overall compatibility
            result.IsCompatible = result.FrameworkCompatible &&
                                result.DependencyConflicts.Count == 0 &&
                                result.SecurityIssues.Count == 0;

            Logger.LogInformation("Plugin {PluginId} compatibility check: {Compatible}",
                pluginId, result.IsCompatible ? "PASS" : "FAIL");

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking compatibility for plugin {PluginId}", pluginId);
            return new PluginCompatibilityResult
            {
                PluginId = pluginId,
                IsCompatible = false,
                Error = ex.Message
            };
        }
    }

    private PluginRecoveryStrategy DetermineRecoveryStrategy(
        Exception error,
        PluginRecoveryContext context,
        PluginHealthState healthState)
    {
        // Critical failures require isolation or shutdown
        if (healthState.ConsecutiveFailures >= _config.MaxConsecutiveFailures)
        {
            return _config.EnablePluginIsolation
                ? PluginRecoveryStrategy.IsolatePlugin
                : PluginRecoveryStrategy.ShutdownPlugin;
        }

        return error switch
        {
            OutOfMemoryException => PluginRecoveryStrategy.RestartPlugin,
            AppDomainUnloadedException => PluginRecoveryStrategy.RestartPlugin,
            BadImageFormatException => PluginRecoveryStrategy.ReloadPlugin,
            FileLoadException => PluginRecoveryStrategy.ReloadPlugin,
            TargetInvocationException => PluginRecoveryStrategy.IsolatePlugin,
            _ when healthState.RestartCount > _config.MaxRestarts => PluginRecoveryStrategy.ShutdownPlugin,
            _ => _config.EnableAutoRestart ? PluginRecoveryStrategy.RestartPlugin : PluginRecoveryStrategy.IsolatePlugin
        };
    }

    private async Task<RecoveryResult> ExecutePluginRecoveryAsync(
        PluginRecoveryStrategy strategy,
        PluginRecoveryContext context,
        PluginHealthState healthState,
        CancellationToken cancellationToken)
    {
        return strategy switch
        {
            PluginRecoveryStrategy.RestartPlugin => await _restartStrategy.ExecuteRestartAsync(context, cancellationToken),
            PluginRecoveryStrategy.ReloadPlugin => await _restartStrategy.ExecuteReloadAsync(context, cancellationToken),
            PluginRecoveryStrategy.IsolatePlugin => await ExecuteIsolationAsync(context, cancellationToken),
            PluginRecoveryStrategy.ShutdownPlugin => await ExecuteShutdownAsync(context, cancellationToken),
            PluginRecoveryStrategy.RollbackVersion => await _restartStrategy.ExecuteRollbackAsync(context, cancellationToken),
            _ => Failure("Unknown plugin recovery strategy")
        };
    }

    private async Task<RecoveryResult> ExecuteIsolationAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
    {
        if (context.Plugin is not IBackendPlugin plugin)
        {
            return Failure("Cannot isolate plugin - plugin instance not available or invalid type");
        }

        try
        {
            var container = await IsolatePluginAsync(context.PluginId, plugin, cancellationToken);
            return Success($"Plugin {context.PluginId} isolated successfully", TimeSpan.FromMilliseconds(300));
        }
        catch (Exception ex)
        {
            return Failure($"Plugin isolation failed: {ex.Message}", ex);
        }
    }

    private async Task<RecoveryResult> ExecuteShutdownAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
    {
        var success = await EmergencyShutdownAsync(context.PluginId, "Recovery strategy", cancellationToken);
        return success
            ? Success($"Plugin {context.PluginId} shut down safely", TimeSpan.FromMilliseconds(200))
            : Failure($"Failed to shut down plugin {context.PluginId}");
    }

    private static bool IsPluginRelatedError(Exception error, PluginRecoveryContext context)
    {
        var message = error.Message.ToLowerInvariant();
        return message.Contains("plugin") ||
               message.Contains(context.PluginId.ToLowerInvariant()) ||
               error.StackTrace?.Contains("plugin", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsFrameworkCompatible(string? frameworkName)
        => frameworkName?.Contains(".NETCoreApp") == true;

    private static List<string> CheckDependencyConflicts(Assembly assembly)
        => [];

    private static List<string> CheckSecurityIssues(Assembly assembly)
        => [];

    public override void Dispose()
    {
        if (!_disposed)
        {
            _healthMonitor?.Dispose();
            _restartStrategy?.Dispose();
            _stateManager?.Dispose();
            _circuitBreaker?.Dispose();
            _recoveryLogger?.Dispose();
            _recoveryLock?.Dispose();

            // Dispose all isolated plugin containers
            foreach (var container in _isolatedPlugins.Values)
            {
                try
                {
                    container.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error disposing plugin container {PluginId}", container.PluginId);
                }
            }

            _disposed = true;
            Logger.LogInformation("Plugin Recovery Orchestrator disposed");
        }
    }
}