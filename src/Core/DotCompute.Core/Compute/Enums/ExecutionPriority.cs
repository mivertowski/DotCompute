// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Compute.Enums
{
    /// <summary>
    /// Defines execution priority levels for kernel execution scheduling.
    /// Priority levels influence how the compute engine schedules and allocates resources for kernel execution.
    /// </summary>
    /// <remarks>
    /// Execution priority affects resource allocation, scheduling order, and potential preemption behavior.
    /// Higher priority kernels may receive preferential treatment in resource allocation and may be
    /// scheduled ahead of lower priority tasks. The actual behavior depends on the underlying compute
    /// backend and system capabilities.
    /// </remarks>
    public enum ExecutionPriority
    {
        /// <summary>
        /// Low priority execution suitable for background processing and non-time-critical operations.
        /// </summary>
        /// <remarks>
        /// Low priority kernels are scheduled when system resources are available and may be
        /// preempted by higher priority tasks. This priority level is ideal for batch processing,
        /// background computation, and tasks that can tolerate longer execution times.
        /// Resource allocation may be limited to ensure system responsiveness.
        /// </remarks>
        Low,

        /// <summary>
        /// Normal priority execution for standard computational workloads.
        /// </summary>
        /// <remarks>
        /// Normal priority represents the default scheduling behavior with balanced resource
        /// allocation. Most application kernels should use this priority level as it provides
        /// a good balance between performance and system resource utilization without
        /// impacting system responsiveness.
        /// </remarks>
        Normal,

        /// <summary>
        /// High priority execution for time-sensitive and performance-critical operations.
        /// </summary>
        /// <remarks>
        /// High priority kernels receive preferential scheduling and resource allocation.
        /// This priority level should be used judiciously for tasks that require low latency
        /// or guaranteed performance characteristics. May impact the execution of lower
        /// priority tasks and overall system responsiveness.
        /// </remarks>
        High,

        /// <summary>
        /// Critical priority execution for real-time and system-critical operations.
        /// </summary>
        /// <remarks>
        /// Critical priority kernels receive the highest level of resource allocation and
        /// scheduling preference. This priority level should be reserved for mission-critical
        /// operations, real-time processing, and tasks that cannot tolerate delays.
        /// Overuse of critical priority may negatively impact system stability and responsiveness.
        /// </remarks>
        Critical
    }
}