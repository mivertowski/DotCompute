// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Core.Recovery.Statistics;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Represents the state of a circuit breaker for a specific service
/// </summary>
/// <remarks>
/// Creates a new service circuit state
/// </remarks>
public class ServiceCircuitState(string serviceName, CircuitBreakerConfiguration config, ILogger logger)
{
    /// <summary>
    /// Service identifier
    /// </summary>
    public string ServiceName { get; set; } = serviceName ?? throw new ArgumentNullException(nameof(serviceName));

    /// <summary>
    /// Current circuit state
    /// </summary>
    public CircuitState State { get; set; } = CircuitState.Closed;

    /// <summary>
    /// Last time the state changed
    /// </summary>
    public DateTimeOffset LastStateChange { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }


    /// <summary>
    /// Total number of requests made to this service
    /// </summary>
    public long TotalRequests { get; set; }


    /// <summary>
    /// Total number of failed requests
    /// </summary>
    public long FailedRequests { get; set; }


    /// <summary>
    /// Total number of successful requests
    /// </summary>
    public long SuccessfulRequests { get; set; }


    /// <summary>
    /// Current failure rate (0.0 to 1.0)
    /// </summary>
    public double FailureRate => TotalRequests > 0 ? (double)FailedRequests / TotalRequests : 0.0;

    /// <summary>
    /// Current success rate (0.0 to 1.0)
    /// </summary>
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0.0;

    /// <summary>
    /// Time when the circuit can transition from Open to Half-Open
    /// </summary>
    public DateTimeOffset? NextRetryTime { get; set; }

    /// <summary>
    /// Average response time for successful requests
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Last recorded error message
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Last error timestamp
    /// </summary>
    public DateTimeOffset? LastErrorTime { get; set; }

    /// <summary>
    /// Number of times the circuit has been opened
    /// </summary>
    public int OpenCount { get; set; }


    /// <summary>
    /// Total time the circuit has been open
    /// </summary>
    public TimeSpan TotalOpenTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Health check status
    /// </summary>
    public ServiceHealthStatus HealthStatus { get; set; } = ServiceHealthStatus.Unknown;

    /// <summary>
    /// Last successful health check time
    /// </summary>
    public DateTimeOffset? LastHealthCheck { get; set; }

    /// <summary>
    /// Additional metrics and metadata
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = [];

