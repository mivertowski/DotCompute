
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
// Using PluginState from Abstractions project

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Manages the lifecycle of algorithm plugins including initialization, start/stop, and shutdown.
    /// </summary>
    public sealed partial class AlgorithmPluginLifecycle : IHostedService, IDisposable
    {
        private readonly ILogger<AlgorithmPluginLifecycle> _logger;
        private readonly AlgorithmPluginManagerOptions _options;
        private readonly ConcurrentDictionary<string, PluginLifecycleState> _pluginStates = new();
        private readonly SemaphoreSlim _lifecycleSemaphore = new(1, 1);
        private readonly Timer? _healthCheckTimer;
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the AlgorithmPluginLifecycle class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="options">The options.</param>

        public AlgorithmPluginLifecycle(ILogger<AlgorithmPluginLifecycle> logger, AlgorithmPluginManagerOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            // Setup health check timer if enabled
            if (_options.EnableHealthChecks)
            {
                _healthCheckTimer = new Timer(
                    PerformHealthCheck,
                    null,
                    _options.HealthCheckInterval,
                    _options.HealthCheckInterval);
            }
        }

        /// <summary>
        /// Initializes a plugin asynchronously.
        /// </summary>
        /// <param name="plugin">The plugin to initialize.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if initialization was successful; otherwise, false.</returns>
        public async Task<bool> InitializePluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            await _lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var state = GetOrCreatePluginState(plugin);
                if (state.State != PluginState.Loading)
                {
                    LogPluginAlreadyInitialized(plugin.Id);
                    return state.State == PluginState.Loaded;
                }

                LogInitializingPlugin(plugin.Id);
                state.SetState(PluginState.Initializing);

                try
                {
                    // TODO: Get appropriate accelerator for plugin initialization
                    // For now, skip initialization that requires accelerator
                    // await plugin.InitializeAsync(accelerator, _logger).ConfigureAwait(false);
                    state.SetState(PluginState.Loaded);
                    state.LastActivityTime = DateTime.UtcNow;
                    LogPluginInitialized(plugin.Id);
                    return true;
                }
                catch (Exception ex)
                {
                    state.SetState(PluginState.Failed);
                    state.LastError = ex;
                    LogPluginInitializationFailed(plugin.Id, ex.Message);
                    return false;
                }
            }
            finally
            {
                _ = _lifecycleSemaphore.Release();
            }
        }

        /// <summary>
        /// Starts a plugin asynchronously.
        /// </summary>
        /// <param name="plugin">The plugin to start.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the plugin was started successfully; otherwise, false.</returns>
        public async Task<bool> StartPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            await _lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var state = GetOrCreatePluginState(plugin);
                if (state.State == PluginState.Running)
                {
                    LogPluginAlreadyRunning(plugin.Id);
                    return true;
                }

                if (state.State != PluginState.Loaded)
                {
                    LogPluginNotInitialized(plugin.Id);
                    return false;
                }

                LogStartingPlugin(plugin.Id);
                state.SetState(PluginState.Running);

                try
                {
                    // IAlgorithmPlugin doesn't have StartAsync - just mark as running
                    // Actual execution happens via ExecuteAsync when needed
                    state.SetState(PluginState.Running);
                    state.LastActivityTime = DateTime.UtcNow;
                    LogPluginStarted(plugin.Id);
                    return true;
                }
                catch (Exception ex)
                {
                    state.SetState(PluginState.Failed);
                    state.LastError = ex;
                    LogPluginStartFailed(plugin.Id, ex.Message);
                    return false;
                }
            }
            finally
            {
                _ = _lifecycleSemaphore.Release();
            }
        }

        /// <summary>
        /// Stops a plugin asynchronously.
        /// </summary>
        /// <param name="plugin">The plugin to stop.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the plugin was stopped successfully; otherwise, false.</returns>
        public async Task<bool> StopPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            await _lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var state = GetOrCreatePluginState(plugin);
                if (state.State is PluginState.Unloaded or PluginState.Loading)
                {
                    LogPluginAlreadyStopped(plugin.Id);
                    return true;
                }

                LogStoppingPlugin(plugin.Id);
                state.SetState(PluginState.Stopping);

                try
                {
                    // IAlgorithmPlugin doesn't have StopAsync - just mark as stopped
                    // Cleanup happens via DisposeAsync
                    state.SetState(PluginState.Unloaded);
                    state.LastActivityTime = DateTime.UtcNow;
                    LogPluginStopped(plugin.Id);
                    return true;
                }
                catch (Exception ex)
                {
                    state.SetState(PluginState.Failed);
                    state.LastError = ex;
                    LogPluginStopFailed(plugin.Id, ex.Message);
                    return false;
                }
            }
            finally
            {
                _ = _lifecycleSemaphore.Release();
            }
        }

        /// <summary>
        /// Shuts down a plugin asynchronously and disposes its resources.
        /// </summary>
        /// <param name="plugin">The plugin to shutdown.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if shutdown was successful; otherwise, false.</returns>
        public async Task<bool> ShutdownPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            await _lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var state = GetOrCreatePluginState(plugin);
                LogShuttingDownPlugin(plugin.Id);

                try
                {
                    // Stop the plugin first if running
                    if (state.State == PluginState.Running)
                    {
                        _ = await StopPluginAsync(plugin, cancellationToken).ConfigureAwait(false);
                    }

                    // Dispose if IDisposable
                    if (plugin is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }

                    // Dispose if IAsyncDisposable
                    if (plugin is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }

                    state.SetState(PluginState.Unloaded);
                    state.LastActivityTime = DateTime.UtcNow;
                    LogPluginShutdown(plugin.Id);
                    return true;
                }
                catch (Exception ex)
                {
                    state.SetState(PluginState.Failed);
                    state.LastError = ex;
                    LogPluginShutdownFailed(plugin.Id, ex.Message);
                    return false;
                }
                finally
                {
                    // Remove from tracking after shutdown
                    _ = _pluginStates.TryRemove(plugin.Id, out _);
                }
            }
            finally
            {
                _ = _lifecycleSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the current state of a plugin.
        /// </summary>
        /// <param name="pluginId">The plugin ID.</param>
        /// <returns>The plugin state or null if not found.</returns>
        public PluginLifecycleState? GetPluginState(string pluginId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            return _pluginStates.TryGetValue(pluginId, out var state) ? state : null;
        }

        /// <summary>
        /// Gets all plugin states.
        /// </summary>
        /// <returns>A read-only dictionary of plugin states.</returns>
        public IReadOnlyDictionary<string, PluginLifecycleState> GetAllPluginStates() => _pluginStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        /// <summary>
        /// Performs health checks on all running plugins.
        /// </summary>
        private void PerformHealthCheck(object? state)
        {
            if (_disposed)
            {
                return;
            }


            _ = Task.Run(() =>
            {
                try
                {
                    var unhealthyPlugins = new List<string>();
                    var currentTime = DateTime.UtcNow;

                    foreach (var kvp in _pluginStates)
                    {
                        var pluginState = kvp.Value;
                        var timeSinceActivity = currentTime - pluginState.LastActivityTime;

                        // Check for timeout
                        if (timeSinceActivity > _options.PluginTimeout)
                        {
                            unhealthyPlugins.Add(kvp.Key);
                            LogPluginTimeout(kvp.Key, timeSinceActivity.TotalMinutes);
                        }

                        // Check for failed state
                        if (pluginState.State == PluginState.Failed && pluginState.LastError != null)
                        {
                            LogPluginUnhealthy(kvp.Key, pluginState.LastError.Message);
                        }
                    }

                    if (unhealthyPlugins.Count > 0)
                    {
                        LogUnhealthyPluginsDetected(unhealthyPlugins.Count, string.Join(", ", unhealthyPlugins));
                    }
                }
                catch (Exception ex)
                {
                    LogHealthCheckFailed(ex.Message);
                }
            });
        }

        private PluginLifecycleState GetOrCreatePluginState(IAlgorithmPlugin plugin) => _pluginStates.GetOrAdd(plugin.Id, _ => new PluginLifecycleState(plugin.Id));
        /// <summary>
        /// Gets start asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        #region IHostedService Implementation

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            LogLifecycleManagerStarting();
            await Task.CompletedTask;
        }
        /// <summary>
        /// Gets stop asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            LogLifecycleManagerStopping();

            // Stop all running plugins
            var stopTasks = _pluginStates.Values
                .Where(state => state.State == PluginState.Running)
                .Select(async state =>
                {
                    try
                    {
                        // We don't have direct access to plugin instances here,
                        // so we just mark them as stopping
                        state.SetState(PluginState.Stopping);
                        await Task.CompletedTask;
                    }
                    catch (Exception ex)
                    {
                        LogPluginStopFailed(state.PluginId, ex.Message);
                    }
                });

            await Task.WhenAll(stopTasks);
            LogLifecycleManagerStopped();
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _healthCheckTimer?.Dispose();
                _lifecycleSemaphore.Dispose();
                _pluginStates.Clear();
                LogLifecycleDisposed();
            }
        }

        // Logger messages
        [LoggerMessage(Level = LogLevel.Information, Message = "Initializing plugin {PluginId}")]
        private partial void LogInitializingPlugin(string pluginId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} initialized successfully")]
        private partial void LogPluginInitialized(string pluginId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is already initialized")]
        private partial void LogPluginAlreadyInitialized(string pluginId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} initialization failed: {Reason}")]
        private partial void LogPluginInitializationFailed(string pluginId, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Starting plugin {PluginId}")]
        private partial void LogStartingPlugin(string pluginId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} started successfully")]
        private partial void LogPluginStarted(string pluginId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is already running")]
        private partial void LogPluginAlreadyRunning(string pluginId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is not initialized")]
        private partial void LogPluginNotInitialized(string pluginId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} start failed: {Reason}")]
        private partial void LogPluginStartFailed(string pluginId, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Stopping plugin {PluginId}")]
        private partial void LogStoppingPlugin(string pluginId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} stopped successfully")]
        private partial void LogPluginStopped(string pluginId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is already stopped")]
        private partial void LogPluginAlreadyStopped(string pluginId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} stop failed: {Reason}")]
        private partial void LogPluginStopFailed(string pluginId, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Shutting down plugin {PluginId}")]
        private partial void LogShuttingDownPlugin(string pluginId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} shutdown successfully")]
        private partial void LogPluginShutdown(string pluginId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} shutdown failed: {Reason}")]
        private partial void LogPluginShutdownFailed(string pluginId, string reason);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} timeout detected, inactive for {Minutes:F1} minutes")]
        private partial void LogPluginTimeout(string pluginId, double minutes);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is unhealthy: {Reason}")]
        private partial void LogPluginUnhealthy(string pluginId, string reason);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Health check detected {Count} unhealthy plugins: {PluginIds}")]
        private partial void LogUnhealthyPluginsDetected(int count, string pluginIds);

        [LoggerMessage(Level = LogLevel.Error, Message = "Health check failed: {Reason}")]
        private partial void LogHealthCheckFailed(string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin lifecycle manager starting")]
        private partial void LogLifecycleManagerStarting();

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin lifecycle manager stopping")]
        private partial void LogLifecycleManagerStopping();

        [LoggerMessage(Level = LogLevel.Information, Message = "Plugin lifecycle manager stopped")]
        private partial void LogLifecycleManagerStopped();

        [LoggerMessage(Level = LogLevel.Information, Message = "AlgorithmPluginLifecycle disposed")]
        private partial void LogLifecycleDisposed();
    }

    /// <summary>
    /// Represents the lifecycle state of a plugin.
    /// </summary>
    public sealed class PluginLifecycleState(string pluginId)
    {
        private readonly Lock _stateLock = new();
        private PluginState _state = PluginState.Loading;
        /// <summary>
        /// Gets or sets the plugin identifier.
        /// </summary>
        /// <value>The plugin id.</value>

        public string PluginId { get; } = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>

        public PluginState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _state;
                }
            }
        }
        /// <summary>
        /// Gets or sets the last activity time.
        /// </summary>
        /// <value>The last activity time.</value>

        public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Gets or sets the last error.
        /// </summary>
        /// <value>The last error.</value>

        public Exception? LastError { get; set; }
        /// <summary>
        /// Sets the state.
        /// </summary>
        /// <param name="newState">The new state.</param>

        public void SetState(PluginState newState)
        {
            lock (_stateLock)
            {
                _state = newState;
                LastActivityTime = DateTime.UtcNow;

                if (newState != PluginState.Failed)
                {
                    LastError = null;
                }
            }
        }
    }
}