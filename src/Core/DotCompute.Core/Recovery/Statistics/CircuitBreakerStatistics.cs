// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Statistics;

/// <summary>
/// Comprehensive statistics for circuit breaker operations across all monitored services.
/// Provides global and per-service metrics for failure rates, states, and performance.
/// </summary>
/// <remarks>
/// Circuit breaker statistics are critical for understanding system resilience and
/// failure patterns. They help identify:
/// - Services experiencing high failure rates
/// - Overall system stability trends
/// - Circuit breaker state transitions and timing
/// - Request volume and failure distribution
/// 
/// These metrics are used for alerting, capacity planning, and system health monitoring.
/// </remarks>
public class CircuitBreakerStatistics
{
    /// <summary>
    /// Gets or sets the global circuit breaker state aggregated across all services.
    /// </summary>
    /// <value>The overall circuit breaker state for the system.</value>
    public CircuitState GlobalState { get; set; }


    /// <summary>
    /// Gets or sets the overall failure rate across all monitored services as a percentage (0.0 to 1.0).
    /// </summary>
    /// <value>The ratio of failed requests to total requests across all services.</value>
    public double OverallFailureRate { get; set; }


    /// <summary>
    /// Gets or sets the total number of requests processed across all services.
    /// </summary>
    /// <value>The cumulative count of all service requests.</value>
    public long TotalRequests { get; set; }


    /// <summary>
    /// Gets or sets the total number of failed requests across all services.
    /// </summary>
    /// <value>The cumulative count of all failed service requests.</value>
    public long FailedRequests { get; set; }


    /// <summary>
    /// Gets or sets the current number of consecutive failures across the system.
    /// This metric is used to trigger global circuit breaker state changes.
    /// </summary>
    /// <value>The count of consecutive failures without a successful request.</value>
    public int ConsecutiveFailures { get; set; }


    /// <summary>
    /// Gets or sets the detailed statistics for individual services.
    /// Maps service names to their specific performance and health metrics.
    /// </summary>
    /// <value>A dictionary of service names and their corresponding statistics.</value>
    public Dictionary<string, ServiceStatistics> ServiceStatistics { get; set; } = [];


    /// <summary>
    /// Gets or sets the timestamp of the last circuit breaker state change.
    /// </summary>
    /// <value>The UTC timestamp when the global state was last modified.</value>
    public DateTimeOffset LastStateChange { get; set; }


    /// <summary>
    /// Gets or sets the number of services currently being monitored by circuit breakers.
    /// </summary>
    /// <value>The count of active services under circuit breaker protection.</value>
    public int ActiveServices { get; set; }

    /// <summary>
    /// Returns a string representation of the circuit breaker statistics.
    /// </summary>
    /// <returns>A formatted string containing key statistics and service counts.</returns>
    public override string ToString()
        => $"State={GlobalState}, FailureRate={OverallFailureRate:P1} ({FailedRequests}/{TotalRequests}), " +
        $"Services={ActiveServices}";
}