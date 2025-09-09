// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Settings.Enums;

/// <summary>
/// Defines optimization levels for generated code, controlling the trade-offs
/// between performance, code size, and debuggability.
/// </summary>
/// <remarks>
/// The optimization level affects various aspects of code generation including
/// loop unrolling, inlining decisions, SIMD usage, and other performance
/// optimizations. Higher optimization levels may produce faster code but
/// can increase compilation time and reduce debuggability.
/// </remarks>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization applied, prioritizing debuggability and fast compilation.
    /// </summary>
    /// <remarks>
    /// This level generates straightforward, unoptimized code that closely
    /// mirrors the source structure. Ideal for development and debugging
    /// scenarios where code clarity is more important than performance.
    /// </remarks>
    None,

    /// <summary>
    /// Balanced optimization suitable for general-purpose use.
    /// </summary>
    /// <remarks>
    /// This level applies moderate optimizations that provide good performance
    /// improvements while maintaining reasonable code size and compilation time.
    /// This is the recommended setting for most production scenarios.
    /// </remarks>
    Balanced,

    /// <summary>
    /// Aggressive optimization for maximum performance, potentially increasing code size.
    /// </summary>
    /// <remarks>
    /// This level applies extensive optimizations including aggressive inlining,
    /// loop unrolling, and vectorization. May significantly increase code size
    /// and compilation time but provides the best runtime performance.
    /// </remarks>
    Aggressive,


    /// <summary>
    /// Optimization focused on minimizing code size rather than maximizing performance.
    /// </summary>
    /// <remarks>
    /// This level prioritizes compact code generation, making trade-offs that
    /// favor smaller binary size over raw performance. Useful for
    /// resource-constrained environments or when minimizing memory footprint
    /// is critical.
    /// </remarks>
    Size
}