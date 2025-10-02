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
        Ignore = 0,
        AccessPolicyWindow = 1,
        Cooperative = 2,
        SynchronizationPolicy = 3,
        ClusterDimension = 4,
        ClusterSchedulingPolicyPreference = 5,
        ProgrammaticStreamSerialization = 6,
        ProgrammaticEvent = 7,
        Priority = 8,
        MemSyncDomainMap = 9,
        MemSyncDomain = 10,
        LaunchCompletionEvent = 12,
        DeviceUpdatableKernelNode = 13,
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
        Auto = 1,
        Spin = 2,
        Yield = 3,
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
        Default = 0,
        Spread = 1,
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
        Default = 0,
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
        Normal = 0,
        Streaming = 1,
        Persisting = 2
    }
}