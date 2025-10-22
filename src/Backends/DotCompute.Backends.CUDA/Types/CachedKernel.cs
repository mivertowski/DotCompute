// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Represents cached kernel compilation data.
    /// </summary>
    public sealed class CachedKernel
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compiled kernel binary.
        /// </summary>
        public IReadOnlyList<byte> Binary { get; set; } = [];

        /// <summary>
        /// Gets or sets the compilation timestamp.
        /// </summary>
        public DateTime CompilationTime { get; set; }

        /// <summary>
        /// Gets or sets the source code hash.
        /// </summary>
        public string SourceHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compilation options hash.
        /// </summary>
        public string OptionsHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target architecture.
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compiled kernel instance.
        /// </summary>
        public CompiledKernel? Kernel { get; set; }

        /// <summary>
        /// Gets or sets the last time this cached kernel was accessed.
        /// </summary>
        /// <remarks>
        /// This property is used for cache eviction policies such as Least Recently Used (LRU).
        /// It's updated automatically when the kernel is retrieved from the cache.
        /// </remarks>
        public DateTime LastAccessTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the number of times this cached kernel has been accessed.
        /// </summary>
        /// <remarks>
        /// This property tracks usage frequency and can be used for cache statistics
        /// and eviction policies that favor frequently used kernels.
        /// </remarks>
        public int AccessCount { get; set; }


        /// <summary>
        /// Gets or sets the unique cache key for this kernel.
        /// </summary>
        /// <remarks>
        /// The cache key is typically generated from the kernel source code hash,
        /// compilation options hash, and target architecture to ensure uniqueness.
        /// </remarks>
        public string CacheKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size of the cached kernel data in bytes.
        /// </summary>
        /// <remarks>
        /// This includes the size of the compiled binary and any associated metadata.
        /// Used for memory management and cache size tracking.
        /// </remarks>
        public long Size { get; set; }


        /// <summary>
        /// Gets or sets the timestamp when this kernel cache entry was created.
        /// </summary>
        /// <remarks>
        /// This timestamp is set when the kernel is first added to the cache
        /// and is used for cache aging and statistics.
        /// </remarks>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}