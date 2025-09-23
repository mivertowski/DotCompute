// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines optimization levels for kernel compilation.
/// These levels correspond to standard compiler optimization flags and provide
/// a balance between compilation time and runtime performance.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization - fastest compilation, no performance optimizations.
    /// Equivalent to compiler flag -O0. Best for debugging.
    /// </summary>
    None = 0,

    /// <summary>
    /// No optimization - fastest compilation, no performance optimizations.
    /// Equivalent to compiler flag -O0. Alias for None.
    /// </summary>
    O0 = 0,

    /// <summary>
    /// Basic optimization - minimal performance improvements with fast compilation.
    /// Equivalent to compiler flag -O1. Enables basic optimizations like dead code elimination.
    /// </summary>
    O1 = 1,

    /// <summary>
    /// Standard optimization - good performance with reasonable compilation time.
    /// Equivalent to compiler flag -O2. Recommended for most production use cases.
    /// </summary>
    O2 = 2,

    /// <summary>
    /// Maximum optimization - best performance, longest compilation time.
    /// Equivalent to compiler flag -O3. Enables aggressive optimizations that may increase code size.
    /// </summary>
    O3 = 3,

    /// <summary>
    /// Size optimization - optimize for smallest code size rather than speed.
    /// Equivalent to compiler flag -Os. Best for memory-constrained environments.
    /// </summary>
    Size = 4,

    /// <summary>
    /// Default optimization level for balanced performance and compilation time.
    /// Typically maps to O2 for most scenarios.
    /// </summary>
    Default = O2,

    /// <summary>
    /// Aggressive optimization alias for maximum performance.
    /// Maps to O3 optimization level.
    /// </summary>
    Aggressive = O3
}
