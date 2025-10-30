// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// Performance statistics for SIMD executor operations
/// </summary>
public sealed class ExecutorStatistics
{
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public long TotalExecutions { get; init; }
    /// <summary>
    /// Gets or sets the total elements.
    /// </summary>
    /// <value>The total elements.</value>
    public long TotalElements { get; init; }
    /// <summary>
    /// Gets or sets the vectorized elements.
    /// </summary>
    /// <value>The vectorized elements.</value>
    public long VectorizedElements { get; init; }
    /// <summary>
    /// Gets or sets the scalar elements.
    /// </summary>
    /// <value>The scalar elements.</value>
    public long ScalarElements { get; init; }
    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    /// <value>The average execution time.</value>
    public TimeSpan AverageExecutionTime { get; init; }
    /// <summary>
    /// Gets or sets the vectorization ratio.
    /// </summary>
    /// <value>The vectorization ratio.</value>
    public double VectorizationRatio { get; init; }
    /// <summary>
    /// Gets or sets the performance gain.
    /// </summary>
    /// <value>The performance gain.</value>
    public double PerformanceGain { get; init; }
}
