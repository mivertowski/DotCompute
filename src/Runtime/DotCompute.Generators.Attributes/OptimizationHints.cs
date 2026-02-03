// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Generators;

/// <summary>
/// Provides optimization hints to guide the kernel compilation process.
/// </summary>
/// <remarks>
/// These flags help the code generator make informed decisions about various
/// optimization strategies. Multiple hints can be combined using bitwise OR operations.
/// The effectiveness of each hint depends on the target backend and hardware capabilities.
/// </remarks>
[Flags]
public enum OptimizationHints
{
    /// <summary>
    /// No specific optimizations requested.
    /// </summary>
    None = 0,

    /// <summary>
    /// Enable aggressive function inlining.
    /// </summary>
    AggressiveInlining = 1,

    /// <summary>
    /// Enable loop unrolling optimizations.
    /// </summary>
    LoopUnrolling = 2,

    /// <summary>
    /// Enable vectorization optimizations.
    /// </summary>
    Vectorize = 4,

    /// <summary>
    /// Enable memory prefetching optimizations.
    /// </summary>
    Prefetch = 8,

    /// <summary>
    /// Enable fast math operations that may reduce numerical accuracy.
    /// </summary>
    FastMath = 16,

    /// <summary>
    /// Enable all available optimizations.
    /// </summary>
    All = AggressiveInlining | LoopUnrolling | Vectorize | Prefetch | FastMath
}
