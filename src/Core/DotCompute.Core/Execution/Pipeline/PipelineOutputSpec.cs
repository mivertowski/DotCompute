// Copyright (c) 2024 DotCompute. All rights reserved.

using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Pipeline
{
    /// <summary>
    /// Specification for pipeline output configuration.
    /// Defines how computed results are formatted, stored, and delivered
    /// from the pipeline. This specification controls the final data representation,
    /// memory layout, and accessibility patterns for optimal integration
    /// with downstream systems and applications.
    /// </summary>
    /// <typeparam name="T">The unmanaged data type used in the output tensors.</typeparam>
    public class PipelineOutputSpec<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the output tensor descriptions.
        /// An array of tensor specifications that define the structure, format,
        /// and characteristics of output data. Each tensor description provides
        /// metadata needed for proper memory allocation, result validation,
        /// and device-specific optimizations during pipeline result processing.
        /// </summary>
        public required TensorDescription<T>[] Tensors { get; set; }

        /// <summary>
        /// Gets or sets the output format.
        /// Specifies how the computed results should be formatted and stored.
        /// Different formats provide trade-offs between performance, memory usage,
        /// and compatibility. Native format optimizes for device performance,
        /// while other formats may optimize for host accessibility or interoperability.
        /// Defaults to Native format for optimal performance.
        /// </summary>
        public OutputFormat Format { get; set; } = OutputFormat.Native;
    }
}