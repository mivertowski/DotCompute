// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Enums
{
    /// <summary>
    /// Specifies the warp scheduling strategy for kernel execution.
    /// Controls how warps are scheduled and managed during kernel execution on Ada Lovelace architecture.
    /// </summary>
    /// <remarks>
    /// Warp scheduling affects how groups of 32 threads (warps) are scheduled for execution
    /// on streaming multiprocessors. Different scheduling modes can optimize for latency,
    /// throughput, or resource utilization depending on the kernel characteristics.
    /// </remarks>
    public enum WarpSchedulingMode
    {
        /// <summary>
        /// Default warp scheduling determined by the CUDA runtime.
        /// </summary>
        /// <remarks>
        /// Uses the standard CUDA warp scheduling algorithm which balances latency
        /// and throughput for general-purpose kernels. This is suitable for most
        /// applications and provides predictable performance characteristics.
        /// </remarks>
        Default,

        /// <summary>
        /// Persistent warp scheduling for long-running kernels with sustained workload.
        /// </summary>
        /// <remarks>
        /// Optimizes scheduling for kernels that run for extended periods with consistent
        /// workload. This mode can improve performance for iterative algorithms and
        /// streaming applications by reducing scheduling overhead and improving cache locality.
        /// Particularly effective on Ada Lovelace architecture with enhanced warp schedulers.
        /// </remarks>
        Persistent,

        /// <summary>
        /// Dynamic warp scheduling that adapts to workload characteristics during execution.
        /// </summary>
        /// <remarks>
        /// Uses adaptive scheduling algorithms that monitor kernel execution patterns
        /// and adjust scheduling decisions to optimize performance dynamically.
        /// This mode is most effective for kernels with varying computational intensity
        /// or irregular memory access patterns.
        /// </remarks>
        Dynamic
    }
}