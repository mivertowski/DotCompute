// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Utils.Analysis;

/// <summary>
/// Configuration options for loop optimization.
/// </summary>
public sealed class LoopOptimizationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether loop unrolling is enabled.
    /// </summary>
    public bool EnableUnrolling { get; set; }

    /// <summary>
    /// Gets or sets the loop unroll factor.
    /// </summary>
    public int UnrollFactor { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether prefetching is enabled.
    /// </summary>
    public bool EnablePrefetching { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether vectorization is enabled.
    /// </summary>
    public bool EnableVectorization { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to parallelize if possible.
    /// </summary>
    public bool ParallelizeIfPossible { get; set; }
}
