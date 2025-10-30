// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Models.Vectorization.Enums;

/// <summary>
/// Represents the different levels of benefit that can be achieved from vectorization optimization.
/// This enumeration helps classify the potential performance improvements when applying SIMD operations.
/// </summary>
/// <remarks>
/// The benefit levels are based on empirical performance measurements and theoretical speedup calculations.
/// These values serve as guidelines for optimization decisions in the code generation pipeline.
/// </remarks>
public enum VectorizationBenefit
{
    /// <summary>
    /// No benefit from vectorization.
    /// Indicates that the code pattern is not suitable for SIMD optimization
    /// or that vectorization would not provide any measurable performance improvement.
    /// </summary>
    None,

    /// <summary>
    /// Low benefit from vectorization (typically 1.5x-2x speedup).
    /// Suitable for simple arithmetic operations or cases with data dependencies
    /// that limit the effectiveness of vectorization.
    /// </summary>
    Low,

    /// <summary>
    /// Medium benefit from vectorization (typically 2x-4x speedup).
    /// Appropriate for well-structured loops with moderate computational complexity
    /// and minimal data dependencies.
    /// </summary>
    Medium,

    /// <summary>
    /// High benefit from vectorization (typically 4x+ speedup).
    /// Reserved for highly optimizable patterns such as dot products, matrix operations,
    /// and reduction operations that can fully utilize SIMD instruction sets.
    /// </summary>
    High
}
