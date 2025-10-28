#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Analysis;

namespace DotCompute.Algorithms.Types.Abstractions;


/// <summary>
/// Performance profile information for an algorithm plugin.
/// </summary>
public class AlgorithmPerformanceProfile
{
    /// <summary>
    /// Gets or sets the algorithm identifier.
    /// </summary>
    public string AlgorithmId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the estimated execution time in milliseconds.
    /// </summary>
    public double EstimatedExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory requirement in megabytes.
    /// </summary>
    public double MemoryRequirementMB { get; set; }

    /// <summary>
    /// Gets or sets the computational complexity level.
    /// </summary>
    public ComputeComplexity ComputeComplexity { get; set; } = ComputeComplexity.Medium;

    /// <summary>
    /// Gets or sets whether the algorithm is parallelizable.
    /// </summary>
    public bool IsParallelizable { get; set; } = true;

    /// <summary>
    /// Gets or sets the optimal batch size for processing.
    /// </summary>
    public int OptimalBatchSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets the algorithmic complexity notation (e.g., "O(n)", "O(n^2)").
    /// </summary>
    public string? Complexity { get; init; }

    /// <summary>
    /// Gets or sets the optimal number of parallel threads for this algorithm.
    /// </summary>
    public int OptimalParallelism { get; init; }

    /// <summary>
    /// Gets or sets whether the algorithm is memory-bound.
    /// </summary>
    public bool IsMemoryBound { get; init; }

    /// <summary>
    /// Gets or sets whether the algorithm is compute-bound.
    /// </summary>
    public bool IsComputeBound { get; init; }

    /// <summary>
    /// Gets or sets the estimated number of floating-point operations.
    /// </summary>
    public long EstimatedFlops { get; init; }

    /// <summary>
    /// Gets or sets additional metadata for the performance profile.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Gets or sets additional performance metrics.
    /// </summary>
    public Dictionary<string, object> AdditionalMetrics { get; } = [];

    /// <summary>
    /// Gets or sets the benchmark scores for different input sizes.
    /// </summary>
    public Dictionary<int, double> BenchmarkScores { get; } = [];

    /// <summary>
    /// Gets the estimated execution time for a given input size.
    /// </summary>
    /// <param name="inputSize">The size of the input data.</param>
    /// <returns>Estimated execution time in milliseconds.</returns>
    public double GetEstimatedTime(int inputSize)
    {
        if (BenchmarkScores.Count == 0)
        {
            return EstimatedExecutionTimeMs;
        }

        // Simple linear interpolation for now
        var closestSize = BenchmarkScores.Keys.OrderBy(k => Math.Abs(k - inputSize)).First();
        var factor = (double)inputSize / closestSize;

        return ComputeComplexity switch
        {
            ComputeComplexity.Low => BenchmarkScores[closestSize] * Math.Log(factor + 1),
            ComputeComplexity.Medium => BenchmarkScores[closestSize] * factor,
            ComputeComplexity.High => BenchmarkScores[closestSize] * factor * factor,
            ComputeComplexity.VeryHigh => BenchmarkScores[closestSize] * Math.Pow(factor, 3),
            _ => BenchmarkScores[closestSize] * factor
        };
    }

    /// <summary>
    /// Gets the estimated memory usage for a given input size.
    /// </summary>
    /// <param name="inputSize">The size of the input data.</param>
    /// <returns>Estimated memory usage in megabytes.</returns>
    public double GetEstimatedMemory(int inputSize)
    {
        // Assume linear memory scaling by default
        var baseSizeAssumption = 1000; // Assume base measurement is for 1000 elements
        return MemoryRequirementMB * ((double)inputSize / baseSizeAssumption);
    }
}
