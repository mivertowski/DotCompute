// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Generators;

/// <summary>
/// Supported kernel operations for SIMD execution.
/// </summary>
internal enum KernelOperation
{
    /// <summary>
    /// Element-wise addition operation.
    /// </summary>
    Add,

    /// <summary>
    /// Element-wise multiplication operation.
    /// </summary>
    Multiply,

    /// <summary>
    /// Fused multiply-add operation (a * b + c).
    /// </summary>
    FusedMultiplyAdd,

    /// <summary>
    /// Element-wise subtraction operation.
    /// </summary>
    Subtract,

    /// <summary>
    /// Element-wise division operation.
    /// </summary>
    Divide,

    /// <summary>
    /// Element-wise maximum operation.
    /// </summary>
    Maximum,

    /// <summary>
    /// Element-wise minimum operation.
    /// </summary>
    Minimum,

    /// <summary>
    /// Dot product operation.
    /// </summary>
    DotProduct,

    /// <summary>
    /// L2 norm operation.
    /// </summary>
    L2Norm,

    /// <summary>
    /// Convolution operation.
    /// </summary>
    Convolution,

    /// <summary>
    /// Reduction operation.
    /// </summary>
    Reduction,

    /// <summary>
    /// Gather operation.
    /// </summary>
    Gather,

    /// <summary>
    /// Scatter operation.
    /// </summary>
    Scatter
}