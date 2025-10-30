// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Performance;

/// <summary>
/// Represents statistics for an async resource pool, including resource utilization metrics.
/// </summary>
public readonly record struct PoolStatistics
{
    /// <summary>
    /// Gets the number of available resources in the pool.
    /// </summary>
    /// <value>The available resources.</value>
    public int AvailableResources { get; init; }

    /// <summary>
    /// Gets the total number of resources in the pool.
    /// </summary>
    /// <value>The total resources.</value>
    public int TotalResources { get; init; }

    /// <summary>
    /// Gets the number of requests waiting for resources.
    /// </summary>
    /// <value>The waiting requests.</value>
    public int WaitingRequests { get; init; }

    /// <summary>
    /// Gets the number of resources currently in use.
    /// </summary>
    /// <value>The used resources.</value>
    public int UsedResources => TotalResources - AvailableResources;

    /// <summary>
    /// Gets the resource utilization rate as a percentage (0.0 to 1.0).
    /// </summary>
    /// <value>The utilization rate.</value>
    public double UtilizationRate => TotalResources > 0 ? (double)UsedResources / TotalResources : 0.0;
}
