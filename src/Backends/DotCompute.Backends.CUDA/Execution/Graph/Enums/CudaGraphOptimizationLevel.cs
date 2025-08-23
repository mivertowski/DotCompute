// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Enums
{
    /// <summary>
    /// Specifies the optimization level to apply during CUDA graph compilation.
    /// Controls the trade-off between compilation time and execution performance.
    /// </summary>
    /// <remarks>
    /// Higher optimization levels generally produce faster executing graphs but require
    /// more time during the compilation phase. Choose the appropriate level based on
    /// your application's requirements for compilation speed versus runtime performance.
    /// </remarks>
    public enum CudaGraphOptimizationLevel
    {
        /// <summary>
        /// No optimization passes are applied. Provides fastest compilation.
        /// </summary>
        /// <remarks>
        /// Use this level when compilation speed is critical or when debugging
        /// graph execution issues that might be obscured by optimizations.
        /// </remarks>
        None,

        /// <summary>
        /// Basic optimizations are applied with minimal compilation overhead.
        /// </summary>
        /// <remarks>
        /// Includes fundamental optimizations such as dead code elimination
        /// and basic memory access pattern improvements with fast compilation.
        /// </remarks>
        Basic,

        /// <summary>
        /// Balanced optimization providing good performance with reasonable compilation time.
        /// </summary>
        /// <remarks>
        /// This is the recommended level for most applications, providing significant
        /// performance improvements while maintaining acceptable compilation times.
        /// Includes kernel fusion, memory optimization, and basic architecture tuning.
        /// </remarks>
        Balanced,

        /// <summary>
        /// Aggressive optimization for maximum performance with longer compilation time.
        /// </summary>
        /// <remarks>
        /// Applies all available optimization passes including advanced kernel fusion,
        /// comprehensive memory hierarchy optimization, and extensive architecture-specific
        /// tuning. Best suited for production workloads where graph execution time is critical.
        /// </remarks>
        Aggressive
    }
}