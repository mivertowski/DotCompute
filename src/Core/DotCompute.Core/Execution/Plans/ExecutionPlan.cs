// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Execution.Plans
{
    /// <summary>
    /// Base class for execution plans that define how computational tasks are executed across devices.
    /// Provides common properties and structure for different execution strategies including data parallel,
    /// model parallel, and pipeline parallel execution.
    /// </summary>
    /// <typeparam name="T">The data type for the execution plan. Must be an unmanaged type.</typeparam>
    public abstract class ExecutionPlan<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the kernel name that will be executed.
        /// This identifies the specific computational kernel to run.
        /// </summary>
        public required string KernelName { get; set; }

        /// <summary>
        /// Gets or sets the target devices where the execution will take place.
        /// These are the accelerators (GPUs, CPUs, etc.) that will participate in the computation.
        /// </summary>
        public required IAccelerator[] Devices { get; set; }

        /// <summary>
        /// Gets or sets the execution strategy type for this plan.
        /// Determines whether this is data parallel, model parallel, pipeline parallel, or single device execution.
        /// </summary>
        public required ExecutionStrategyType StrategyType { get; set; }

        /// <summary>
        /// Gets or sets the estimated execution time in milliseconds.
        /// This value is used for scheduling and optimization decisions.
        /// </summary>
        public double EstimatedExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the plan creation timestamp.
        /// Records when this execution plan was created for tracking and debugging purposes.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
