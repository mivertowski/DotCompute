// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Extended memory statistics for CUDA memory management
    /// </summary>
    public sealed class CudaMemoryStatisticsExtended
    {
        /// <summary>
        /// Gets or sets the total memory bytes.
        /// </summary>
        /// <value>The total memory bytes.</value>
        // Base statistics (from MemoryStatistics equivalent)
        public long TotalMemoryBytes { get; set; }
        /// <summary>
        /// Gets or sets the used memory bytes.
        /// </summary>
        /// <value>The used memory bytes.</value>
        public long UsedMemoryBytes { get; set; }
        /// <summary>
        /// Gets or sets the available memory bytes.
        /// </summary>
        /// <value>The available memory bytes.</value>
        public long AvailableMemoryBytes { get; set; }
        /// <summary>
        /// Gets or sets the allocation count.
        /// </summary>
        /// <value>The allocation count.</value>
        public int AllocationCount { get; set; }
        /// <summary>
        /// Gets or sets the deallocation count.
        /// </summary>
        /// <value>The deallocation count.</value>
        public int DeallocationCount { get; set; }
        /// <summary>
        /// Gets or sets the peak memory usage bytes.
        /// </summary>
        /// <value>The peak memory usage bytes.</value>
        public long PeakMemoryUsageBytes { get; set; }
        /// <summary>
        /// Gets or sets the pinned memory bytes.
        /// </summary>
        /// <value>The pinned memory bytes.</value>

        // CUDA-specific extensions

        public long PinnedMemoryBytes { get; set; }
        /// <summary>
        /// Gets or sets the unified memory bytes.
        /// </summary>
        /// <value>The unified memory bytes.</value>
        public long UnifiedMemoryBytes { get; set; }
        /// <summary>
        /// Gets or sets the pooled memory bytes.
        /// </summary>
        /// <value>The pooled memory bytes.</value>
        public long PooledMemoryBytes { get; set; }
        /// <summary>
        /// Gets or sets the pool hits.
        /// </summary>
        /// <value>The pool hits.</value>
        public int PoolHits { get; set; }
        /// <summary>
        /// Gets or sets the pool misses.
        /// </summary>
        /// <value>The pool misses.</value>
        public int PoolMisses { get; set; }
        /// <summary>
        /// Gets or sets the pool efficiency.
        /// </summary>
        /// <value>The pool efficiency.</value>
        public double PoolEfficiency => PoolHits + PoolMisses > 0 ?
            (double)PoolHits / (PoolHits + PoolMisses) : 0.0;
        /// <summary>
        /// Gets or sets the host to device transfers.
        /// </summary>
        /// <value>The host to device transfers.</value>
        public long HostToDeviceTransfers { get; set; }
        /// <summary>
        /// Gets or sets the device to host transfers.
        /// </summary>
        /// <value>The device to host transfers.</value>
        public long DeviceToHostTransfers { get; set; }
        /// <summary>
        /// Gets or sets the total transferred bytes.
        /// </summary>
        /// <value>The total transferred bytes.</value>
        public long TotalTransferredBytes { get; set; }
        /// <summary>
        /// Gets or sets the average bandwidth.
        /// </summary>
        /// <value>The average bandwidth.</value>
        public double AverageBandwidth { get; set; } // MB/s
        /// <summary>
        /// Gets or sets the memory fragmentation.
        /// </summary>
        /// <value>The memory fragmentation.</value>
        public double MemoryFragmentation { get; set; }
        /// <summary>
        /// Gets or sets the last update.
        /// </summary>
        /// <value>The last update.</value>
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }
}