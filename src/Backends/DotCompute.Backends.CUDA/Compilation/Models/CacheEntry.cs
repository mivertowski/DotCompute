using System;

namespace DotCompute.Backends.CUDA.Compilation.Models
{
    /// <summary>
    /// Represents a cached kernel entry.
    /// </summary>
    public class CacheEntry
    {
        /// <summary>
        /// Gets or sets the cache key.
        /// </summary>
        public string Key { get; set; } = "";

        /// <summary>
        /// Gets or sets the cached kernel.
        /// </summary>
        public CompiledKernel? Kernel { get; set; }

        /// <summary>
        /// Gets or sets when the entry was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the entry was last accessed.
        /// </summary>
        public DateTimeOffset LastAccessedAt { get; set; }

        /// <summary>
        /// Gets or sets the number of times this entry has been accessed.
        /// </summary>
        public int AccessCount { get; set; }

        /// <summary>
        /// Gets or sets the size of the cached data in bytes.
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Gets or sets the time-to-live for this entry.
        /// </summary>
        public TimeSpan? TimeToLive { get; set; }

        /// <summary>
        /// Gets or sets whether this entry is pinned in cache.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// Gets whether this entry has expired.
        /// </summary>
        public bool IsExpired => 
            TimeToLive.HasValue && 
            DateTimeOffset.UtcNow - CreatedAt > TimeToLive.Value;
    }
}