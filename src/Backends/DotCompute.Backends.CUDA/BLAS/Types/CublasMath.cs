// <copyright file="CublasMath.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.BLAS.Types;

/// <summary>
/// Specifies the math mode for cuBLAS operations.
/// Controls the use of Tensor Cores and precision settings.
/// </summary>
public enum CublasMath
{
    /// <summary>
    /// Default math mode.
    /// Uses standard CUDA cores without Tensor Core acceleration.
    /// Provides IEEE-compliant floating-point arithmetic.
    /// </summary>
    DefaultMath = 0,

    /// <summary>
    /// Tensor Core math mode.
    /// Enables Tensor Core acceleration for supported operations.
    /// May use reduced precision for improved performance.
    /// </summary>
    TensorOpMath = 1,

    /// <summary>
    /// Pedantic math mode.
    /// Enforces strict IEEE compliance and disables optimizations.
    /// Use for debugging or when exact reproducibility is required.
    /// </summary>
    PedanticMath = 2,

    /// <summary>
    /// TF32 Tensor Core math mode.
    /// Uses TensorFloat-32 (TF32) precision on Ampere and newer GPUs.
    /// Provides good balance between performance and precision for deep learning.
    /// </summary>
    TF32TensorOpMath = 3
}