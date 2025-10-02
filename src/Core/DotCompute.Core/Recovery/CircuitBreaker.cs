// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Core.Recovery.Statistics;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Circuit breaker implementation for network/distributed system failures
/// with exponential backoff, failure rate monitoring, and automatic recovery
/// </summary>
public sealed class CircuitBreaker : IDisposable
{
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly CircuitBreakerConfiguration _config;
    private readonly ConcurrentDictionary<string, ServiceCircuitState> _serviceStates;
    private readonly Timer _healthCheckTimer;
    private readonly object _stateLock = new();
    private bool _disposed;

    // Global circuit state
    private CircuitState _globalState = CircuitState.Closed;
    private DateTimeOffset _lastStateChange = DateTimeOffset.UtcNow;
    private int _consecutiveFailures;
    private long _totalRequests;
    private long _failedRequests;

    public CircuitBreaker(ILogger<CircuitBreaker> logger, CircuitBreakerConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? CircuitBreakerConfiguration.Default;
        _serviceStates = new ConcurrentDictionary<string, ServiceCircuitState>();


        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            _config.HealthCheckInterval, _config.HealthCheckInterval);

        _logger.LogInfoMessage($"Circuit Breaker initialized with failure threshold {_config.FailureThresholdPercentage}% and timeout {_config.OpenCircuitTimeout}");
    }

    /// <summary>
    /// Executes an operation through the circuit breaker
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        string serviceName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Check if circuit is open for this service
        if (!CanExecute(serviceName))
        {
            var exception = new CircuitOpenException($"Circuit breaker is OPEN for service '{serviceName}'");
            RecordFailure(serviceName, exception);
            throw exception;
        }

        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogDebugMessage("Executing operation {OperationId} for service {operationId, serviceName}");

        try
        {
            // Add timeout to the operation
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_config.OperationTimeout);

            var result = await operation(timeoutCts.Token);

            stopwatch.Stop();
            RecordSuccess(serviceName, stopwatch.Elapsed);

            _logger.LogDebugMessage($"Operation {operationId} succeeded for service {serviceName} in {stopwatch.ElapsedMilliseconds}ms");

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User-requested cancellation - don't count as failure
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordFailure(serviceName, ex);

            _logger.LogWarning(ex, "Operation {OperationId} failed for service {ServiceName} after {Duration}ms",
                operationId, serviceName, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    /// <summary>
    /// Executes an operation with retry logic and circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(
        string serviceName,
        Func<CancellationToken, Task<T>> operation,
        RetryPolicy? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        var policy = retryPolicy ?? RetryPolicy.Default;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= policy.MaxRetries; attempt++)
        {
            try
            {
                return await ExecuteAsync(serviceName, operation, cancellationToken);
            }
            catch (CircuitOpenException)
            {
                // Don't retry if circuit is open
                throw;
            }
            catch (Exception ex) when (attempt < policy.MaxRetries)
            {
                lastException = ex;

                // Calculate delay with exponential backoff and jitter
                var delay = CalculateRetryDelay(attempt, policy);

                _logger.LogWarningMessage($"Operation attempt {attempt}/{policy.MaxRetries} failed for service {serviceName}, retrying in {delay.TotalMilliseconds}ms: {ex.Message}");

                await Task.Delay(delay, cancellationToken);
            }
        }

        // All retries exhausted
        throw lastException ?? new InvalidOperationException("All retry attempts failed");
    }

    /// <summary>
    /// Checks if the circuit allows execution for a service
    /// </summary>
    public bool CanExecute(string serviceName)
    {
        var serviceState = GetOrCreateServiceState(serviceName);
        return serviceState.CanExecute() && _globalState != CircuitState.Open;
    }

    /// <summary>
    /// Gets the current state of a service circuit
    /// </summary>
    public CircuitState GetServiceState(string serviceName)
    {
        var serviceState = GetOrCreateServiceState(serviceName);
        return serviceState.State;
    }

    /// <summary>
    /// Gets comprehensive circuit breaker statistics
    /// </summary>
    public CircuitBreakerStatistics GetStatistics()
    {
        var serviceStats = _serviceStates.ToDictionary(
            kvp => kvp.Key,
            kvp => new ServiceStatistics
            {
                ServiceName = kvp.Key,
                State = kvp.Value.State,
                TotalRequests = kvp.Value.TotalRequests,
                FailedRequests = kvp.Value.FailedRequests,
                FailureRate = kvp.Value.FailureRate,
                LastFailure = DateTimeOffset.UtcNow // Default value since property doesn't exist
            }
        );

        var overallFailureRate = _totalRequests > 0 ? (double)_failedRequests / _totalRequests * 100.0 : 0.0;

        return new CircuitBreakerStatistics
        {
            GlobalState = _globalState,
            OverallFailureRate = overallFailureRate,
            TotalRequests = _totalRequests,
            FailedRequests = _failedRequests,
            ConsecutiveFailures = _consecutiveFailures,
            ServiceStatistics = serviceStats,
            LastStateChange = _lastStateChange,
            ActiveServices = _serviceStates.Count
        };
    }

    /// <summary>
    /// Forces a service circuit to a specific state (for testing/manual intervention)
    /// </summary>
    public void ForceServiceState(string serviceName, CircuitState state)
    {
        var serviceState = GetOrCreateServiceState(serviceName);
        serviceState.ForceState(state);

        _logger.LogWarningMessage("Service {ServiceName} circuit forced to {serviceName, state}");
    }

    /// <summary>
    /// Resets a service circuit to closed state
    /// </summary>
    public void ResetService(string serviceName)
    {
        if (_serviceStates.TryGetValue(serviceName, out var serviceState))
        {
            serviceState.Reset();
            _logger.LogInfoMessage("Service {serviceName} circuit reset to CLOSED");
        }
    }

    /// <summary>
    /// Resets all service circuits
    /// </summary>
    public void ResetAll()
    {
        foreach (var serviceState in _serviceStates.Values)
        {
            serviceState.Reset();
        }

        lock (_stateLock)
        {
            _globalState = CircuitState.Closed;
            _consecutiveFailures = 0;
            _lastStateChange = DateTimeOffset.UtcNow;
        }

        _logger.LogInfoMessage("All circuits reset to CLOSED state");
    }

    private ServiceCircuitState GetOrCreateServiceState(string serviceName)
    {
        return _serviceStates.GetOrAdd(serviceName, name =>
            new ServiceCircuitState(name, _config, _logger));
    }

    private void RecordSuccess(string serviceName, TimeSpan duration)
    {
        var serviceState = GetOrCreateServiceState(serviceName);
        serviceState.RecordSuccess(duration);

        lock (_stateLock)
        {
            _ = Interlocked.Increment(ref _totalRequests);
            _consecutiveFailures = 0;

            // Transition from half-open to closed if needed
            if (_globalState == CircuitState.HalfOpen)
            {
                TransitionToState(CircuitState.Closed, "Successful operation in half-open state");
            }
        }
    }

    private void RecordFailure(string serviceName, Exception exception)
    {
        var serviceState = GetOrCreateServiceState(serviceName);
        serviceState.RecordFailure(exception);

        lock (_stateLock)
        {
            _ = Interlocked.Increment(ref _totalRequests);
            _ = Interlocked.Increment(ref _failedRequests);
            _consecutiveFailures++;

            // Check if global circuit should open
            var failureRate = (double)_failedRequests / _totalRequests * 100.0;

            if (_globalState == CircuitState.Closed &&
                (failureRate >= _config.FailureThresholdPercentage ||
                 _consecutiveFailures >= _config.ConsecutiveFailureThreshold))
            {
                TransitionToState(CircuitState.Open, $"Failure rate {failureRate:F1}% exceeded threshold");
            }
            else if (_globalState == CircuitState.HalfOpen)
            {
                TransitionToState(CircuitState.Open, "Failure in half-open state");
            }
        }
    }

    private void TransitionToState(CircuitState newState, string reason)
    {
        var oldState = _globalState;
        _globalState = newState;
        _lastStateChange = DateTimeOffset.UtcNow;

        _logger.LogWarningMessage($"Global circuit breaker state changed from {oldState} to {newState}: {reason}");
    }

    private static TimeSpan CalculateRetryDelay(int attempt, RetryPolicy policy)
    {
        var baseDelay = policy.BaseDelay;
        var exponentialDelay = TimeSpan.FromTicks((long)(baseDelay.Ticks * Math.Pow(policy.BackoffMultiplier, attempt - 1)));

        // Apply jitter to prevent thundering herd
        var jitter = Random.Shared.NextDouble() * policy.JitterFactor;
        var finalDelay = TimeSpan.FromTicks((long)(exponentialDelay.Ticks * (1.0 + jitter)));

        return finalDelay > policy.MaxDelay ? policy.MaxDelay : finalDelay;
    }

    private void PerformHealthCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;

            // Check if global circuit should transition from open to half-open
            lock (_stateLock)
            {
                if (_globalState == CircuitState.Open &&
                    (now - _lastStateChange) >= _config.OpenCircuitTimeout)
                {
                    TransitionToState(CircuitState.HalfOpen, "Timeout expired, trying half-open");
                }
            }

            // Health check for individual services
            foreach (var serviceState in _serviceStates.Values)
            {
                serviceState.PerformHealthCheck();
            }

            // Log statistics periodically
            if (now.Minute % 5 == 0) // Every 5 minutes
            {
                var stats = GetStatistics();
                _logger.LogInfoMessage($"Circuit Breaker Stats - Global: {_globalState}, Failure Rate: {stats.OverallFailureRate:F1}%, Services: {_serviceStates.Count}, Total Requests: {_totalRequests}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during circuit breaker health check");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();

            foreach (var serviceState in _serviceStates.Values)
            {
                serviceState.Dispose();
            }

            _disposed = true;
            _logger.LogInfoMessage("Circuit Breaker disposed");
        }
    }
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed, requests flow normally
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, requests are blocked
    /// </summary>
    Open,


    /// <summary>
    /// Circuit is half-open, limited requests are allowed to test recovery
    /// </summary>
    HalfOpen
}

