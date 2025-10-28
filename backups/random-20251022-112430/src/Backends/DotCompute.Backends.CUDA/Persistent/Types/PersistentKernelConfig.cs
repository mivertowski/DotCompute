// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Persistent.Types
{
    /// <summary>
    /// Configuration for persistent, grid-resident CUDA kernels.
    /// </summary>
    public sealed class PersistentKernelConfig
    {
        /// <summary>
        /// Gets or sets whether the kernel should be grid-resident (persistent across multiple launches).
        /// </summary>
        public bool GridResident { get; set; } = true;

        /// <summary>
        /// Gets or sets the depth of the ring buffer for temporal data (e.g., 3 for t, t-1, t-2).
        /// </summary>
        public int RingBufferDepth { get; set; } = 3;

        /// <summary>
        /// Gets or sets the number of SMs (Streaming Multiprocessors) to use.
        /// Default is -1 which means use all available SMs.
        /// </summary>
        public int SMCount { get; set; } = -1;

        /// <summary>
        /// Gets or sets the maximum number of iterations the persistent kernel should run.
        /// </summary>
        public int MaxIterations { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether to use cooperative groups for inter-block synchronization.
        /// </summary>
        public bool UseCooperativeGroups { get; set; }


        /// <summary>
        /// Gets or sets the shared memory size per block in bytes.
        /// </summary>
        public uint SharedMemoryBytes { get; set; }


        /// <summary>
        /// Gets or sets the block size for the persistent kernel.
        /// </summary>
        public int BlockSize { get; set; } = 256;

        /// <summary>
        /// Gets or sets whether to enable automatic load balancing across SMs.
        /// </summary>
        public bool EnableLoadBalancing { get; set; } = true;

        /// <summary>
        /// Gets or sets the synchronization mode for the persistent kernel.
        /// </summary>
        public SynchronizationMode SyncMode { get; set; } = SynchronizationMode.Atomic;

        /// <summary>
        /// Validates the configuration and throws if invalid.
        /// </summary>
        public void Validate()
        {
            if (RingBufferDepth < 2)
            {
                throw new ArgumentException("Ring buffer depth must be at least 2", nameof(RingBufferDepth));
            }

            if (MaxIterations <= 0)
            {
                throw new ArgumentException("Max iterations must be positive", nameof(MaxIterations));
            }

            if (BlockSize is <= 0 or > 1024)
            {
                throw new ArgumentException("Block size must be between 1 and 1024", nameof(BlockSize));
            }

            if (SharedMemoryBytes > 48 * 1024) // 48KB is typical max for most GPUs
            {
                throw new ArgumentException("Shared memory exceeds typical GPU limits", nameof(SharedMemoryBytes));
            }
        }
    }

    /// <summary>
    /// Synchronization modes for persistent kernels.
    /// </summary>
    public enum SynchronizationMode
    {
        /// <summary>
        /// Use atomic operations for synchronization.
        /// </summary>
        Atomic,

        /// <summary>
        /// Use global memory flags for synchronization.
        /// </summary>
        GlobalMemory,

        /// <summary>
        /// Use cooperative groups for grid-wide synchronization.
        /// </summary>
        CooperativeGroups,

        /// <summary>
        /// No explicit synchronization (for embarrassingly parallel workloads).
        /// </summary>
        None
    }

    /// <summary>
    /// Wave equation types supported by the persistent kernel system.
    /// </summary>
    public enum WaveEquationType
    {
        /// <summary>
        /// 1D acoustic wave equation.
        /// </summary>
        Acoustic1D,

        /// <summary>
        /// 2D acoustic wave equation.
        /// </summary>
        Acoustic2D,

        /// <summary>
        /// 3D acoustic wave equation.
        /// </summary>
        Acoustic3D,

        /// <summary>
        /// 2D electromagnetic wave (Maxwell's equations).
        /// </summary>
        Electromagnetic2D,

        /// <summary>
        /// 2D seismic wave equation.
        /// </summary>
        Seismic2D,

        /// <summary>
        /// Custom wave equation (user-provided kernel).
        /// </summary>
        Custom
    }
}