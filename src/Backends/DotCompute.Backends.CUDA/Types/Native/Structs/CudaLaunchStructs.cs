// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Enums;
using DotCompute.Backends.CUDA.Types.Native.Delegates;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA native launch configuration structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaNativeLaunchConfig
    {
        public CudaDim3 gridDim;
        public CudaDim3 blockDim;
        public nuint dynamicSmemBytes;
        public nint stream;
        public nint attrs;
        public uint numAttrs;
    }

    /// <summary>
    /// CUDA 3D dimension structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaDim3
    {
        public uint x;
        public uint y;
        public uint z;
    }

    /// <summary>
    /// CUDA launch attribute structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaLaunchAttribute
    {
        public CudaLaunchAttributeID id;
        public CudaLaunchAttributeValue val;
    }

    /// <summary>
    /// CUDA launch attribute value union structure
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct CudaLaunchAttributeValue
    {
        [FieldOffset(0)] public CudaAccessPolicyWindow accessPolicyWindow;
        [FieldOffset(0)] public int cooperative;
        [FieldOffset(0)] public CudaSynchronizationPolicy syncPolicy;
        [FieldOffset(0)] public CudaClusterDimension clusterDim;
        [FieldOffset(0)] public CudaClusterSchedulingPolicy clusterSchedulingPolicyPreference;
        [FieldOffset(0)] public uint programmaticStreamSerializationAllowed;
        [FieldOffset(0)] public CudaEvent programmaticEvent;
        [FieldOffset(0)] public int priority;
        [FieldOffset(0)] public CudaLaunchMemSyncDomain memSyncDomain;
        [FieldOffset(0)] public CudaLaunchMemSyncDomainMap memSyncDomainMap;
        [FieldOffset(0)] public ulong launchCompletionEvent;
        [FieldOffset(0)] public CudaDeviceNumaConfig deviceUpdatableKernelNode;
        [FieldOffset(0)] public uint preferredShmemCarveout;
    }

    /// <summary>
    /// CUDA access policy window structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaAccessPolicyWindow
    {
        public nint base_ptr;
        public nuint num_bytes;
        public float hitRatio;
        public CudaAccessProperty hitProp;
        public CudaAccessProperty missProp;
    }

    /// <summary>
    /// CUDA cluster dimension structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaClusterDimension
    {
        public uint x;
        public uint y;
        public uint z;
    }

    /// <summary>
    /// CUDA launch memory sync domain map structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaLaunchMemSyncDomainMap
    {
        public byte default_;
        public byte remote;
    }

    /// <summary>
    /// CUDA device NUMA configuration structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaDeviceNumaConfig
    {
        public byte numaNode;
        public byte numaNodeId;
    }
}