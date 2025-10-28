#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Lifecycle;

/// <summary>
/// Manages plugin lifecycle operations including initialization, state transitions, and shutdown.
/// </summary>
public sealed partial class AlgorithmPluginLifecycle : IDisposable
{
    private readonly ILogger<AlgorithmPluginLifecycle> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly ConcurrentDictionary<string, PluginLifecycleState> _pluginStates;
    private readonly SemaphoreSlim _stateLock;
    private readonly Timer? _healthCheckTimer;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the AlgorithmPluginLifecycle class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>

    public AlgorithmPluginLifecycle(ILogger<AlgorithmPluginLifecycle> logger, AlgorithmPluginManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options;
        _pluginStates = new ConcurrentDictionary<string, PluginLifecycleState>();
        _stateLock = new SemaphoreSlim(1, 1);

        if (_options.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(PerformHealthChecks, null,
                _options.HealthCheckInterval, _options.HealthCheckInterval);
        }
    }

    /// <summary>
    /// Initializes a plugin with proper state management and error handling.
    /// </summary>
    /// <param name="plugin">The plugin to initialize.</param>
    /// <param name="accelerator">The accelerator context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Initialization result.</returns>
    public async Task<PluginInitializationResult> InitializePluginAsync(
        IAlgorithmPlugin plugin,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(accelerator);

        var result = new PluginInitializationResult
        {
            PluginId = plugin.Id,
            StartTime = DateTime.UtcNow
        };

        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var state = GetOrCreatePluginState(plugin.Id);

            if (state.CurrentState != PluginState.Loaded)
            {
                result.Success = false;
                result.ErrorMessage = $"Plugin {plugin.Id} is not in loaded state (current: {state.CurrentState})";
                return result;
            }

            LogPluginInitializing(plugin.Id);
            state.TransitionTo(PluginState.Initializing);

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Initialize with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.InitializationTimeout);

                await plugin.InitializeAsync(accelerator, timeoutCts.Token);

                stopwatch.Stop();
                result.InitializationDuration = stopwatch.Elapsed;

                // Verify initialization
                if (await VerifyPluginInitializationAsync(plugin, cancellationToken))
                {
                    state.TransitionTo(PluginState.Running);
                    state.LastInitialization = DateTime.UtcNow;
                    state.InitializationCount++;

                    result.Success = true;
                    LogPluginInitialized(plugin.Id, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    state.TransitionTo(PluginState.Failed);
                    result.Success = false;
                    result.ErrorMessage = "Plugin initialization verification failed";
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                state.TransitionTo(PluginState.Failed);
                result.Success = false;
                result.ErrorMessage = "Plugin initialization was cancelled";
                LogPluginInitializationCancelled(plugin.Id);
            }
            catch (Exception ex)
            {
                state.TransitionTo(PluginState.Failed);
                state.LastError = ex;
                result.Success = false;
                result.ErrorMessage = ex.Message;
                LogPluginInitializationFailed(plugin.Id, ex.Message);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        finally
        {
            _ = _stateLock.Release();
        }
    }

    /// <summary>
    /// Shuts down a plugin with proper cleanup and state management.
    /// </summary>
    /// <param name="plugin">The plugin to shutdown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Shutdown result.</returns>
    public async Task<PluginShutdownResult> ShutdownPluginAsync(
        IAlgorithmPlugin plugin,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);

        var result = new PluginShutdownResult
        {
            PluginId = plugin.Id,
            StartTime = DateTime.UtcNow
        };

        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var state = GetOrCreatePluginState(plugin.Id);

            if (state.CurrentState == PluginState.Unloaded)
            {
                result.Success = true;
                result.Message = "Plugin already unloaded";
                return result;
            }

            LogPluginShuttingDown(plugin.Id);
            state.TransitionTo(PluginState.Stopping);

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Shutdown with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.ShutdownTimeout);

                await plugin.DisposeAsync();

                stopwatch.Stop();
                result.ShutdownDuration = stopwatch.Elapsed;

                state.TransitionTo(PluginState.Unloaded);
                state.LastShutdown = DateTime.UtcNow;
                state.ShutdownCount++;

                result.Success = true;
                LogPluginShutdown(plugin.Id, stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                state.TransitionTo(PluginState.Failed);
                result.Success = false;
                result.Message = "Plugin shutdown was cancelled";
                LogPluginShutdownCancelled(plugin.Id);
            }
            catch (Exception ex)
            {
                state.TransitionTo(PluginState.Failed);
                state.LastError = ex;
                result.Success = false;
                result.Message = ex.Message;
                LogPluginShutdownFailed(plugin.Id, ex.Message);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        finally
        {
            _ = _stateLock.Release();
        }
    }

