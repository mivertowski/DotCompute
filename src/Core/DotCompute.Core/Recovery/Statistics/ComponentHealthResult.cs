// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Statistics;

/// <summary>
/// Represents the health status and diagnostic information for a specific system component.
/// This includes both the health assessment and detailed metrics about component performance.
/// </summary>
/// <remarks>
/// Component health results are generated during system health checks and provide
/// detailed information about individual subsystems such as GPU devices, memory managers,
/// compilation services, and plugin systems. Each result includes both a binary health
/// status and quantitative metrics for performance monitoring.
/// </remarks>
public class ComponentHealthResult
{
    /// <summary>
    /// Gets or sets the name of the component being monitored.
    /// </summary>
    /// <value>The component identifier or descriptive name.</value>
    public string Component { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets a value indicating whether this component is considered healthy.
    /// </summary>
    /// <value>
    /// true if the component is operating within acceptable parameters;
    /// otherwise, false.
    /// </value>
    public bool IsHealthy { get; set; }


    /// <summary>
    /// Gets or sets the component's health score as a percentage (0.0 to 1.0).
    /// </summary>
    /// <value>A value between 0.0 (completely unhealthy) and 1.0 (completely healthy).</value>
    public double Health { get; set; }


    /// <summary>
    /// Gets or sets a descriptive message about the component's health status.
    /// </summary>
    /// <value>A human-readable description of the component's current state.</value>
    public string Message { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets the overall health score including sub-components.
    /// This may differ from <see cref="Health"/> when the component has sub-systems.
    /// </summary>
    /// <value>The aggregated health score including all sub-components.</value>
    public double OverallHealth { get; set; }


    /// <summary>
    /// Gets or sets the number of healthy plugins or sub-components.
    /// </summary>
    /// <value>The count of operational sub-components.</value>
    public int HealthyPlugins { get; set; }


    /// <summary>
    /// Gets or sets the total number of plugins or sub-components.
    /// </summary>
    /// <value>The total count of monitored sub-components.</value>
    public int TotalPlugins { get; set; }


    /// <summary>
    /// Gets or sets the response time for the health check operation.
    /// </summary>
    /// <value>The time taken to assess this component's health.</value>
    public TimeSpan ResponseTime { get; set; }


    /// <summary>
    /// Gets or sets the timestamp of the last successful health check.
    /// </summary>
    /// <value>The UTC timestamp of the most recent health assessment.</value>
    public DateTimeOffset LastCheck { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns a string representation of the component health result.
    /// </summary>
    /// <returns>A formatted string containing component name, status, and health score.</returns>
    public override string ToString()
        => $"{Component}: {(IsHealthy ? "HEALTHY" : "UNHEALTHY")} ({Health:P1}) - {Message}";
}
