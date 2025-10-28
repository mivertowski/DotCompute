// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Execution;

namespace DotCompute.Core.Compute.Options
{
    /// <summary>
    /// Provides configuration options for kernel execution, including work group sizing, priority settings, and profiling controls.
    /// This class encapsulates all execution-time parameters that affect how kernels are dispatched and executed on compute backends.
    /// </summary>
    /// <remarks>
    /// ExecutionOptions allows fine-grained control over kernel execution behavior across different compute backends.
    /// The options are designed to be backend-agnostic while providing meaningful performance tuning capabilities.
    /// Default values are chosen to provide reasonable performance for most use cases.
    /// </remarks>
    public sealed class ExecutionOptions
    {
        /// <summary>
        /// Gets or sets the global work size for kernel execution.
        /// </summary>
        /// <value>
        /// A read-only list of long values representing the total number of work items in each dimension.
        /// Each element corresponds to a dimension (X, Y, Z) of the compute space.
        /// A null value indicates that the compute engine should determine the work size automatically
        /// based on the kernel requirements and available hardware.
        /// </value>
        /// <remarks>
        /// The global work size determines the total number of kernel instances that will be executed.
        /// For multi-dimensional kernels, this list specifies the size in each dimension.
        /// The total number of work items is the product of all dimensions.
        /// Values must be positive and within the limits supported by the target backend.
        /// </remarks>
        public IReadOnlyList<long>? GlobalWorkSize { get; set; }

        /// <summary>
        /// Gets or sets the local work size (work group size) for kernel execution.
        /// </summary>
        /// <value>
        /// A read-only list of long values representing the number of work items in each work group dimension.
        /// Each element corresponds to a dimension (X, Y, Z) of the local work group.
        /// A null value allows the compute engine to select optimal work group sizes based on
        /// hardware capabilities and kernel characteristics.
        /// </value>
        /// <remarks>
        /// The local work size affects memory access patterns, occupancy, and performance characteristics.
        /// Work items within the same work group can share local memory and synchronize execution.
        /// Optimal work group sizes depend on the target hardware and kernel memory access patterns.
        /// Values must be positive and should be powers of 2 for best performance on most hardware.
        /// </remarks>
        public IReadOnlyList<long>? LocalWorkSize { get; set; }

        /// <summary>
        /// Gets or sets the work group offset for kernel execution.
        /// </summary>
        /// <value>
        /// A read-only list of long values representing the offset applied to work item IDs in each dimension.
        /// Each element corresponds to a dimension (X, Y, Z) offset.
        /// A null value indicates no offset (starting from 0 in all dimensions).
        /// </value>
        /// <remarks>
        /// Work group offset allows kernels to operate on a subset of a larger data space
        /// without modifying the kernel code. This is useful for tiled processing and
        /// distributed computation scenarios where different kernel invocations process
        /// different regions of the same dataset.
        /// </remarks>
        public IReadOnlyList<long>? WorkGroupOffset { get; set; }

        /// <summary>
        /// Gets or sets the execution priority for kernel scheduling.
        /// </summary>
        /// <value>
        /// An ExecutionPriority enumeration value that influences how the kernel is scheduled
        /// relative to other compute tasks. Default value is ExecutionPriority.Normal.
        /// </value>
        /// <remarks>
        /// Execution priority affects resource allocation and scheduling order but does not
        /// guarantee execution timing. Higher priority kernels may preempt lower priority ones
        /// depending on the backend implementation and system load.
        /// </remarks>
        public ExecutionPriority Priority { get; set; } = ExecutionPriority.Normal;

        /// <summary>
        /// Gets or sets whether to enable profiling information collection during kernel execution.
        /// </summary>
        /// <value>
        /// A boolean value indicating whether the compute engine should collect detailed
        /// timing and performance information during kernel execution. Default value is false.
        /// </value>
        /// <remarks>
        /// Enabling profiling may introduce slight performance overhead but provides valuable
        /// information for performance analysis and optimization. Profiling data typically
        /// includes execution timing, memory transfer times, and resource utilization metrics.
        /// The availability and detail level of profiling information depends on the backend capabilities.
        /// </remarks>
        public bool EnableProfiling { get; set; }

        /// <summary>
        /// Gets or sets the timeout for kernel execution.
        /// </summary>
        /// <value>
        /// A TimeSpan representing the maximum allowed execution time for the kernel.
        /// A null value indicates no timeout (kernel can run indefinitely until completion).
        /// </value>
        /// <remarks>
        /// Execution timeout provides protection against runaway kernels and ensures system
        /// responsiveness. If the timeout is exceeded, the kernel execution is terminated
        /// and an exception is thrown. The actual timeout behavior may vary between backends
        /// and some platforms may not support fine-grained timeout control.
        /// </remarks>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets the default execution options with standard settings suitable for most use cases.
        /// </summary>
        /// <value>
        /// A static ExecutionOptions instance configured with default values:
        /// - GlobalWorkSize: null (automatic)
        /// - LocalWorkSize: null (automatic)
        /// - WorkGroupOffset: null (no offset)
        /// - Priority: Normal
        /// - EnableProfiling: false
        /// - Timeout: null (no timeout)
        /// </value>
        /// <remarks>
        /// The default options are designed to provide reasonable performance and compatibility
        /// across different compute backends without requiring manual tuning. These settings
        /// can be used as a starting point and customized based on specific application requirements.
        /// </remarks>
        public static ExecutionOptions Default { get; } = new();
    }
}