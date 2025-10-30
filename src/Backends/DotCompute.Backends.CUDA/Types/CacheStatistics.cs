// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Cache statistics for CUDA operations
    /// </summary>
    public sealed class CacheStatistics
    {
        /// <summary>
        /// Gets or sets the hit count.
        /// </summary>
        /// <value>The hit count.</value>
        public int HitCount { get; set; }
        /// <summary>
        /// Gets or sets the miss count.
        /// </summary>
        /// <value>The miss count.</value>
        public int MissCount { get; set; }
        /// <summary>
        /// Gets or sets the total requests.
        /// </summary>
        /// <value>The total requests.</value>
        public int TotalRequests => HitCount + MissCount;
        /// <summary>
        /// Gets or sets the total entries.
        /// </summary>
        /// <value>The total entries.</value>
        public int TotalEntries { get; set; }
        /// <summary>
        /// Gets or sets the total size bytes.
        /// </summary>
        /// <value>The total size bytes.</value>
        public long TotalSizeBytes { get; set; }
        /// <summary>
        /// Gets or sets the hit rate.
        /// </summary>
        /// <value>The hit rate.</value>
        public double HitRate { get; set; }
        /// <summary>
        /// Gets or sets the average access count.
        /// </summary>
        /// <value>The average access count.</value>

        public double AverageAccessCount { get; set; }
        /// <summary>
        /// Gets or sets the oldest entry time.
        /// </summary>
        /// <value>The oldest entry time.</value>
        public DateTime? OldestEntryTime { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Gets or sets the newest entry time.
        /// </summary>
        /// <value>The newest entry time.</value>
        public DateTime? NewestEntryTime { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Gets or sets the cache size bytes.
        /// </summary>
        /// <value>The cache size bytes.</value>
        public long CacheSizeBytes { get; set; }
        /// <summary>
        /// Gets or sets the last access.
        /// </summary>
        /// <value>The last access.</value>
        public DateTime LastAccess { get; set; } = DateTime.UtcNow;
    }
}
