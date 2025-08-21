// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Represents memory usage statistics for an accelerator.
    /// </summary>
    public class MemoryStatistics
    {
        /// <summary>
        /// Gets or sets the total memory available on the device in bytes.
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets or sets the currently used memory in bytes.
        /// </summary>
        public long UsedMemory { get; set; }

        /// <summary>
        /// Gets or sets the free memory available in bytes.
        /// </summary>
        public long FreeMemory { get; set; }

        /// <summary>
        /// Gets or sets the memory allocated by this manager in bytes.
        /// </summary>
        public long AllocatedMemory { get; set; }

        /// <summary>
        /// Gets or sets the number of active allocations.
        /// </summary>
        public int AllocationCount { get; set; }

        /// <summary>
        /// Gets or sets the peak memory usage in bytes.
        /// </summary>
        public long PeakMemory { get; set; }

        /// <summary>
        /// Gets the percentage of memory currently in use.
        /// </summary>
        public double UsagePercentage => TotalMemory > 0 ? (double)UsedMemory / TotalMemory * 100 : 0;

        /// <summary>
        /// Gets the percentage of free memory available.
        /// </summary>
        public double FreePercentage => TotalMemory > 0 ? (double)FreeMemory / TotalMemory * 100 : 0;
    }
}
