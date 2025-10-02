// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Circuit breaker pattern implementation for plugin fault isolation
/// </summary>
public sealed class PluginCircuitBreaker : IDisposable
{
    private readonly PluginRecoveryConfiguration _config;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitStates;
    private readonly Timer _resetTimer;
    private volatile bool _disposed;

    public PluginCircuitBreaker(PluginRecoveryConfiguration config, ILogger logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _circuitStates = new ConcurrentDictionary<string, CircuitBreakerState>();

        // Timer to reset circuits periodically
        _resetTimer = new Timer(CheckAndResetCircuits, null,
            _config.CircuitBreakerResetInterval, _config.CircuitBreakerResetInterval);

        _logger.LogDebugMessage("Plugin Circuit Breaker initialized");
    }

    /// <summary>
    /// Checks if operation should be blocked due to circuit breaker
    /// </summary>
    public bool ShouldBlockOperation(string pluginId)
    {
        var state = _circuitStates.GetOrAdd(pluginId, id => new CircuitBreakerState { PluginId = id });

        lock (state)
        {
            return state.State switch
            {
                CircuitState.Closed => false,
                CircuitState.Open => !ShouldAttemptReset(state),
                CircuitState.HalfOpen => state.HalfOpenAttempts >= _config.CircuitBreakerHalfOpenMaxAttempts,
                _ => false
            };
        }
    }

    /// <summary>
    /// Records a successful operation
    /// </summary>
    public void RecordSuccess(string pluginId)
    {
        if (_circuitStates.TryGetValue(pluginId, out var state))
        {
            lock (state)
            {
                state.ConsecutiveFailures = 0;
                state.LastSuccessTime = DateTimeOffset.UtcNow;

                if (state.State == CircuitState.HalfOpen)
                {
                    state.State = CircuitState.Closed;
                    state.HalfOpenAttempts = 0;
                    _logger.LogInformation("Circuit breaker closed for plugin {PluginId} after successful operation", pluginId);
                }
            }
        }
    }

    /// <summary>
    /// Records a failed operation
    /// </summary>
    public void RecordFailure(string pluginId)
    {
        var state = _circuitStates.GetOrAdd(pluginId, id => new CircuitBreakerState { PluginId = id });

        lock (state)
        {
            state.ConsecutiveFailures++;
            state.LastFailureTime = DateTimeOffset.UtcNow;
            state.TotalFailures++;

            if (state.State == CircuitState.HalfOpen)
            {
                state.HalfOpenAttempts++;

                if (state.HalfOpenAttempts >= _config.CircuitBreakerHalfOpenMaxAttempts)
                {
                    state.State = CircuitState.Open;
                    state.OpenTime = DateTimeOffset.UtcNow;
                    _logger.LogWarning("Circuit breaker opened for plugin {PluginId} after {Attempts} half-open attempts",
                        pluginId, state.HalfOpenAttempts);
                }
            }
            else if (state.State == CircuitState.Closed &&
                     state.ConsecutiveFailures >= _config.CircuitBreakerFailureThreshold)
            {
                state.State = CircuitState.Open;
                state.OpenTime = DateTimeOffset.UtcNow;
                _logger.LogWarning("Circuit breaker opened for plugin {PluginId} after {Failures} consecutive failures",
                    pluginId, state.ConsecutiveFailures);
            }
        }
    }

    /// <summary>
    /// Checks if circuit breaker is open for a plugin
    /// </summary>
    public bool IsOpen(string pluginId)
    {
        if (!_circuitStates.TryGetValue(pluginId, out var state))
        {
            return false;
        }

        lock (state)
        {
            return state.State == CircuitState.Open;
        }
    }

    /// <summary>
    /// Gets the circuit breaker state for a plugin
    /// </summary>
    public CircuitBreakerStatus? GetState(string pluginId)
    {
        if (!_circuitStates.TryGetValue(pluginId, out var state))
        {
            return null;
        }

        lock (state)
        {
            return new CircuitBreakerStatus
            {
                PluginId = pluginId,
                State = state.State,
                ConsecutiveFailures = state.ConsecutiveFailures,
                TotalFailures = state.TotalFailures,
                LastFailureTime = state.LastFailureTime,
                LastSuccessTime = state.LastSuccessTime,
                OpenTime = state.OpenTime,
                HalfOpenAttempts = state.HalfOpenAttempts
            };
        }
    }

