// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Configuration options for Metal health monitor
/// </summary>
public sealed class MetalHealthMonitorOptions
{
    /// <summary>
    /// Gets or sets the health check interval
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the anomaly detection interval
    /// </summary>
    public TimeSpan AnomalyDetectionInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the anomaly detection window
    /// </summary>
    public TimeSpan AnomalyDetectionWindow { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the event retention period
    /// </summary>
    public TimeSpan EventRetentionPeriod { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the maximum number of health events to keep
    /// </summary>
    public int MaxHealthEvents { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the circuit breaker threshold
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the circuit breaker timeout
    /// </summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the anomaly error rate threshold
    /// </summary>
    public double AnomalyErrorRateThreshold { get; set; } = 0.1; // 10%
}
