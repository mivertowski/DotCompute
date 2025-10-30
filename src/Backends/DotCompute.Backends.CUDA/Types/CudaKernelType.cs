// <copyright file="CudaKernelType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Types;

/// <summary>
/// Categorizes CUDA kernels by their computational pattern.
/// Used for optimization decisions and kernel fusion strategies.
/// </summary>
public enum CudaKernelType
{
    /// <summary>
    /// Element-wise operation kernel.
    /// Each thread operates on independent data elements.
    /// Examples: vector addition, scalar multiplication, activation functions.
    /// High potential for fusion and memory bandwidth optimization.
    /// </summary>
    ElementWise,

    /// <summary>
    /// Matrix multiplication kernel.
    /// Performs matrix-matrix or matrix-vector operations.
    /// Benefits from Tensor Core acceleration on supported hardware.
    /// Critical for deep learning and linear algebra workloads.
    /// </summary>
    MatrixMultiply,

    /// <summary>
    /// Reduction operation kernel.
    /// Combines multiple values into a single result.
    /// Examples: sum, max, min, dot product.
    /// Requires careful synchronization and often uses shared memory.
    /// </summary>
    Reduction,

    /// <summary>
    /// Matrix transpose kernel.
    /// Rearranges matrix data layout in memory.
    /// Memory bandwidth bound, benefits from coalesced access patterns.
    /// Often combined with other operations for efficiency.
    /// </summary>
    Transpose,

    /// <summary>
    /// Custom kernel implementation.
    /// User-defined kernel with arbitrary computation pattern.
    /// Optimization strategies must be determined case-by-case.
    /// </summary>
    Custom,

    /// <summary>
    /// Fused kernel combining multiple operations.
    /// Result of kernel fusion optimization.
    /// Reduces memory traffic by combining multiple operations in a single pass.
    /// </summary>
    Fused
}
