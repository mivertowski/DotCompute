// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda launch attribute i d enumeration.
    /// </summary>
    /// <summary>
    /// CUDA launch attribute ID enumeration
    /// </summary>
    public enum CudaLaunchAttributeID : uint
    {
        /// <summary>
        /// Ignore this attribute (no operation).
        /// </summary>
        Ignore = 0,

        /// <summary>
        /// Access policy window attribute for cache management.
        /// </summary>
        AccessPolicyWindow = 1,

        /// <summary>
        /// Cooperative kernel launch attribute.
        /// </summary>
        Cooperative = 2,

        /// <summary>
        /// Synchronization policy for kernel execution.
        /// </summary>
        SynchronizationPolicy = 3,

        /// <summary>
        /// Cluster dimension configuration for thread block clusters.
        /// </summary>
        ClusterDimension = 4,

        /// <summary>
        /// Cluster scheduling policy preference.
        /// </summary>
        ClusterSchedulingPolicyPreference = 5,

        /// <summary>
        /// Programmatic stream serialization attribute.
        /// </summary>
        ProgrammaticStreamSerialization = 6,

        /// <summary>
        /// Programmatic event attribute for synchronization.
        /// </summary>
        ProgrammaticEvent = 7,

        /// <summary>
        /// Kernel launch priority.
        /// </summary>
        Priority = 8,

        /// <summary>
        /// Memory synchronization domain mapping.
        /// </summary>
        MemSyncDomainMap = 9,

        /// <summary>
        /// Memory synchronization domain.
        /// </summary>
        MemSyncDomain = 10,

        /// <summary>
        /// Launch completion event for synchronization.
        /// </summary>
        LaunchCompletionEvent = 12,

        /// <summary>
        /// Device-updatable kernel node configuration.
        /// </summary>
        DeviceUpdatableKernelNode = 13,

        /// <summary>
        /// Preferred shared memory carveout ratio.
        /// </summary>
        PreferredSharedMemoryCarveout = 14
    }
    /// <summary>
    /// An cuda synchronization policy enumeration.
    /// </summary>

    /// <summary>
    /// CUDA synchronization policy enumeration
    /// </summary>
    public enum CudaSynchronizationPolicy : uint
    {
        /// <summary>
        /// Auto-select synchronization policy based on heuristics.
        /// </summary>
        Auto = 1,

        /// <summary>
        /// Spin-wait synchronization for minimal latency.
        /// </summary>
        Spin = 2,

        /// <summary>
        /// Yield to other threads while waiting.
        /// </summary>
        Yield = 3,

        /// <summary>
        /// Blocking synchronization using OS primitives.
        /// </summary>
        BlockingSync = 4
    }
    /// <summary>
    /// An cuda cluster scheduling policy enumeration.
    /// </summary>

    /// <summary>
    /// CUDA cluster scheduling policy enumeration
    /// </summary>
    public enum CudaClusterSchedulingPolicy : uint
    {
        /// <summary>
        /// Default cluster scheduling policy.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Spread thread blocks across SMs for maximum parallelism.
        /// </summary>
        Spread = 1,

        /// <summary>
        /// Load balancing scheduling to optimize SM utilization.
        /// </summary>
        LoadBalancing = 2
    }
    /// <summary>
    /// An cuda launch mem sync domain enumeration.
    /// </summary>

    /// <summary>
    /// CUDA launch memory sync domain enumeration
    /// </summary>
    public enum CudaLaunchMemSyncDomain : uint
    {
        /// <summary>
        /// Default memory synchronization domain.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Remote memory synchronization domain for multi-GPU.
        /// </summary>
        Remote = 1
    }
    /// <summary>
    /// An cuda access property enumeration.
    /// </summary>

    /// <summary>
    /// CUDA access property enumeration
    /// </summary>
    public enum CudaAccessProperty : uint
    {
        /// <summary>
        /// Normal memory access with standard caching.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Streaming access with limited cache retention.
        /// </summary>
        Streaming = 1,

        /// <summary>
        /// Persisting access that prefers to keep data in cache.
        /// </summary>
        Persisting = 2
    }
}
