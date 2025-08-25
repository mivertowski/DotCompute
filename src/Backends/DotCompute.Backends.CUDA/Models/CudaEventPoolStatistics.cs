// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Statistics for the CUDA event pool.
    /// </summary>
    public sealed class CudaEventPoolStatistics
    {
        /// <summary>
        /// Gets or sets the number of timing events in the pool.
        /// </summary>
        public int TimingEvents { get; set; }

        /// <summary>
        /// Gets or sets the number of synchronization events in the pool.
        /// </summary>
        public int SyncEvents { get; set; }

        /// <summary>
        /// Gets or sets the total number of pooled events.
        /// </summary>
        public int TotalPooledEvents { get; set; }

        /// <summary>
        /// Gets or sets the total number of timing events created.
        /// </summary>
        public long TotalTimingEventsCreated { get; set; }

        /// <summary>
        /// Gets or sets the total number of synchronization events created.
        /// </summary>
        public long TotalSyncEventsCreated { get; set; }

        /// <summary>
        /// Gets or sets the total number of events acquired from the pool.
        /// </summary>
        public long TotalEventsAcquired { get; set; }

        /// <summary>
        /// Gets or sets the total number of events returned to the pool.
        /// </summary>
        public long TotalEventsReturned { get; set; }

        /// <summary>
        /// Gets or sets the number of currently active (acquired) events.
        /// </summary>
        public long ActiveEvents { get; set; }

        /// <summary>
        /// Gets or sets the pool utilization percentage (0.0 to 1.0).
        /// </summary>
        public double PoolUtilization { get; set; }

        /// <summary>
        /// Gets or sets the average number of times each event has been acquired.
        /// </summary>
        public double AverageAcquireCount { get; set; }
    }
}