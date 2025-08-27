// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines optimization levels for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization - fastest compilation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Minimal optimization - fastest compilation with basic optimizations.
    /// </summary>
    Minimal = 1,

    /// <summary>
    /// Default optimization - balanced compilation time and performance.
    /// </summary>
    Default = 2,

    /// <summary>
    /// Moderate optimization - good performance with reasonable compilation time.
    /// </summary>
    Moderate = 3,

    /// <summary>
    /// Aggressive optimization - maximum performance, slower compilation.
    /// </summary>
    Aggressive = 4,

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
