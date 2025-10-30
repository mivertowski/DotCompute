// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Types;

/// <summary>
/// Enumeration of supported kernel types for the CPU backend, used to categorize
/// and optimize kernel execution based on computational patterns.
/// </summary>
/// <remarks>
/// This enumeration helps the CPU backend select the most appropriate optimized
/// kernel implementation based on the computational characteristics of the operation.
/// Each type corresponds to a specific optimization strategy.
/// </remarks>
internal enum KernelType
{
    /// <summary>
    /// Generic kernel type for unknown or unsupported patterns.
    /// Uses fallback implementation with basic optimization.
    /// </summary>
    Generic,

    /// <summary>
    /// Vector addition operations (element-wise addition of two vectors).
    /// Optimized with SIMD instructions for parallel processing.
    /// </summary>
    VectorAdd,

    /// <summary>
    /// Vector multiplication operations (element-wise multiplication of two vectors).
    /// Optimized with SIMD instructions for parallel processing.
    /// </summary>
    VectorMultiply,

    /// <summary>
    /// Vector scaling operations (multiplication of vector by scalar).
    /// Optimized with SIMD instructions and vectorized scalar broadcasting.
    /// </summary>
    VectorScale,

    /// <summary>
    /// Matrix multiplication operations using cache-optimized algorithms.
    /// Optimized for memory access patterns and parallel execution.
    /// </summary>
    MatrixMultiply,

    /// <summary>
    /// Reduction operations (sum, max, min, etc.) with logarithmic scaling.
    /// Optimized with parallel partitioning and tree-based aggregation.
    /// </summary>
    Reduction,

    /// <summary>
    /// Memory-intensive operations optimized for bandwidth utilization.
    /// Focuses on efficient memory access patterns and data movement.
    /// </summary>
    MemoryIntensive,

    /// <summary>
    /// Compute-intensive operations with transcendental functions.
    /// Optimized for floating-point computation and mathematical operations.
    /// </summary>
    ComputeIntensive
}
