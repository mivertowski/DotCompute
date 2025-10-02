// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Execution.Workload
{
    /// <summary>
    /// Represents a unit of work for work stealing execution.
    /// Each work item contains the data and metadata needed for independent execution.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type for the work item data</typeparam>
    public class WorkItem<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the unique identifier for this work item.
        /// </summary>
        public required int Id { get; set; }

        /// <summary>
        /// Gets or sets the input data buffers for this work item.
        /// These contain the data that will be processed by this work unit.
        /// </summary>
        public required IUnifiedMemoryBuffer<T>[] InputBuffers { get; set; }

        /// <summary>
        /// Gets or sets the output data buffers for this work item.
        /// These will contain the results after processing is complete.
        /// </summary>
        public required IUnifiedMemoryBuffer<T>[] OutputBuffers { get; set; }

        /// <summary>
        /// Gets or sets the estimated processing time for this work item in milliseconds.
        /// This is used for load balancing and scheduling decisions.
        /// </summary>
        public double EstimatedProcessingTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the list of work item IDs that this work item depends on.
        /// This work item cannot be executed until all dependencies are completed.
        /// </summary>
        public IList<int> Dependencies { get; } = [];
    }
}