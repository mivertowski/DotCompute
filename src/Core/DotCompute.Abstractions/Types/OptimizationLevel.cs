// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines optimization levels for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization - fastest compilation (equivalent to -O0).
    /// </summary>
    None = 0,
    
    /// <summary>
    /// No optimization - fastest compilation (equivalent to -O0).
    /// </summary>
    O0 = 0,

    /// <summary>
    /// Minimal optimization - fastest compilation with basic optimizations (equivalent to -O1).
    /// </summary>
    Minimal = 1,
    
    /// <summary>
    /// Basic optimization - minimal performance improvements (equivalent to -O1).
    /// </summary>
    O1 = 1,

    /// <summary>
    /// Default optimization - balanced compilation time and performance (equivalent to -O2).
    /// </summary>
    Default = 2,
    
    /// <summary>
    /// Standard optimization - good performance with reasonable compilation time (equivalent to -O2).
    /// </summary>
    O2 = 2,

    /// <summary>
    /// Moderate optimization - good performance with reasonable compilation time.
    /// </summary>
    Moderate = 3,

    /// <summary>
    /// Aggressive optimization - maximum performance, slower compilation (equivalent to -O3).
    /// </summary>
    Aggressive = 4,
    
    /// <summary>
    /// Maximum optimization - best performance, longest compilation time (equivalent to -O3).
    /// </summary>
    O3 = 4,

    /// <summary>
    /// Size optimization - optimize for smallest code size.
    /// </summary>
    Size = 5,

    /// <summary>
    /// Maximum optimization - best performance, longest compilation time.
    /// </summary>
    Maximum = 6,

    /// <summary>
    /// Full optimization - comprehensive optimization with all techniques.
    /// </summary>
    Full = 7,

    /// <summary>
    /// Balanced optimization - optimal balance between performance and compilation time.
    /// </summary>
    Balanced = 8
}
