// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Selection.Enums;

/// <summary>
/// Matrix multiplication algorithm strategies.
/// Each strategy is optimized for different problem sizes and hardware characteristics.
/// </summary>
public enum MatrixMultiplyStrategy
{
    /// <summary>
    /// Optimized micro-kernels for very small matrices (typically ≤ 4x4).
    /// </summary>
    Micro,

    /// <summary>
    /// Standard ijk algorithm for general-purpose use.
    /// </summary>
    Standard,

    /// <summary>
    /// SIMD-optimized algorithm using vectorized instructions.
    /// </summary>
    SIMD,

    /// <summary>
    /// Cache-blocked algorithm for improved memory locality.
    /// </summary>
    Blocked,

    /// <summary>
    /// Strassen's algorithm for large square matrices (power of 2).
    /// </summary>
    Strassen,

    /// <summary>
    /// Cache-oblivious algorithm for very large matrices.
    /// </summary>
    CacheOblivious,

    /// <summary>
    /// Parallel blocked algorithm using multiple threads.
    /// </summary>
    ParallelBlocked
}