    private readonly CircuitBreakerConfiguration _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Records a successful operation
    /// </summary>
    public void RecordSuccess(TimeSpan responseTime)
    {
        TotalRequests++;
        SuccessfulRequests++;
        ConsecutiveFailures = 0;

        // Update average response time using exponential moving average

        if (AverageResponseTime == TimeSpan.Zero)
        {
            AverageResponseTime = responseTime;
        }
        else
        {
            var alpha = 0.1; // Smoothing factor
            AverageResponseTime = TimeSpan.FromTicks(
                (long)(alpha * responseTime.Ticks + (1 - alpha) * AverageResponseTime.Ticks));
        }

        HealthStatus = ServiceHealthStatus.Healthy;
        LastHealthCheck = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records a failed operation
    /// </summary>
    public void RecordFailure(Exception exception)
    {
        TotalRequests++;
        FailedRequests++;
        ConsecutiveFailures++;
        LastError = exception?.Message ?? "Unknown error";
        LastErrorTime = DateTimeOffset.UtcNow;
        HealthStatus = ServiceHealthStatus.Unhealthy;
    }

    /// <summary>
    /// Transitions the circuit to a new state
    /// </summary>
    public void TransitionTo(CircuitState newState, TimeSpan? openTimeout = null)
    {
        if (State != newState)
        {
            var previousState = State;
            State = newState;
            LastStateChange = DateTimeOffset.UtcNow;

            if (newState == CircuitState.Open)
            {
                OpenCount++;
                NextRetryTime = openTimeout.HasValue

                    ? DateTimeOffset.UtcNow.Add(openTimeout.Value)
                    : DateTimeOffset.UtcNow.AddMinutes(1); // Default 1 minute
            }
            else if (previousState == CircuitState.Open)
            {
                TotalOpenTime = TotalOpenTime.Add(DateTimeOffset.UtcNow - LastStateChange);
                NextRetryTime = null;
            }
        }
    }

    /// <summary>
    /// Checks if the circuit can attempt a retry (for Half-Open state)
    /// </summary>
    public bool CanAttemptRetry()
    {
        return State == CircuitState.HalfOpen ||

               (State == CircuitState.Open && NextRetryTime.HasValue && DateTimeOffset.UtcNow >= NextRetryTime.Value);
    }

    /// <summary>
    /// Resets the circuit state statistics
    /// </summary>
    public void Reset()
    {
        State = CircuitState.Closed;
        ConsecutiveFailures = 0;
        TotalRequests = 0;
        FailedRequests = 0;
        SuccessfulRequests = 0;
        NextRetryTime = null;
        AverageResponseTime = TimeSpan.Zero;
        LastError = null;
        LastErrorTime = null;
        HealthStatus = ServiceHealthStatus.Unknown;
        LastHealthCheck = null;
        Metrics.Clear();
        LastStateChange = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets a summary of the circuit state
    /// </summary>
    public CircuitStateSummary GetSummary()
    {
        return new CircuitStateSummary
        {
            ServiceName = ServiceName,
            State = State,
            FailureRate = FailureRate,
            SuccessRate = SuccessRate,
            ConsecutiveFailures = ConsecutiveFailures,
            TotalRequests = TotalRequests,
            AverageResponseTime = AverageResponseTime,
            HealthStatus = HealthStatus,
            LastStateChange = LastStateChange,
            OpenCount = OpenCount,
            TotalOpenTime = TotalOpenTime
        };
    }

    /// <summary>
    /// Checks if the circuit can execute requests
    /// </summary>
    public bool CanExecute()
    {
        return State switch
        {
            CircuitState.Closed => true,
            CircuitState.Open => NextRetryTime.HasValue && DateTimeOffset.UtcNow >= NextRetryTime.Value,
            CircuitState.HalfOpen => true,
            _ => false
        };
    }


    /// <summary>
    /// Forces the circuit to a specific state
    /// </summary>
    public void ForceState(CircuitState state)
    {
        TransitionTo(state, _config?.OpenCircuitTimeout);
        _logger?.LogWarning("Service {ServiceName} circuit forced to {State}", ServiceName, state);
    }


    /// <summary>
    /// Performs health check for the service
    /// </summary>
    public void PerformHealthCheck()
    {
        // Perform basic health status evaluation
        if (State == CircuitState.Open && NextRetryTime.HasValue && DateTimeOffset.UtcNow >= NextRetryTime.Value)
        {
            TransitionTo(CircuitState.HalfOpen);
            _logger?.LogInformation("Service {ServiceName} transitioned to HalfOpen for testing", ServiceName);
        }
    }


    /// <summary>
    /// Gets circuit breaker statistics
    /// </summary>
    public CircuitBreakerStatistics GetStatistics()
    {
        return new CircuitBreakerStatistics
        {
            GlobalState = State,
            OverallFailureRate = FailureRate * 100.0,
            TotalRequests = TotalRequests,
            FailedRequests = FailedRequests,
            ConsecutiveFailures = ConsecutiveFailures,
            ServiceStatistics = new Dictionary<string, ServiceStatistics>
            {
                [ServiceName] = new ServiceStatistics
                {
                    ServiceName = ServiceName,
                    State = State,
                    FailureRate = FailureRate * 100.0,
                    TotalRequests = TotalRequests,
                    FailedRequests = FailedRequests,
                    AverageResponseTime = TimeSpan.FromMilliseconds(100), // Default value
                    LastFailure = LastStateChange
                }
            },
            LastStateChange = LastStateChange,
            ActiveServices = 1
        };
    }


    public void Dispose()
        // Clean up any resources if needed



        => Metrics.Clear();

    public override string ToString()
        => $"{ServiceName}: {State}, Failures={ConsecutiveFailures}, " +
        $"Rate={FailureRate:P1}, Health={HealthStatus}";
}


/// <summary>
/// Service health status enumeration
/// </summary>
public enum ServiceHealthStatus
{
    /// <summary>
    /// Health status is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// Service is healthy
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is degraded but operational
    /// </summary>
    Degraded,

    /// <summary>
    /// Service is unhealthy
    /// </summary>
    Unhealthy
}


/// <summary>
/// Summary information about a circuit breaker state
/// </summary>
public class CircuitStateSummary
{
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Current circuit state
    /// </summary>
    public CircuitState State { get; set; }

    /// <summary>
    /// Failure rate (0.0 to 1.0)
    /// </summary>
    public double FailureRate { get; set; }

    /// <summary>
    /// Success rate (0.0 to 1.0)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Total number of requests
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Average response time
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Health status
    /// </summary>
    public ServiceHealthStatus HealthStatus { get; set; }

    /// <summary>
    /// Last state change time
    /// </summary>
    public DateTimeOffset LastStateChange { get; set; }

    /// <summary>
    /// Number of times circuit has been opened
    /// </summary>
    public int OpenCount { get; set; }

    /// <summary>
    /// Total time circuit has been open
    /// </summary>
    public TimeSpan TotalOpenTime { get; set; }

    public override string ToString()
        => $"{ServiceName}: {State}, {FailureRate:P1} failure rate, {TotalRequests} requests";
}

