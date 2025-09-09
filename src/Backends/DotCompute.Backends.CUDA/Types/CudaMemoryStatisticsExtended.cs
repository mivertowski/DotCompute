// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Extended memory statistics for CUDA memory management
    /// </summary>
    public sealed class CudaMemoryStatisticsExtended
    {
        // Base statistics (from MemoryStatistics equivalent)
        public long TotalMemoryBytes { get; set; }
        public long UsedMemoryBytes { get; set; }
        public long AvailableMemoryBytes { get; set; }
        public int AllocationCount { get; set; }
        public int DeallocationCount { get; set; }
        public long PeakMemoryUsageBytes { get; set; }

        // CUDA-specific extensions

        public long PinnedMemoryBytes { get; set; }
        public long UnifiedMemoryBytes { get; set; }
        public long PooledMemoryBytes { get; set; }
        public int PoolHits { get; set; }
        public int PoolMisses { get; set; }
        public double PoolEfficiency => PoolHits + PoolMisses > 0 ?
            (double)PoolHits / (PoolHits + PoolMisses) : 0.0;
        public long HostToDeviceTransfers { get; set; }
        public long DeviceToHostTransfers { get; set; }
        public long TotalTransferredBytes { get; set; }
        public double AverageBandwidth { get; set; } // MB/s
        public double MemoryFragmentation { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }
}