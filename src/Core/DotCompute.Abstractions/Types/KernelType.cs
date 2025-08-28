// <copyright file="KernelType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Enumeration of kernel types for cross-platform kernel categorization and optimization.
/// </summary>
public enum KernelType
{
    /// <summary>
    /// Generic kernel type for unknown or unsupported patterns.
    /// Uses fallback implementation with basic optimization.
    /// </summary>
    Generic,

    /// <summary>
    /// Element-wise operation kernel.
    /// Each thread operates on independent data elements.
    /// Examples: vector addition, scalar multiplication, activation functions.
    /// </summary>
    ElementWise,

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
    /// Benefits from Tensor Core acceleration on supported hardware.
    /// </summary>
    MatrixMultiply,

    /// <summary>
    /// Reduction operations (sum, max, min, etc.) with logarithmic scaling.
    /// Combines multiple values into a single result.
    /// </summary>
    Reduction,

    /// <summary>
    /// Matrix transpose kernel.
    /// Rearranges matrix data layout in memory.
    /// </summary>
    Transpose,

    /// <summary>
    /// Memory-intensive operations optimized for bandwidth utilization.
    /// Focuses on efficient memory access patterns and data movement.
    /// </summary>
    MemoryIntensive,

    /// <summary>
    /// Compute-intensive operations with transcendental functions.
    /// Optimized for floating-point computation and mathematical operations.
    /// </summary>
    ComputeIntensive,

    /// <summary>
    /// Custom kernel implementation.
    /// User-defined kernel with arbitrary computation pattern.
    /// </summary>
    Custom,

    /// <summary>
    /// Fused kernel combining multiple operations.
    /// Result of kernel fusion optimization.
    /// </summary>
    Fused
}

/// <summary>
/// Represents an optimized kernel implementation strategy.
/// </summary>
public static class Optimized
{
    /// <summary>
    /// CPU-optimized kernel using SIMD instructions.
    /// </summary>
    public static readonly string Cpu = "CPU_SIMD_Optimized";

    /// <summary>
    /// GPU-optimized kernel for CUDA devices.
    /// </summary>
    public static readonly string Gpu = "GPU_CUDA_Optimized";

    /// <summary>
    /// Memory-optimized kernel for efficient bandwidth usage.
    /// </summary>
    public static readonly string Memory = "Memory_Bandwidth_Optimized";

    /// <summary>
    /// Compute-optimized kernel for mathematical operations.
    /// </summary>
    public static readonly string Compute = "Compute_Intensive_Optimized";

    /// <summary>
    /// Cache-optimized kernel for improved locality.
    /// </summary>
    public static readonly string Cache = "Cache_Locality_Optimized";

    /// <summary>
    /// Vectorized kernel using platform-specific instructions.
    /// </summary>
    public static readonly string Vectorized = "Vectorized_SIMD_Optimized";
}