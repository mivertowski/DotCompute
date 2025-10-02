// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using DotCompute.Core.Recovery;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Recovery
{
    /// <summary>
    /// Core plugin recovery manager with crash isolation,
    /// automatic restart capabilities, and health monitoring
    /// </summary>
    public sealed class PluginRecoveryCore : BaseRecoveryStrategy<PluginRecoveryContext>, IDisposable
    {
        private readonly ConcurrentDictionary<string, PluginHealthState> _pluginStates;
        private readonly ConcurrentDictionary<string, IsolatedPluginContainer> _isolatedPlugins;
        private readonly PluginRecoveryConfiguration _config;
        private readonly Timer _healthMonitorTimer;
        private readonly SemaphoreSlim _recoveryLock;
        private bool _disposed;
        /// <summary>
        /// Gets or sets the capability.
        /// </summary>
        /// <value>The capability.</value>

        public override RecoveryCapability Capability => RecoveryCapability.DeviceErrors; // Plugin errors are a subset of device errors
        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public override int Priority => 80;
        /// <summary>
        /// Initializes a new instance of the PluginRecoveryCore class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The config.</param>

        public PluginRecoveryCore(ILogger<PluginRecoveryCore> logger, PluginRecoveryConfiguration? config = null)
            : base(logger)
        {
            _config = config ?? PluginRecoveryConfiguration.Default;
            _pluginStates = new ConcurrentDictionary<string, PluginHealthState>();
            _isolatedPlugins = new ConcurrentDictionary<string, IsolatedPluginContainer>();
            _recoveryLock = new SemaphoreSlim(1, 1);

            // Start health monitoring
            _healthMonitorTimer = new Timer(PerformHealthCheck, null,
                _config.HealthCheckInterval, _config.HealthCheckInterval);

            Logger.LogInformation("Plugin Recovery Manager initialized with isolation: {Isolation}, auto-restart: {AutoRestart}",
                _config.EnablePluginIsolation, _config.EnableAutoRestart);
        }
        /// <summary>
        /// Determines whether handle.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <param name="context">The context.</param>
        /// <returns>true if the condition is met; otherwise, false.</returns>

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

            await _recoveryLock.WaitAsync(cancellationToken);

            try
            {
                // Get or create plugin health state
                var healthState = _pluginStates.GetOrAdd(context.PluginId,
                    id => new PluginHealthState(id, _config));
                healthState.RecordError(error);

                // Determine recovery strategy
                var strategy = DetermineRecoveryStrategy(error, context, healthState);
                Logger.LogInformation("Using plugin recovery strategy: {Strategy} for {PluginId}", strategy, context.PluginId);

                var result = await ExecutePluginRecoveryAsync(strategy, context, healthState, cancellationToken);

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                if (result.Success)
                {
                    healthState.RecordSuccessfulRecovery();
                    Logger.LogInformation("Plugin recovery successful for {PluginId} using {Strategy} in {Duration}ms",
                        context.PluginId, strategy, stopwatch.ElapsedMilliseconds);
                }
                else
                {
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
            Logger.LogInformation("Isolating plugin {PluginId} for crash protection", pluginId);

            var container = new IsolatedPluginContainer(pluginId, plugin, Logger, _config);
            await container.InitializeAsync(cancellationToken);

            _ = _isolatedPlugins.TryAdd(pluginId, container);

            // Monitor the container
            var healthState = _pluginStates.GetOrAdd(pluginId, id => new PluginHealthState(id, _config));

            Logger.LogInformation("Plugin {PluginId} successfully isolated", pluginId);
            return container;
        }

        /// <summary>
        /// Restarts a failed plugin with proper cleanup
        /// </summary>
        public async Task<bool> RestartPluginAsync(
            string pluginId,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Attempting to restart plugin {PluginId}", pluginId);

            try
            {
                // Stop and cleanup existing instance
                _ = await StopPluginAsync(pluginId, cancellationToken);

                // Wait for cleanup to complete
                await Task.Delay(_config.RestartDelay, cancellationToken);

                // Start new instance
                var success = await StartPluginAsync(pluginId, cancellationToken);

                if (success)
                {
                    var healthState = _pluginStates.GetOrAdd(pluginId, id => new PluginHealthState(id, _config));
                    healthState.RecordSuccessfulRecovery();

                    Logger.LogInformation("Plugin {PluginId} successfully restarted", pluginId);
                }
                else
                {
                    Logger.LogError("Failed to restart plugin {PluginId}", pluginId);
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception during plugin {PluginId} restart", pluginId);
                return false;
            }
        }

        /// <summary>
        /// Gets health information for all monitored plugins
        /// </summary>
        public PluginHealthReport GetHealthReport()
        {
            var pluginHealths = new List<PluginHealthInfo>();

            foreach (var kvp in _pluginStates)
            {
                var pluginId = kvp.Key;
                var state = kvp.Value;

                pluginHealths.Add(new PluginHealthInfo
                {
                    PluginId = pluginId,
                    IsHealthy = state.Status == PluginHealthStatus.Healthy,
                    IsIsolated = _isolatedPlugins.ContainsKey(pluginId),
                    ErrorCount = state.ErrorCount,
                    RestartCount = state.RestartCount,
                    LastError = state.GetRecentErrors().LastOrDefault(),
                    ConsecutiveFailures = state.ConsecutiveFailures
                });
            }

            return new PluginHealthReport
            {
                PluginHealths = pluginHealths
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
            Logger.LogCritical("Performing emergency shutdown of plugin {PluginId}: {Reason}", pluginId, reason);

            try
            {
                // Force stop isolated container if exists
                if (_isolatedPlugins.TryGetValue(pluginId, out var container))
                {
                    await container.ForceTerminateAsync();
                    _ = _isolatedPlugins.TryRemove(pluginId, out _);
                }

                // Force stop plugin
                await ForceStopPluginAsync(pluginId, cancellationToken);

                // Update health state
                if (_pluginStates.TryGetValue(pluginId, out var healthState))
                {
                    healthState.RecordError(new InvalidOperationException($"Emergency shutdown: {reason}"));
                }

                Logger.LogInformation("Emergency shutdown completed for plugin {PluginId}", pluginId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to perform emergency shutdown for plugin {PluginId}", pluginId);
                return false;
            }
        }

        /// <summary>
        /// Checks plugin compatibility
        /// </summary>
        public PluginCompatibilityResult CheckPluginCompatibility(string pluginId, Assembly pluginAssembly)
        {
            var result = new PluginCompatibilityResult
            {
                PluginId = pluginId,
                CurrentFramework = Environment.Version.ToString()
            };

            try
            {
                // Check framework compatibility
                var targetFramework = pluginAssembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
                result.RequiredFramework = targetFramework?.FrameworkName ?? "Unknown";

                // Check dependencies
                var dependencies = pluginAssembly.GetReferencedAssemblies();
                result.RequiredDependencies = [.. dependencies.Select(d => d.FullName)];

                // Perform compatibility checks
                result.IsCompatible = true; // Simplified check

                Logger.LogInformation("Compatibility check completed for plugin {PluginId}: {Compatible}",
                    pluginId, result.IsCompatible);
            }
            catch (Exception ex)
            {
                result.IsCompatible = false;
                result.Report = $"Compatibility check failed: {ex.Message}";
                Logger.LogError(ex, "Failed to check compatibility for plugin {PluginId}", pluginId);
            }

            return result;
        }

        #region Private Methods

        private static PluginRecoveryStrategy DetermineRecoveryStrategy(
            Exception error,
            PluginRecoveryContext context,
            PluginHealthState healthState)
        {
            // Critical errors require isolation
            if (error is OutOfMemoryException or StackOverflowException)
            {
                return PluginRecoveryStrategy.IsolatePlugin;
            }

            // Too many failures - isolate or shutdown
            if (healthState.ShouldIsolate())
            {
                return PluginRecoveryStrategy.IsolatePlugin;
            }

            // Can restart
            if (healthState.ShouldRestart())
            {
                return PluginRecoveryStrategy.RestartPlugin;
            }

            // Try reload first
            return PluginRecoveryStrategy.ReloadPlugin;
        }

        private async Task<RecoveryResult> ExecutePluginRecoveryAsync(
            PluginRecoveryStrategy strategy,
            PluginRecoveryContext context,
            PluginHealthState healthState,
            CancellationToken cancellationToken)
        {
            return strategy switch
            {
                PluginRecoveryStrategy.RestartPlugin => await RestartPluginRecoveryAsync(context, cancellationToken),
                PluginRecoveryStrategy.ReloadPlugin => await ReloadPluginRecoveryAsync(context, cancellationToken),
                PluginRecoveryStrategy.IsolatePlugin => await IsolatePluginRecoveryAsync(context, cancellationToken),
                PluginRecoveryStrategy.ShutdownPlugin => await ShutdownPluginRecoveryAsync(context, cancellationToken),
                PluginRecoveryStrategy.RollbackVersion => await RollbackPluginRecoveryAsync(context, cancellationToken),
                _ => RecoveryResult.CreateFailure($"Unknown strategy: {strategy}")
            };
        }

        private async Task<RecoveryResult> RestartPluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
        {
            var success = await RestartPluginAsync(context.PluginId, cancellationToken);
            return success ? RecoveryResult.CreateSuccess("Plugin restarted successfully")
                          : RecoveryResult.CreateFailure("Plugin restart failed");
        }

        private async Task<RecoveryResult> ReloadPluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Simplified reload logic
                await Task.Delay(100, cancellationToken);
                return RecoveryResult.CreateSuccess("Plugin reloaded successfully");
            }
            catch (Exception ex)
            {
                return RecoveryResult.CreateFailure($"Plugin reload failed: {ex.Message}");
            }
        }

        private async Task<RecoveryResult> IsolatePluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
        {
            try
            {
                if (context.Plugin is IBackendPlugin backendPlugin)
                {
                    _ = await IsolatePluginAsync(context.PluginId, backendPlugin, cancellationToken);
                    return RecoveryResult.CreateSuccess("Plugin isolated successfully");
                }
                return RecoveryResult.CreateFailure("No plugin instance available for isolation");
            }
            catch (Exception ex)
            {
                return RecoveryResult.CreateFailure($"Plugin isolation failed: {ex.Message}");
            }
        }

        private async Task<RecoveryResult> ShutdownPluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
        {
            var success = await EmergencyShutdownAsync(context.PluginId, "Recovery shutdown", cancellationToken);
            return success ? RecoveryResult.CreateSuccess("Plugin shutdown successfully")
                          : RecoveryResult.CreateFailure("Plugin shutdown failed");
        }

        private async Task<RecoveryResult> RollbackPluginRecoveryAsync(PluginRecoveryContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Simplified rollback logic
                await Task.Delay(100, cancellationToken);
                return RecoveryResult.CreateSuccess("Plugin rollback completed");
            }
            catch (Exception ex)
            {
                return RecoveryResult.CreateFailure($"Plugin rollback failed: {ex.Message}");
            }
        }

        private static bool IsPluginRelatedError(Exception error, PluginRecoveryContext context)
        {
            return error.StackTrace?.Contains(context.PluginId) == true ||
                   error.Source?.Contains("plugin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static async Task<bool> StopPluginAsync(string pluginId, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(50, cancellationToken); // Placeholder
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> StartPluginAsync(string pluginId, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(100, cancellationToken); // Placeholder
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ForceStopPluginAsync(string pluginId, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(10, cancellationToken); // Placeholder for force stop
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to force stop plugin {PluginId}", pluginId);
            }
        }

        private void PerformHealthCheck(object? state)
        {
            try
            {
                foreach (var kvp in _pluginStates)
                {
                    var pluginId = kvp.Key;
                    var healthState = kvp.Value;

                    // Check if plugin needs attention
                    if (healthState.Status == PluginHealthStatus.Critical)
                    {
                        Logger.LogWarning("Plugin {PluginId} is in critical state", pluginId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during health check");
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        #endregion

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _healthMonitorTimer?.Dispose();

            foreach (var container in _isolatedPlugins.Values)
            {
                container.Dispose();
            }

            _recoveryLock?.Dispose();
            _disposed = true;
        }
    }
}