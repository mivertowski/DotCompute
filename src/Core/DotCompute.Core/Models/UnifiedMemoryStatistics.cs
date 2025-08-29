using System;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Statistics for unified memory management.
    /// </summary>
    public class UnifiedMemoryStatistics
    {
        /// <summary>
        /// Gets or sets the total number of allocations.
        /// </summary>
        public int TotalAllocations { get; set; }

        /// <summary>
        /// Gets or sets the number of active allocations.
        /// </summary>
        public int ActiveAllocations { get; set; }

        /// <summary>
        /// Gets or sets the total bytes allocated.
        /// </summary>
        public long TotalBytesAllocated { get; set; }

        /// <summary>
        /// Gets or sets the current bytes in use.
        /// </summary>
        public long CurrentBytesInUse { get; set; }

        /// <summary>
        /// Gets or sets the peak memory usage.
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the total number of prefetch operations.
        /// </summary>
        public long PrefetchOperations { get; set; }

        /// <summary>
        /// Gets or sets the total bytes prefetched.
        /// </summary>
        public long BytesPrefetched { get; set; }

        /// <summary>
        /// Gets or sets the number of memory advice calls.
        /// </summary>
        public int MemoryAdviceCount { get; set; }

        /// <summary>
        /// Gets or sets the total migration time.
        /// </summary>
        public TimeSpan TotalMigrationTime { get; set; }

        /// <summary>
        /// Gets or sets when statistics collection started.
        /// </summary>
        public DateTimeOffset CollectionStarted { get; set; } = DateTimeOffset.UtcNow;
    }
}