    /// <summary>
    /// Restarts a plugin with proper state management.
    /// </summary>
    /// <param name="plugin">The plugin to restart.</param>
    /// <param name="accelerator">The accelerator context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Restart result.</returns>
    public async Task<PluginRestartResult> RestartPluginAsync(
        IAlgorithmPlugin plugin,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new PluginRestartResult
        {
            PluginId = plugin.Id,
            StartTime = DateTime.UtcNow
        };

        LogPluginRestarting(plugin.Id);

        try
        {
            // Shutdown first
            var shutdownResult = await ShutdownPluginAsync(plugin, cancellationToken);
            if (!shutdownResult.Success)
            {
                result.Success = false;
                result.Message = $"Shutdown failed: {shutdownResult.Message}";
                return result;
            }

            // Wait for cleanup
            await Task.Delay(_options.RestartDelay, cancellationToken);

            // Initialize again
            var initResult = await InitializePluginAsync(plugin, accelerator, cancellationToken);
            if (!initResult.Success)
            {
                result.Success = false;
                result.Message = $"Initialization failed: {initResult.ErrorMessage}";
                return result;
            }

            var state = GetOrCreatePluginState(plugin.Id);
            state.RestartCount++;
            state.LastRestart = DateTime.UtcNow;

            result.Success = true;
            result.ShutdownDuration = shutdownResult.ShutdownDuration;
            result.InitializationDuration = initResult.InitializationDuration;
            result.EndTime = DateTime.UtcNow;

            LogPluginRestarted(plugin.Id, result.TotalDuration.TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            LogPluginRestartFailed(plugin.Id, ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Gets the current state of a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>Plugin state information.</returns>
    public PluginStateInfo? GetPluginState(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_pluginStates.TryGetValue(pluginId, out var state))
        {
            return null;
        }

        return new PluginStateInfo
        {
            PluginId = pluginId,
            CurrentState = state.CurrentState,
            Health = state.Health,
            LastStateChange = state.LastStateChange,
            InitializationCount = state.InitializationCount,
            RestartCount = state.RestartCount,
            ShutdownCount = state.ShutdownCount,
            LastInitialization = state.LastInitialization,
            LastRestart = state.LastRestart,
            LastShutdown = state.LastShutdown,
            LastError = state.LastError,
            StateHistory = [.. state.StateHistory]
        };
    }

    /// <summary>
    /// Gets health information for all managed plugins.
    /// </summary>
    /// <returns>Collection of plugin health information.</returns>
    public IReadOnlyList<PluginHealthInfo> GetAllPluginHealth()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _pluginStates.Values.Select(state => new PluginHealthInfo
        {
            PluginId = state.PluginId,
            CurrentState = state.CurrentState,
            Health = state.Health,
            LastHealthCheck = state.LastHealthCheck,
            HealthCheckCount = state.HealthCheckCount,
            LastError = state.LastError,
            Uptime = state.CurrentState == PluginState.Running && state.LastInitialization.HasValue
                ? DateTime.UtcNow - state.LastInitialization.Value
                : TimeSpan.Zero
        }).ToList().AsReadOnly();
    }

    /// <summary>
    /// Performs health checks on all managed plugins.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void PerformHealthChecks(object? state)
    {
        if (_disposed)
        {
            return;
        }


        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var pluginState in _pluginStates.Values)
                {
                    await CheckPluginHealthAsync(pluginState);
                }
            }
            catch (Exception ex)
            {
                LogHealthCheckError(ex);
            }
        });
    }

    /// <summary>
    /// Checks the health of a specific plugin.
    /// </summary>
    /// <param name="pluginState">The plugin state to check.</param>
    private async Task CheckPluginHealthAsync(PluginLifecycleState pluginState)
    {
        try
        {
            pluginState.HealthCheckCount++;
            pluginState.LastHealthCheck = DateTime.UtcNow;

            // Update health based on current state and error history
            var previousHealth = pluginState.Health;

            if (pluginState.CurrentState == PluginState.Failed)
            {
                pluginState.Health = PluginHealth.Critical;
            }
            else if (pluginState.CurrentState == PluginState.Running)
            {
                // Check for recent errors
                if (pluginState.LastError != null &&
                    DateTime.UtcNow - pluginState.LastStateChange < TimeSpan.FromMinutes(5))
                {
                    pluginState.Health = PluginHealth.Degraded;
                }
                else
                {
                    pluginState.Health = PluginHealth.Healthy;
                }
            }
            else
            {
                pluginState.Health = PluginHealth.Unknown;
            }

            if (previousHealth != pluginState.Health)
            {
                LogPluginHealthChanged(pluginState.PluginId, previousHealth, pluginState.Health);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogHealthCheckFailed(ex, pluginState.PluginId);
            pluginState.Health = PluginHealth.Critical;
        }
    }

    /// <summary>
    /// Verifies that a plugin was properly initialized.
    /// </summary>
    /// <param name="plugin">The plugin to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if initialization was successful; otherwise, false.</returns>
    private static async Task<bool> VerifyPluginInitializationAsync(
        IAlgorithmPlugin plugin,
        CancellationToken cancellationToken)
    {
        try
        {
            // Basic verification - check if plugin responds to metadata queries
            _ = plugin.Id;
            _ = plugin.Name;
            _ = plugin.Version;

            await Task.CompletedTask;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets or creates a plugin lifecycle state.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>Plugin lifecycle state.</returns>
    private PluginLifecycleState GetOrCreatePluginState(string pluginId) => _pluginStates.GetOrAdd(pluginId, id => new PluginLifecycleState(id));
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _stateLock.Dispose();
            _pluginStates.Clear();
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing plugin {PluginId}")]
    private partial void LogPluginInitializing(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} initialized in {ElapsedMs}ms")]
    private partial void LogPluginInitialized(string pluginId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} initialization was cancelled")]
    private partial void LogPluginInitializationCancelled(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} initialization failed: {Reason}")]
    private partial void LogPluginInitializationFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutting down plugin {PluginId}")]
    private partial void LogPluginShuttingDown(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} shutdown in {ElapsedMs}ms")]
    private partial void LogPluginShutdown(string pluginId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} shutdown was cancelled")]
    private partial void LogPluginShutdownCancelled(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} shutdown failed: {Reason}")]
    private partial void LogPluginShutdownFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restarting plugin {PluginId}")]
    private partial void LogPluginRestarting(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} restarted in {ElapsedMs}ms")]
    private partial void LogPluginRestarted(string pluginId, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} restart failed: {Reason}")]
    private partial void LogPluginRestartFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} health changed: {OldHealth} -> {NewHealth}")]
    private partial void LogPluginHealthChanged(string pluginId, PluginHealth oldHealth, PluginHealth newHealth);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during health checks")]
    private partial void LogHealthCheckError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Health check failed for plugin {PluginId}")]
    private partial void LogHealthCheckFailed(Exception ex, string pluginId);

    #endregion
}

/// <summary>
/// Represents the lifecycle state of a plugin.
/// </summary>
internal sealed class PluginLifecycleState(string pluginId)
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    /// <value>The plugin id.</value>
    public string PluginId { get; } = pluginId;
    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    /// <value>The current state.</value>
    public PluginState CurrentState { get; private set; } = PluginState.Loaded;
    /// <summary>
    /// Gets or sets the health.
    /// </summary>
    /// <value>The health.</value>
    public PluginHealth Health { get; set; } = PluginHealth.Unknown;
    /// <summary>
    /// Gets or sets the last state change.
    /// </summary>
    /// <value>The last state change.</value>
    public DateTime LastStateChange { get; private set; } = DateTime.UtcNow;
    /// <summary>
    /// Gets or sets the last error.
    /// </summary>
    /// <value>The last error.</value>
    public Exception? LastError { get; set; }
    /// <summary>
    /// Gets or sets the initialization count.
    /// </summary>
    /// <value>The initialization count.</value>

    public int InitializationCount { get; set; }
    /// <summary>
    /// Gets or sets the restart count.
    /// </summary>
    /// <value>The restart count.</value>
    public int RestartCount { get; set; }
    /// <summary>
    /// Gets or sets the shutdown count.
    /// </summary>
    /// <value>The shutdown count.</value>
    public int ShutdownCount { get; set; }
    /// <summary>
    /// Gets or sets the health check count.
    /// </summary>
    /// <value>The health check count.</value>
    public int HealthCheckCount { get; set; }
    /// <summary>
    /// Gets or sets the last initialization.
    /// </summary>
    /// <value>The last initialization.</value>

    public DateTime? LastInitialization { get; set; }
    /// <summary>
    /// Gets or sets the last restart.
    /// </summary>
    /// <value>The last restart.</value>
    public DateTime? LastRestart { get; set; }
    /// <summary>
    /// Gets or sets the last shutdown.
    /// </summary>
    /// <value>The last shutdown.</value>
    public DateTime? LastShutdown { get; set; }
    /// <summary>
    /// Gets or sets the last health check.
    /// </summary>
    /// <value>The last health check.</value>
    public DateTime? LastHealthCheck { get; set; }
    /// <summary>
    /// Gets or sets the state history.
    /// </summary>
    /// <value>The state history.</value>

    public Queue<StateTransition> StateHistory { get; } = new();
    /// <summary>
    /// Performs transition to.
    /// </summary>
    /// <param name="newState">The new state.</param>

    public void TransitionTo(PluginState newState)
    {
        var oldState = CurrentState;
        CurrentState = newState;
        LastStateChange = DateTime.UtcNow;

        StateHistory.Enqueue(new StateTransition(oldState, newState, LastStateChange));

        // Keep only recent history
        while (StateHistory.Count > 50)
        {
            _ = StateHistory.Dequeue();
        }
    }
}
/// <summary>
/// A class that represents state transition.
/// </summary>

