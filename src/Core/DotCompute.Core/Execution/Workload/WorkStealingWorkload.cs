// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Optimization;

namespace DotCompute.Core.Execution.Workload
{
    /// <summary>
    /// Work stealing workload specification for dynamic load balancing
    /// where idle processors can steal work from busy processors.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type for the workload data</typeparam>
    public class WorkStealingWorkload<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the collection of work items that can be executed independently
        /// and potentially stolen by idle processors.
        /// </summary>
        public required List<WorkItem<T>> WorkItems { get; set; }

        /// <summary>
        /// Gets or sets the load balancing hints to guide the work stealing strategy.
        /// This is optional and can be null if no specific hints are provided.
        /// </summary>
        public LoadBalancingHints? LoadBalancingHints { get; set; }
    }
}