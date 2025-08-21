// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Device.Interfaces
{
    /// <summary>
    /// Provides information about the various cache levels and specialized
    /// cache types available on a compute device.
    /// </summary>
    public interface ICacheSizes
    {
        /// <summary>
        /// Gets the size of the Level 1 (L1) cache.
        /// </summary>
        /// <value>The L1 cache size in bytes.</value>
        /// <remarks>
        /// L1 cache is typically the fastest but smallest cache level,
        /// closest to the compute units.
        /// </remarks>
        public long L1Size { get; }

        /// <summary>
        /// Gets the size of the Level 2 (L2) cache.
        /// </summary>
        /// <value>The L2 cache size in bytes.</value>
        /// <remarks>
        /// L2 cache provides a balance between speed and capacity,
        /// typically shared among multiple compute units.
        /// </remarks>
        public long L2Size { get; }

        /// <summary>
        /// Gets the size of the Level 3 (L3) cache.
        /// </summary>
        /// <value>The L3 cache size in bytes.</value>
        /// <remarks>
        /// L3 cache is typically the largest but slowest cache level,
        /// often shared across the entire device.
        /// </remarks>
        public long L3Size { get; }

        /// <summary>
        /// Gets the size of the texture cache used for optimized texture memory access.
        /// </summary>
        /// <value>The texture cache size in bytes.</value>
        /// <remarks>
        /// Texture cache is specialized for 2D/3D spatial locality patterns
        /// commonly found in graphics and image processing workloads.
        /// </remarks>
        public long TextureCacheSize { get; }

        /// <summary>
        /// Gets the size of the constant cache used for read-only constant data.
        /// </summary>
        /// <value>The constant cache size in bytes.</value>
        /// <remarks>
        /// Constant cache is optimized for broadcasting the same data
        /// to multiple compute units simultaneously.
        /// </remarks>
        public long ConstantCacheSize { get; }
    }
}