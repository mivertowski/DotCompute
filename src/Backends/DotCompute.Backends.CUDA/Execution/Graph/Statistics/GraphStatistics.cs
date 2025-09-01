// <copyright file="GraphStatistics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Graph.Statistics;

/// <summary>
/// Statistical data for graph execution and performance.
/// </summary>
public sealed class GraphStatistics
{
    /// <summary>
    /// Gets or sets the total number of nodes in the graph.
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Gets or sets the number of kernel nodes.
    /// </summary>
    public int KernelNodes { get; set; }

    /// <summary>
    /// Gets or sets the number of memory operation nodes.
    /// </summary>
    public int MemoryNodes { get; set; }

    /// <summary>
    /// Gets or sets the graph depth (longest dependency chain).
    /// </summary>
    public int GraphDepth { get; set; }

    /// <summary>
    /// Gets or sets the average parallelism level.
    /// </summary>
    public double AverageParallelism { get; set; }

    /// <summary>
    /// Gets or sets the total execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time in milliseconds.
    /// </summary>
    public double MinExecutionTimeMs { get; set; } = double.MaxValue;

    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public double MaxExecutionTimeMs { get; set; }

    /// <summary>
    /// Updates statistics with a new execution time.
    /// </summary>
    /// <param name="executionTimeMs">The execution time in milliseconds.</param>
    public void UpdateExecutionTime(double executionTimeMs)
    {
        ExecutionCount++;
        
        if (executionTimeMs < MinExecutionTimeMs)
        {
            MinExecutionTimeMs = executionTimeMs;
        }


        if (executionTimeMs > MaxExecutionTimeMs)
        {
            MaxExecutionTimeMs = executionTimeMs;
        }

        // Calculate running average

        AverageExecutionTimeMs = ((AverageExecutionTimeMs * (ExecutionCount - 1)) + executionTimeMs) / ExecutionCount;
    }
}

/// <summary>
/// Performance analysis data for graph operations.
/// </summary>
public sealed class GraphPerformanceAnalysis
{
    /// <summary>
    /// Gets or sets the bottleneck nodes in the graph.
    /// </summary>
    public List<string> BottleneckNodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the optimization opportunities.
    /// </summary>
    public List<string> OptimizationOpportunities { get; set; } = [];

    /// <summary>
    /// Gets or sets the memory efficiency percentage.
    /// </summary>
    public double MemoryEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the compute efficiency percentage.
    /// </summary>
    public double ComputeEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the estimated speedup from optimization.
    /// </summary>
    public double EstimatedSpeedupFactor { get; set; } = 1.0;
}