/// <summary>
/// Represents a state transition in plugin lifecycle.
/// </summary>
public sealed record StateTransition(PluginState FromState, PluginState ToState, DateTime Timestamp);

/// <summary>
/// Result of plugin initialization operation.
/// </summary>
public sealed class PluginInitializationResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets whether initialization was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the initialization start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the initialization end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the initialization duration.
    /// </summary>
    public TimeSpan InitializationDuration { get; set; }
}

/// <summary>
/// Result of plugin shutdown operation.
/// </summary>
public sealed class PluginShutdownResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets whether shutdown was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the result message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the shutdown start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the shutdown end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the shutdown duration.
    /// </summary>
    public TimeSpan ShutdownDuration { get; set; }
}

/// <summary>
/// Result of plugin restart operation.
/// </summary>
public sealed class PluginRestartResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets whether restart was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the result message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the restart start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the restart end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the shutdown duration.
    /// </summary>
    public TimeSpan ShutdownDuration { get; set; }

    /// <summary>
    /// Gets or sets the initialization duration.
    /// </summary>
    public TimeSpan InitializationDuration { get; set; }

    /// <summary>
    /// Gets the total restart duration.
    /// </summary>
    public TimeSpan TotalDuration => EndTime - StartTime;
}

/// <summary>
/// Information about plugin state.
/// </summary>
public sealed class PluginStateInfo
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    public PluginState CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the health status.
    /// </summary>
    public PluginHealth Health { get; set; }

    /// <summary>
    /// Gets or sets the last state change time.
    /// </summary>
    public DateTime LastStateChange { get; set; }

    /// <summary>
    /// Gets or sets the initialization count.
    /// </summary>
    public int InitializationCount { get; set; }

    /// <summary>
    /// Gets or sets the restart count.
    /// </summary>
    public int RestartCount { get; set; }

    /// <summary>
    /// Gets or sets the shutdown count.
    /// </summary>
    public int ShutdownCount { get; set; }

    /// <summary>
    /// Gets or sets the last initialization time.
    /// </summary>
    public DateTime? LastInitialization { get; set; }

    /// <summary>
    /// Gets or sets the last restart time.
    /// </summary>
    public DateTime? LastRestart { get; set; }

    /// <summary>
    /// Gets or sets the last shutdown time.
    /// </summary>
    public DateTime? LastShutdown { get; set; }

    /// <summary>
    /// Gets or sets the last error.
    /// </summary>
    public Exception? LastError { get; set; }

    /// <summary>
    /// Gets the state transition history.
    /// </summary>
    public IReadOnlyList<StateTransition> StateHistory { get; init; } = [];
}

/// <summary>
/// Health information for a plugin.
/// </summary>
public sealed class PluginHealthInfo
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    public PluginState CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the health status.
    /// </summary>
    public PluginHealth Health { get; set; }

    /// <summary>
    /// Gets or sets the last health check time.
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>
    /// Gets or sets the health check count.
    /// </summary>
    public int HealthCheckCount { get; set; }

    /// <summary>
    /// Gets or sets the last error.
    /// </summary>
    public Exception? LastError { get; set; }

    /// <summary>
    /// Gets or sets the plugin uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }
}