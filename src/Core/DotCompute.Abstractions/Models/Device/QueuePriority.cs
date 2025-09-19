// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Models.Device
{
    /// <summary>
    /// Defines priority levels for command queues to control task scheduling and resource allocation.
    /// Higher priority queues receive preferential treatment from the device scheduler.
    /// </summary>
    /// <remarks>
    /// Queue priorities enable differentiated service levels for different types of workloads.
    /// The scheduler uses these priorities to determine execution order, resource allocation,
    /// and preemption policies. Priority levels help ensure that critical tasks receive
    /// appropriate attention while maintaining system throughput.
    /// </remarks>
    public enum QueuePriority
    {
        /// <summary>
        /// Low priority for background tasks and non-critical operations.
        /// </summary>
        /// <remarks>
        /// Low priority queues are scheduled when higher priority work is not available.
        /// Suitable for batch processing, maintenance tasks, data preprocessing,
        /// and other operations that can tolerate longer execution times.
        /// These tasks may be preempted by higher priority work.
        /// </remarks>
        Low,

        /// <summary>
        /// Normal priority for standard computational workloads.
        /// </summary>
        /// <remarks>
        /// Default priority level for most compute tasks. Provides balanced resource
        /// allocation without special treatment. Normal priority queues receive
        /// fair scheduling with other normal priority work and are the baseline
        /// for system performance expectations.
        /// </remarks>
        Normal,

        /// <summary>
        /// High priority for time-sensitive and critical operations.
        /// </summary>
        /// <remarks>
        /// High priority queues receive preferential scheduling and resource allocation.
        /// Suitable for real-time operations, interactive applications, critical
        /// system tasks, and latency-sensitive workloads. May preempt lower
        /// priority tasks to ensure timely execution.
        /// </remarks>
        High
    }
}