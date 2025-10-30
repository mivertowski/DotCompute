// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Analysis;

/// <summary>
/// Computational complexity levels for algorithms and operations.
/// This is the canonical complexity enumeration used across DotCompute,
/// including algorithm plugins and backend-specific optimizations.
/// </summary>
public enum ComputeComplexity
{
    /// <summary>
    /// Low computational complexity - simple operations like element-wise addition.
    /// Typically O(n) with minimal branching and cache-friendly access patterns.
    /// </summary>
    Low,

    /// <summary>
    /// Medium computational complexity - moderate operations like basic linear algebra.
    /// Typically O(n log n) to O(n²) with some branching and decent cache locality.
    /// </summary>
    Medium,

    /// <summary>
    /// High computational complexity - complex operations like matrix multiplication.
    /// Typically O(n²) to O(n³) with significant computation per memory access.
    /// </summary>
    High,

    /// <summary>
    /// Very high computational complexity - intensive operations like FFT or convolution.
    /// Typically O(n³) or higher with complex computation patterns.
    /// </summary>
    VeryHigh
}
