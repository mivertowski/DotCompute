// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Models
{
    /// <summary>
    /// Interface for memory pools that can be monitored and recovered.
    /// </summary>
    public interface IMemoryPool
    {
        /// <summary>
        /// Gets the pool identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the total capacity of the pool in bytes.
        /// </summary>
        public long TotalCapacity { get; }

        /// <summary>
        /// Gets the currently allocated size in bytes.
        /// </summary>
        public long AllocatedSize { get; }

        /// <summary>
        /// Gets the available size in bytes.
        /// </summary>
        public long AvailableSize { get; }

        /// <summary>
        /// Gets the fragmentation ratio (0.0 to 1.0).
        /// </summary>
        public double FragmentationRatio { get; }

        /// <summary>
        /// Attempts to defragment the memory pool.
        /// </summary>
        /// <returns>True if defragmentation was successful.</returns>
        public ValueTask<bool> DefragmentAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to reclaim unused memory.
        /// </summary>
        /// <returns>The amount of memory reclaimed in bytes.</returns>
        public ValueTask<long> ReclaimMemoryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the pool to its initial state.
        /// </summary>
        public ValueTask ResetAsync(CancellationToken cancellationToken = default);
    }
}