    /// <summary>
    /// Gets current circuit breaker status for all plugins
    /// </summary>
    public Dictionary<string, CircuitBreakerStatus> GetStatus()
    {
        return _circuitStates.ToDictionary(
            kvp => kvp.Key,
            kvp => new CircuitBreakerStatus
            {
                PluginId = kvp.Key,
                State = kvp.Value.State,
                ConsecutiveFailures = kvp.Value.ConsecutiveFailures,
                TotalFailures = kvp.Value.TotalFailures,
                LastFailureTime = kvp.Value.LastFailureTime,
                LastSuccessTime = kvp.Value.LastSuccessTime,
                OpenTime = kvp.Value.OpenTime,
                HalfOpenAttempts = kvp.Value.HalfOpenAttempts
            });
    }

    /// <summary>
    /// Manually resets circuit breaker for a plugin
    /// </summary>
    public void Reset(string pluginId)
    {
        if (_circuitStates.TryGetValue(pluginId, out var state))
        {
            lock (state)
            {
                state.State = CircuitState.Closed;
                state.ConsecutiveFailures = 0;
                state.HalfOpenAttempts = 0;
                state.OpenTime = null;
                _logger.LogInformation("Circuit breaker manually reset for plugin {PluginId}", pluginId);
            }
        }
    }

    /// <summary>
    /// Manually opens circuit breaker for a plugin
    /// </summary>
    public void ForceOpen(string pluginId, string reason)
    {
        var state = _circuitStates.GetOrAdd(pluginId, id => new CircuitBreakerState { PluginId = id });

        lock (state)
        {
            state.State = CircuitState.Open;
            state.OpenTime = DateTimeOffset.UtcNow;
            _logger.LogWarning("Circuit breaker forcibly opened for plugin {PluginId}: {Reason}", pluginId, reason);
        }
    }

    private bool ShouldAttemptReset(CircuitBreakerState state)
    {
        if (state.OpenTime == null)
        {
            return false;
        }


        var timeSinceOpen = DateTimeOffset.UtcNow - state.OpenTime.Value;
        if (timeSinceOpen >= _config.CircuitBreakerTimeout)
        {
            state.State = CircuitState.HalfOpen;
            state.HalfOpenAttempts = 0;
            _logger.LogInformation("Circuit breaker transitioning to half-open for plugin {PluginId}", state.PluginId);
            return true;
        }

        return false;
    }

    private void CheckAndResetCircuits(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            foreach (var circuitState in _circuitStates.Values)
            {
                lock (circuitState)
                {
                    if (circuitState.State == CircuitState.Open)
                    {
                        _ = ShouldAttemptReset(circuitState);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during circuit breaker reset check");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _resetTimer?.Dispose();
            _circuitStates.Clear();
            _disposed = true;
            _logger.LogDebugMessage("Plugin Circuit Breaker disposed");
        }
    }
}

/// <summary>
/// Internal state of a circuit breaker
/// </summary>
public sealed class CircuitBreakerState
{
    public required string PluginId { get; init; }
    public CircuitState State { get; set; } = CircuitState.Closed;
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
    public DateTimeOffset? LastFailureTime { get; set; }
    public DateTimeOffset? LastSuccessTime { get; set; }
    public DateTimeOffset? OpenTime { get; set; }
    public int HalfOpenAttempts { get; set; }
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitState
{
    Closed,    // Normal operation
    Open,      // Blocking all operations
    HalfOpen   // Testing with limited operations
}

/// <summary>
/// Public status of a circuit breaker
/// </summary>
public sealed class CircuitBreakerStatus
{
    public required string PluginId { get; init; }
    public CircuitState State { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int TotalFailures { get; init; }
    public DateTimeOffset? LastFailureTime { get; init; }
    public DateTimeOffset? LastSuccessTime { get; init; }
    public DateTimeOffset? OpenTime { get; init; }
    public int HalfOpenAttempts { get; init; }

    /// <summary>
    /// Gets whether the circuit breaker is open
    /// </summary>
    public bool IsOpen => State == CircuitState.Open;

    /// <summary>
    /// Gets the failure count (alias for TotalFailures)
    /// </summary>
    public int FailureCount => TotalFailures;
}