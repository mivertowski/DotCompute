// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Interfaces.Compute
{
    /// <summary>
    /// Provides compilation metadata for kernels, including timing information, options used, and compilation results.
    /// This interface enables inspection of compilation details for debugging, profiling, and optimization purposes.
    /// </summary>
    /// <remarks>
    /// Compilation metadata is generated during the kernel compilation process and provides valuable
    /// information about how the kernel was compiled, what optimizations were applied, and any
    /// warnings or issues encountered during compilation.
    /// </remarks>
    public interface ICompilationMetadata
    {
        /// <summary>
        /// Gets the timestamp when the kernel compilation was completed.
        /// </summary>
        /// <value>
        /// A DateTimeOffset representing the exact time when kernel compilation finished.
        /// This timestamp uses UTC offset to ensure consistency across different time zones.
        /// </value>
        /// <remarks>
        /// This timestamp can be used for cache validation, debugging compilation issues,
        /// and tracking kernel compilation performance over time.
        /// </remarks>
        public DateTimeOffset CompilationTime { get; }

        /// <summary>
        /// Gets the compilation options that were used during kernel compilation.
        /// </summary>
        /// <value>
        /// A CompilationOptions object containing all the settings and flags that were
        /// applied during the compilation process, including optimization levels, debug settings,
        /// and target-specific configurations.
        /// </value>
        /// <remarks>
        /// These options directly affect the generated code quality, performance characteristics,
        /// and debugging capabilities of the compiled kernel.
        /// </remarks>
        public CompilationOptions Options { get; }

        /// <summary>
        /// Gets any compilation warnings that were generated during the kernel compilation process.
        /// </summary>
        /// <value>
        /// An array of strings containing human-readable warning messages. Each warning
        /// describes a non-critical issue or potential optimization opportunity identified
        /// during compilation. An empty array indicates no warnings were generated.
        /// </value>
        /// <remarks>
        /// Warnings do not prevent successful compilation but may indicate potential performance
        /// issues, deprecated usage patterns, or opportunities for code improvement.
        /// These messages are typically formatted for developer consumption and may include
        /// line numbers and specific recommendations.
        /// </remarks>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Gets the optimization level that was applied during compilation.
        /// </summary>
        /// <value>
        /// An OptimizationLevel enumeration value indicating the level of optimization
        /// applied to the kernel code during compilation. This affects both compilation
        /// time and runtime performance of the generated code.
        /// </value>
        /// <remarks>
        /// The optimization level determines trade-offs between compilation time, code size,
        /// and runtime performance. Higher optimization levels generally produce faster
        /// executing code at the cost of longer compilation times.
        /// </remarks>
        public OptimizationLevel OptimizationLevel { get; }
    }
}