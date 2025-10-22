// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Selection.Enums;

/// <summary>
/// BLAS (Basic Linear Algebra Subprograms) algorithm strategies.
/// Optimizations range from standard implementations to hardware-accelerated variants.
/// </summary>
public enum BLASStrategy
{
    /// <summary>
    /// Standard scalar implementation.
    /// </summary>
    Standard,

    /// <summary>
    /// Vector-optimized implementation using compiler auto-vectorization.
    /// </summary>
    Vectorized,

    /// <summary>
    /// SIMD-vectorized implementation using explicit SIMD instructions.
    /// </summary>
    SimdVectorized,

    /// <summary>
    /// Cache-blocked implementation for improved memory performance.
    /// </summary>
    Blocked,

    /// <summary>
    /// Parallel blocked implementation using multiple threads.
    /// </summary>
    ParallelBlocked
}
