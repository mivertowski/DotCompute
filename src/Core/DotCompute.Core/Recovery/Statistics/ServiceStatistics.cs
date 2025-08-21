// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Statistics;

/// <summary>
/// Detailed statistics for an individual service monitored by the circuit breaker system.
/// Provides comprehensive metrics for service health, performance, and failure patterns.
/// </summary>
/// <remarks>
/// Service statistics enable fine-grained monitoring of individual service health
/// within the circuit breaker pattern. These metrics help identify:
/// - Service-specific failure patterns and rates
/// - Performance degradation trends
/// - Circuit breaker state transitions for the service
/// - Request volume and timing patterns
/// 
/// This data is essential for service-level alerting and targeted recovery actions.
/// </remarks>
public class ServiceStatistics
{
    /// <summary>
    /// Gets or sets the name or identifier of the monitored service.
    /// </summary>
    /// <value>The unique service identifier.</value>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the current circuit breaker state for this specific service.
    /// </summary>
    /// <value>The service's circuit breaker state (Closed, Open, or HalfOpen).</value>
    public CircuitState State { get; set; }
    
    /// <summary>
    /// Gets or sets the failure rate for this service as a percentage (0.0 to 1.0).
    /// </summary>
    /// <value>The ratio of failed requests to total requests for this service.</value>
    public double FailureRate { get; set; }
    
    /// <summary>
    /// Gets or sets the total number of requests processed by this service.
    /// </summary>
    /// <value>The cumulative count of all requests to this service.</value>
    public long TotalRequests { get; set; }
    
    /// <summary>
    /// Gets or sets the number of failed requests for this service.
    /// </summary>
    /// <value>The cumulative count of failed requests to this service.</value>
    public long FailedRequests { get; set; }
    
    /// <summary>
    /// Gets or sets the average response time for successful requests to this service.
    /// </summary>
    /// <value>The mean duration of successful service calls.</value>
    public TimeSpan AverageResponseTime { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp of the most recent failure for this service.
    /// </summary>
    /// <value>The UTC timestamp of the last failed request, or default if no failures.</value>
    public DateTimeOffset LastFailure { get; set; }

    /// <summary>
    /// Returns a string representation of the service statistics.
    /// </summary>
    /// <returns>A formatted string containing service name, state, and key metrics.</returns>
    public override string ToString()
        => $"{ServiceName}: {State}, {FailureRate:P1} failure rate, {TotalRequests} requests";
}