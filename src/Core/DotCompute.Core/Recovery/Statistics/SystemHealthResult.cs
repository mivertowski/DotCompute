// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Statistics;

/// <summary>
/// Represents the result of a comprehensive system health check operation,
/// providing aggregated health status across all monitored components.
/// </summary>
/// <remarks>
/// A system health check evaluates the operational status of all critical components
/// including hardware devices, software services, and recovery mechanisms.
/// The result provides both individual component health and an overall system assessment
/// that can be used for monitoring, alerting, and automated recovery decisions.
/// </remarks>
public class SystemHealthResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the overall system is considered healthy.
    /// </summary>
    /// <value>
    /// true if the system is healthy and operating within acceptable parameters;
    /// otherwise, false.
    /// </value>
    public bool IsHealthy { get; set; }


    /// <summary>
    /// Gets or sets the overall health score as a percentage (0.0 to 1.0).
    /// This is calculated from all component health scores.
    /// </summary>
    /// <value>A value between 0.0 (completely unhealthy) and 1.0 (completely healthy).</value>
    public double OverallHealth { get; set; }


    /// <summary>
    /// Gets or sets the list of individual component health results.
    /// </summary>
    /// <value>A collection of health results for each monitored component.</value>
    public IList<ComponentHealthResult> ComponentResults { get; } = [];


    /// <summary>
    /// Gets or sets the duration of the health check operation.
    /// </summary>
    /// <value>The time taken to complete the health check.</value>
    public TimeSpan Duration { get; set; }


    /// <summary>
    /// Gets or sets the timestamp when the health check was performed.
    /// </summary>
    /// <value>The UTC timestamp of the health check operation.</value>
    public DateTimeOffset Timestamp { get; set; }


    /// <summary>
    /// Gets or sets the error message if the health check failed or encountered issues.
    /// </summary>
    /// <value>The error description, or null if no errors occurred.</value>
    public string? Error { get; set; }

    /// <summary>
    /// Returns a string representation of the system health result.
    /// </summary>
    /// <returns>A formatted string indicating health status and component count.</returns>
    public override string ToString()
        => IsHealthy
            ? $"HEALTHY ({OverallHealth:P1}) - {ComponentResults.Count} components checked"
            : $"UNHEALTHY ({OverallHealth:P1}) - Error: {Error}";
}