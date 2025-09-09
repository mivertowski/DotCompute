// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Cache statistics for CUDA operations
    /// </summary>
    public sealed class CacheStatistics
    {
        public int HitCount { get; set; }
        public int MissCount { get; set; }
        public int TotalRequests => HitCount + MissCount;
        public int TotalEntries { get; set; }
        public long TotalSizeBytes { get; set; }
        public double HitRate { get; set; }

        public double AverageAccessCount { get; set; }
        public DateTime? OldestEntryTime { get; set; } = DateTime.UtcNow;
        public DateTime? NewestEntryTime { get; set; } = DateTime.UtcNow;
        public long CacheSizeBytes { get; set; }
        public DateTime LastAccess { get; set; } = DateTime.UtcNow;
    }
}