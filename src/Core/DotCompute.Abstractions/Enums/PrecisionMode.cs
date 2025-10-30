// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Abstractions.Enums;

/// <summary>
/// Defines precision modes for kernel computations.
/// </summary>
/// <remarks>
/// Precision mode affects both accuracy and performance of compute operations.
/// Lower precision (Half) offers better performance but reduced accuracy,
/// while higher precision (Double) provides maximum accuracy at the cost of performance.
/// </remarks>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Single and Double are industry-standard terms for floating-point precision modes")]
public enum PrecisionMode
{
    /// <summary>
    /// Single precision floating point (32-bit, float/float32).
    /// </summary>
    /// <remarks>
    /// Provides good balance between performance and accuracy.
    /// Standard choice for most GPU compute workloads.
    /// </remarks>
    Single,

    /// <summary>
    /// Double precision floating point (64-bit, double/float64).
    /// </summary>
    /// <remarks>
    /// Highest accuracy but may have reduced performance on some GPUs.
    /// Required for scientific computing and high-precision calculations.
    /// </remarks>
    Double,

    /// <summary>
    /// Half precision floating point (16-bit, half/float16).
    /// </summary>
    /// <remarks>
    /// Fastest performance on modern GPUs with tensor cores.
    /// Suitable for machine learning inference and graphics workloads.
    /// Limited range and precision may not be suitable for all applications.
    /// </remarks>
    Half,

    /// <summary>
    /// Mixed precision mode using different precisions for different operations.
    /// </summary>
    /// <remarks>
    /// Optimizes performance by using lower precision for intermediate calculations
    /// while maintaining higher precision for critical operations and final results.
    /// Common in deep learning training workflows.
    /// </remarks>
    Mixed
}
