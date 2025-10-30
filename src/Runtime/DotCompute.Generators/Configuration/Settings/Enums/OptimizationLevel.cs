// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Settings.Enums;

/// <summary>
/// Defines optimization levels for generated code, controlling the trade-offs
/// between performance, code size, and debuggability.
/// Note: This enum is kept separate from DotCompute.Abstractions due to netstandard2.0 compatibility requirements for source generators.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization applied, prioritizing debuggability and fast compilation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Basic optimization - minimal performance improvements with fast compilation.
    /// </summary>
    O1 = 1,

    /// <summary>
    /// Standard optimization - good performance with reasonable compilation time (recommended).
    /// </summary>
    O2 = 2,

    /// <summary>
    /// Maximum optimization - best performance, longest compilation time.
    /// </summary>
    O3 = 3,

    /// <summary>
    /// Size optimization - optimize for smallest code size rather than maximizing performance.
    /// </summary>
    Size = 4,

    /// <summary>
    /// Balanced optimization suitable for general-purpose use (maps to O2).
    /// </summary>
    Balanced = O2,

    /// <summary>
    /// Aggressive optimization for maximum performance (maps to O3).
    /// </summary>
    Aggressive = O3
}
