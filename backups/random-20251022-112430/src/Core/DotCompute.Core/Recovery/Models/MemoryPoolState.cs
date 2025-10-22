// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Models
{
    /// <summary>
    /// Represents the state of a memory pool for recovery monitoring.
    /// </summary>
    public sealed class MemoryPoolState
    {
        /// <summary>
        /// Gets the pool identifier.
        /// </summary>
        public string PoolId { get; }

        /// <summary>
        /// Gets the memory pool instance.
        /// </summary>
        public IMemoryPool Pool { get; }

        /// <summary>
        /// Gets or sets the last defragmentation time.
        /// </summary>
        public DateTime? LastDefragmentationTime { get; set; }

        /// <summary>
        /// Gets or sets the last reclamation time.
        /// </summary>
        public DateTime? LastReclamationTime { get; set; }

        /// <summary>
        /// Gets or sets the number of failed recovery attempts.
        /// </summary>
        public int FailedRecoveryAttempts { get; set; }

        /// <summary>
        /// Gets or sets whether the pool is healthy.
        /// </summary>
        public bool IsHealthy { get; set; } = true;

        /// <summary>
        /// Gets or sets the last recorded fragmentation ratio.
        /// </summary>
        public double LastFragmentationRatio { get; set; }

        /// <summary>
        /// Gets or sets the last recorded available size.
        /// </summary>
        public long LastAvailableSize { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryPoolState"/> class.
        /// </summary>
        public MemoryPoolState(string poolId, IMemoryPool pool)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
            ArgumentNullException.ThrowIfNull(pool);

            PoolId = poolId;
            Pool = pool;
            LastAvailableSize = pool.AvailableSize;
            LastFragmentationRatio = pool.FragmentationRatio;
        }

        /// <summary>
        /// Updates the state with current pool metrics.
        /// </summary>
        public void UpdateMetrics()
        {
            LastAvailableSize = Pool.AvailableSize;
            LastFragmentationRatio = Pool.FragmentationRatio;
        }

        /// <summary>
        /// Marks a successful recovery operation.
        /// </summary>
        public void MarkRecoverySuccess()
        {
            FailedRecoveryAttempts = 0;
            IsHealthy = true;
        }

        /// <summary>
        /// Marks a failed recovery operation.
        /// </summary>
        public void MarkRecoveryFailure()
        {
            FailedRecoveryAttempts++;
            if (FailedRecoveryAttempts > 3)
            {
                IsHealthy = false;
            }
        }
    }
}