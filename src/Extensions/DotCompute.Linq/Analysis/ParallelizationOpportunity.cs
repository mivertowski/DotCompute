// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Represents a parallelization opportunity identified during expression analysis.
/// </summary>
public class ParallelizationOpportunity
{
    /// <summary>
    /// Gets or sets whether this operation is suitable for vectorization.
    /// </summary>
    public bool VectorizationSuitable { get; set; }

    /// <summary>
    /// Gets or sets whether this operation supports parallel execution.
    /// </summary>
    public bool SupportsParallelExecution { get; set; }

    /// <summary>
    /// Gets or sets the recommended level of parallelism.
    /// </summary>
    public int RecommendedParallelism { get; set; }

    /// <summary>
    /// Gets or sets data dependencies that might limit parallelization.
    /// </summary>
    public List<string> DataDependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the estimated speedup from parallelization.
    /// </summary>
    public double EstimatedSpeedup { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets whether the operation requires synchronization.
    /// </summary>
    public bool RequiresSynchronization { get; set; }

    /// <summary>
    /// Gets or sets the optimal thread count for this operation.
    /// </summary>
    public int OptimalThreadCount { get; set; }

    /// <summary>
    /// Gets or sets parallelization constraints or limitations.
    /// </summary>
    public List<string> Constraints { get; set; } = [];

    /// <summary>
    /// Gets or sets the parallel execution strategy to use.
    /// </summary>
    public string ParallelStrategy { get; set; } = "None";

    /// <summary>
    /// Gets or sets the estimated memory requirements for this parallelization opportunity.
    /// </summary>
    public long MemoryRequirements { get; set; }
}