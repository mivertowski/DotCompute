// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel.Enums;

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
    /// <remarks>
    /// The compiler will use default optimization strategies without
    /// any specific hints. This is the safest option when optimization
    /// behavior is uncertain.
    /// </remarks>
    None = 0,

    /// <summary>
    /// Enable aggressive function inlining.
    /// </summary>
    /// <remarks>
    /// Requests that function calls within the kernel be aggressively inlined
    /// to reduce call overhead. This can improve performance at the cost of
    /// increased code size and compilation time.
    /// </remarks>
    AggressiveInlining = 1,

    /// <summary>
    /// Enable loop unrolling optimizations.
    /// </summary>
    /// <remarks>
    /// Suggests that loops with known iteration counts should be unrolled
    /// to reduce loop overhead and enable further optimizations. This can
    /// improve performance for loops with small, fixed iteration counts.
    /// </remarks>
    LoopUnrolling = 2,

    /// <summary>
    /// Enable vectorization optimizations.
    /// </summary>
    /// <remarks>
    /// Hints that the kernel should be optimized for SIMD/vector operations.
    /// The compiler will attempt to vectorize loops and use vector instructions
    /// when beneficial for the target architecture.
    /// </remarks>
    Vectorize = 4,

    /// <summary>
    /// Enable memory prefetching optimizations.
    /// </summary>
    /// <remarks>
    /// Suggests that memory prefetch instructions should be inserted to
    /// reduce memory access latency. This is particularly beneficial for
    /// kernels with predictable memory access patterns.
    /// </remarks>
    Prefetch = 8,

    /// <summary>
    /// Enable fast math operations that may reduce numerical accuracy.
    /// </summary>
    /// <remarks>
    /// Allows the use of faster mathematical operations that may sacrifice
    /// some numerical precision for improved performance. Use with caution
    /// in applications requiring high numerical accuracy.
    /// </remarks>
    FastMath = 16,

    /// <summary>
    /// Enable all available optimizations.
    /// </summary>
    /// <remarks>
    /// Convenience value that enables all optimization hints.
    /// This provides maximum performance but may increase compilation time
    /// and potentially affect numerical stability with FastMath enabled.
    /// </remarks>
    All = AggressiveInlining | LoopUnrolling | Vectorize | Prefetch | FastMath
}
