// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Statistics about the learning effectiveness of the adaptive system.
/// </summary>
public class LearningStatistics
{
    /// <summary>
    /// Gets or sets the total performance samples.
    /// </summary>
    /// <value>The total performance samples.</value>
    public int TotalPerformanceSamples { get; set; }
    /// <summary>
    /// Gets or sets the average samples per workload.
    /// </summary>
    /// <value>The average samples per workload.</value>
    public int AverageSamplesPerWorkload { get; set; }
    /// <summary>
    /// Gets or sets the workloads with sufficient history.
    /// </summary>
    /// <value>The workloads with sufficient history.</value>
    public int WorkloadsWithSufficientHistory { get; set; }
    /// <summary>
    /// Gets or sets the learning effectiveness.
    /// </summary>
    /// <value>The learning effectiveness.</value>
    public float LearningEffectiveness { get; set; }
}