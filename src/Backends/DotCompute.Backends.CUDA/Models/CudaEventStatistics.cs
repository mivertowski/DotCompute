// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Statistics for the CUDA event manager.
    /// </summary>
    public sealed class CudaEventStatistics
    {
        /// <summary>
        /// Gets or sets the number of currently active events.
        /// </summary>
        public int ActiveEvents { get; set; }

        /// <summary>
        /// Gets or sets the number of completed events.
        /// </summary>
        public int CompletedEvents { get; set; }

        /// <summary>
        /// Gets or sets the number of pending events.
        /// </summary>
        public int PendingEvents { get; set; }

        /// <summary>
        /// Gets or sets the number of timing events.
        /// </summary>
        public int TimingEvents { get; set; }

        /// <summary>
        /// Gets or sets the number of synchronization events.
        /// </summary>
        public int SyncEvents { get; set; }

        /// <summary>
        /// Gets or sets the total number of events created.
        /// </summary>
        public long TotalEventsCreated { get; set; }

        /// <summary>
        /// Gets or sets the total number of timing measurements performed.
        /// </summary>
        public long TotalTimingMeasurements { get; set; }

        /// <summary>
        /// Gets or sets the average age of active events in seconds.
        /// </summary>
        public double AverageEventAge { get; set; }

        /// <summary>
        /// Gets or sets the number of active timing sessions.
        /// </summary>
        public int ActiveTimingSessions { get; set; }

        /// <summary>
        /// Gets or sets the event pool statistics.
        /// </summary>
        public CudaEventPoolStatistics? PoolStatistics { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent events allowed.
        /// </summary>
        public int MaxConcurrentEvents { get; set; }
    }
}
