// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Models
{
    /// <summary>
    /// Feature support information for CUDA device.
    /// </summary>
    public sealed class CudaFeatureSupport
    {
        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compute capability (e.g., "8.9" for Ada).
        /// </summary>
        public string ComputeCapability { get; set; } = string.Empty;

        // Cooperative Groups

        /// <summary>
        /// Gets or sets whether cooperative kernel launch is supported.
        /// </summary>
        public bool CooperativeLaunch { get; set; }

        /// <summary>
        /// Gets or sets whether cooperative multi-device launch is supported.
        /// </summary>
        public bool CooperativeMultiDeviceLaunch { get; set; }

        // Dynamic Parallelism

        /// <summary>
        /// Gets or sets whether dynamic parallelism is supported.
        /// </summary>
        public bool DynamicParallelism { get; set; }

        // Unified Memory

        /// <summary>
        /// Gets or sets whether unified addressing is supported.
        /// </summary>
        public bool UnifiedAddressing { get; set; }

        /// <summary>
        /// Gets or sets whether managed memory is supported.
        /// </summary>
        public bool ManagedMemory { get; set; }

        /// <summary>
        /// Gets or sets whether concurrent managed access is supported.
        /// </summary>
        public bool ConcurrentManagedAccess { get; set; }

        /// <summary>
        /// Gets or sets whether pageable memory access is supported.
        /// </summary>
        public bool PageableMemoryAccess { get; set; }

        // Tensor Cores

        /// <summary>
        /// Gets or sets whether tensor cores are available.
        /// </summary>
        public bool TensorCores { get; set; }

        /// <summary>
        /// Gets or sets the tensor core generation (e.g., "3rd Gen" for Ada).
        /// </summary>
        public string TensorCoreGeneration { get; set; } = string.Empty;

        // Memory and Performance

        /// <summary>
        /// Gets or sets the L2 cache size in bytes.
        /// </summary>
        public int L2CacheSize { get; set; }

        /// <summary>
        /// Gets or sets the shared memory per block in bytes.
        /// </summary>
        public ulong SharedMemoryPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the shared memory per multiprocessor in bytes.
        /// </summary>
        public ulong SharedMemoryPerMultiprocessor { get; set; }

        /// <summary>
        /// Gets or sets the maximum shared memory per block opt-in in bytes.
        /// </summary>
        public ulong MaxSharedMemoryPerBlockOptin { get; set; }

        // Advanced features

        /// <summary>
        /// Gets or sets whether stream priorities are supported.
        /// </summary>
        public bool StreamPriorities { get; set; }

        /// <summary>
        /// Gets or sets whether global L1 cache is available.
        /// </summary>
        public bool GlobalL1Cache { get; set; }

        /// <summary>
        /// Gets or sets whether local L1 cache is available.
        /// </summary>
        public bool LocalL1Cache { get; set; }

        /// <summary>
        /// Gets or sets whether compute preemption is supported.
        /// </summary>
        public bool ComputePreemption { get; set; }
    }
}
