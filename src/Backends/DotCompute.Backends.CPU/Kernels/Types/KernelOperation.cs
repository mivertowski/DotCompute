// <copyright file="KernelOperation.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CPU.Kernels.Types;

/// <summary>
/// Defines the types of kernel operations supported by the SIMD code generator.
/// These operations represent fundamental computational primitives that can be vectorized.
/// </summary>
internal enum KernelOperation
{
    /// <summary>
    /// Element-wise addition operation.
    /// Performs a[i] + b[i] for each element.
    /// </summary>
    Add,

    /// <summary>
    /// Element-wise multiplication operation.
    /// Performs a[i] * b[i] for each element.
    /// </summary>
    Multiply,

    /// <summary>
    /// Fused multiply-add operation.
    /// Performs a[i] * b[i] + c[i] in a single instruction for better performance.
    /// </summary>
    FusedMultiplyAdd,

    /// <summary>
    /// Element-wise subtraction operation.
    /// Performs a[i] - b[i] for each element.
    /// </summary>
    Subtract,

    /// <summary>
    /// Element-wise division operation.
    /// Performs a[i] / b[i] for each element.
    /// </summary>
    Divide,

    /// <summary>
    /// Element-wise maximum operation.
    /// Returns max(a[i], b[i]) for each element.
    /// </summary>
    Maximum,

    /// <summary>
    /// Element-wise minimum operation.
    /// Returns min(a[i], b[i]) for each element.
    /// </summary>
    Minimum,

    /// <summary>
    /// Dot product operation.
    /// Computes the sum of products: Σ(a[i] * b[i]).
    /// </summary>
    DotProduct,

    /// <summary>
    /// L2 norm calculation.
    /// Computes the Euclidean norm: √(Σ(a[i]²)).
    /// </summary>
    L2Norm,

    /// <summary>
    /// Convolution operation.
    /// Applies a filter kernel to the input data.
    /// </summary>
    Convolution,

    /// <summary>
    /// Reduction operation.
    /// Reduces array elements to a single value using an associative operation.
    /// </summary>
    Reduction,

    /// <summary>
    /// Gather operation.
    /// Collects elements from non-contiguous memory locations.
    /// </summary>
    Gather,

    /// <summary>
    /// Scatter operation.
    /// Distributes elements to non-contiguous memory locations.
    /// </summary>
    Scatter
}