/// <summary>
/// Exception thrown when circuit breaker is open
/// </summary>
public class CircuitOpenException : Exception
{
    public CircuitOpenException(string message) : base(message) { }
    public CircuitOpenException(string message, Exception innerException) : base(message, innerException) { }
    public CircuitOpenException()
    {
    }
}

/// <summary>
/// Configuration for circuit breaker behavior
/// </summary>
public class CircuitBreakerConfiguration
{
    public double FailureThresholdPercentage { get; set; } = 50.0; // 50%
    public int ConsecutiveFailureThreshold { get; set; } = 5;
    public TimeSpan OpenCircuitTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MinimumThroughput { get; set; } = 10; // Minimum requests before calculating failure rate


    public static CircuitBreakerConfiguration Default => new();


    public override string ToString()

        => $"FailureThreshold={FailureThresholdPercentage}%, ConsecutiveFailures={ConsecutiveFailureThreshold}, " +
        $"Timeout={OpenCircuitTimeout}, OpTimeout={OperationTimeout}";
}

/// <summary>
/// Retry policy for operations
/// </summary>
public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public double BackoffMultiplier { get; set; } = 2.0;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public double JitterFactor { get; set; } = 0.1; // 10% jitter


    public static RetryPolicy Default => new();


    public override string ToString()

        => $"MaxRetries={MaxRetries}, BaseDelay={BaseDelay}, Backoff={BackoffMultiplier}x";
}



// Additional supporting types would continue in separate files...
