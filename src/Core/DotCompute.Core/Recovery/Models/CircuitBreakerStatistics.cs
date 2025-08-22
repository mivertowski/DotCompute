// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Represents comprehensive circuit breaker statistics
/// </summary>
public class CircuitBreakerStatistics
{
    /// <summary>
    /// Gets or sets the global circuit state
    /// </summary>
    public CircuitState GlobalState { get; set; }

    /// <summary>
    /// Gets or sets the overall failure rate percentage
    /// </summary>
    public double OverallFailureRate { get; set; }

    /// <summary>
    /// Gets or sets the total number of requests
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of failed requests
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Gets or sets statistics for individual services
    /// </summary>
    public Dictionary<string, ServiceStatistics> ServiceStatistics { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the last state change
    /// </summary>
    public DateTimeOffset LastStateChange { get; set; }

    /// <summary>
    /// Gets or sets the number of active services being monitored
    /// </summary>
    public int ActiveServices { get; set; }
}

/// <summary>
/// Represents statistics for a specific service
/// </summary>
public class ServiceStatistics
{
    /// <summary>
    /// Gets or sets the service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current circuit state for this service
    /// </summary>
    public CircuitState State { get; set; }

    /// <summary>
    /// Gets or sets the total number of requests for this service
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of failed requests for this service
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// Gets or sets the failure rate percentage for this service
    /// </summary>
    public double FailureRate { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last failure for this service
    /// </summary>
    public DateTimeOffset LastFailure { get; set; }
}