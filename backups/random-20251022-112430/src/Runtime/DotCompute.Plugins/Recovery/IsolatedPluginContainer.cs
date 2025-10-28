// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.Loader;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Recovery
{
    /// <summary>
    /// Isolated container for running plugins in a secure, monitored environment
    /// </summary>
    public sealed class IsolatedPluginContainer : IDisposable, IAsyncDisposable
    {
        private readonly string _pluginId;
        private readonly IBackendPlugin _plugin;
        private readonly ILogger _logger;
        private readonly PluginRecoveryConfiguration _config;
        private readonly AssemblyLoadContext? _loadContext;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private readonly SemaphoreSlim _operationLock;
#pragma warning disable CS0649 // Field is never assigned to
        private readonly Process? _isolatedProcess;
#pragma warning restore CS0649
        private bool _disposed;
        private bool _initialized;

        /// <summary>
        /// Plugin identifier
        /// </summary>
        public string PluginId => _pluginId;

        /// <summary>
        /// Whether the container is currently active
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Whether the plugin is running in a separate process
        /// </summary>
        public bool IsProcessIsolated => _isolatedProcess != null;

        /// <summary>
        /// Current memory usage of the isolated plugin
        /// </summary>
        public long MemoryUsageBytes { get; private set; }

        /// <summary>
        /// Container start time
        /// </summary>
        public DateTimeOffset StartTime { get; private set; }

        /// <summary>
        /// Container uptime
        /// </summary>
        public TimeSpan Uptime => DateTimeOffset.UtcNow - StartTime;

        /// <summary>
        /// Number of operations executed in this container
        /// </summary>
        public long OperationCount { get; private set; }

        /// <summary>
        /// Last operation timestamp
        /// </summary>
        public DateTimeOffset? LastOperationTime { get; private set; }
        /// <summary>
        /// Initializes a new instance of the IsolatedPluginContainer class.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <param name="plugin">The plugin.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The config.</param>

        public IsolatedPluginContainer(
            string pluginId,
            IBackendPlugin plugin,
            ILogger logger,
            PluginRecoveryConfiguration config)
        {
            _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _shutdownTokenSource = new CancellationTokenSource();
            _operationLock = new SemaphoreSlim(1, 1);

            // Create isolated assembly load context if needed
            if (config.EnablePluginIsolation)
            {
                _loadContext = new AssemblyLoadContext($"Plugin_{pluginId}", isCollectible: true);
            }

            StartTime = DateTimeOffset.UtcNow;
            _logger.LogInfoMessage("Created isolated container for plugin {pluginId}");
        }

        /// <summary>
        /// Initializes the isolated container
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_initialized)
            {
                return;
            }

            _logger.LogInfoMessage("Initializing isolated container for plugin {_pluginId}");

            try
            {
                // Initialize the plugin in isolation
                // Note: Plugin initialization would be implementation specific
                await Task.Delay(100, cancellationToken); // Placeholder for initialization

                // Start monitoring
                _ = Task.Run(MonitorResourceUsageAsync, cancellationToken);

                IsActive = true;
                _initialized = true;

                _logger.LogInfoMessage("Successfully initialized isolated container for plugin {_pluginId}");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to initialize isolated container for plugin {_pluginId}");
                throw;
            }
        }

        /// <summary>
        /// Executes an operation within the isolated container
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<IBackendPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_initialized)
            {
                throw new InvalidOperationException("Container not initialized");
            }

            await _operationLock.WaitAsync(cancellationToken);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _shutdownTokenSource.Token);
                timeoutCts.CancelAfter(_config.RecoveryTimeout);

                OperationCount++;
                LastOperationTime = DateTimeOffset.UtcNow;

                var result = await operation(_plugin, timeoutCts.Token);

                _logger.LogDebugMessage("Operation completed successfully in container {_pluginId}");
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarningMessage("Operation cancelled in container {_pluginId}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Operation failed in container {_pluginId}");
                throw;
            }
            finally
            {
                _ = _operationLock.Release();
            }
        }

        /// <summary>
        /// Shuts down the isolated container gracefully
        /// </summary>
        public async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || !_initialized)
            {
                return;
            }

            _logger.LogInfoMessage("Shutting down isolated container for plugin {_pluginId}");

            try
            {
                // Signal shutdown
                _shutdownTokenSource.Cancel();

                // Wait for current operations to complete
                await _operationLock.WaitAsync(cancellationToken);

                try
                {
                    // Perform plugin-specific shutdown
                    if (_plugin is IDisposable disposablePlugin)
                    {
                        disposablePlugin.Dispose();
                    }

                    // Unload assembly context if isolated
                    if (_loadContext != null)
                    {
                        _loadContext.Unload();
                    }

                    IsActive = false;
                    _logger.LogInfoMessage("Successfully shut down isolated container for plugin {_pluginId}");
                }
                finally
                {
                    _ = _operationLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error during container shutdown for plugin {_pluginId}");
                throw;
            }
        }

        /// <summary>
        /// Gets current resource usage statistics
        /// </summary>
        public PluginResourceUsage GetResourceUsage()
        {
            return new PluginResourceUsage
            {
                PluginId = _pluginId,
                MemoryUsageBytes = MemoryUsageBytes,
                MeasurementTime = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// Monitors resource usage in background
        /// </summary>
        private async Task MonitorResourceUsageAsync()
        {
            while (!_shutdownTokenSource.Token.IsCancellationRequested && IsActive)
            {
                try
                {
                    // Update memory usage (simplified monitoring)
                    var currentProcess = Process.GetCurrentProcess();
                    MemoryUsageBytes = currentProcess.WorkingSet64;

                    // Check for resource threshold violations
                    var usage = GetResourceUsage();
                    if (usage.ExceedsCriticalThresholds())
                    {
                        _logger.LogWarningMessage("Plugin {_pluginId} exceeds critical resource thresholds: {usage}");
                    }
                    else if (usage.ExceedsWarningThresholds())
                    {
                        _logger.LogWarningMessage("Plugin {_pluginId} exceeds warning resource thresholds: {usage}");
                    }

                    await Task.Delay(_config.HealthCheckInterval, _shutdownTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogErrorMessage(ex, $"Error during resource monitoring for plugin {_pluginId}");
                    await Task.Delay(TimeSpan.FromMinutes(1), _shutdownTokenSource.Token);
                }
            }
        }

        /// <summary>
        /// Forces immediate termination of the container
        /// </summary>
        public async Task ForceTerminateAsync()
        {
            _logger.LogWarningMessage("Force terminating isolated container for plugin {_pluginId}");

            try
            {
                // Cancel all operations immediately
                _shutdownTokenSource.Cancel();

                // Force terminate isolated process if exists
                if (_isolatedProcess != null && !_isolatedProcess.HasExited)
                {
                    _isolatedProcess.Kill();
                    await _isolatedProcess.WaitForExitAsync();
                }

                // Force unload assembly context
                if (_loadContext != null)
                {
                    _loadContext.Unload();
                }

                IsActive = false;
                _logger.LogWarningMessage("Force terminated isolated container for plugin {_pluginId}");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error during force termination for plugin {_pluginId}");
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            try
            {
                ShutdownAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error during disposal of container for plugin {_pluginId}");
            }

            _shutdownTokenSource?.Dispose();
            _operationLock?.Dispose();
            _loadContext?.Unload();

            _disposed = true;
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }


            try
            {
                await ShutdownAsync();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error during async disposal of container for plugin {_pluginId}");
            }

            _shutdownTokenSource?.Dispose();
            _operationLock?.Dispose();
            _loadContext?.Unload();

            _disposed = true;
        }
    }
}