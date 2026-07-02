// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Delegates;
using DotCompute.Backends.CUDA.Types.Native.Enums;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA native launch configuration structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaNativeLaunchConfig
    {
        /// <summary>
        /// The grid dim.
        /// </summary>
        public CudaDim3 gridDim;
        /// <summary>
        /// The block dim.
        /// </summary>
        public CudaDim3 blockDim;
        /// <summary>
        /// The dynamic smem bytes.
        /// </summary>
        public nuint dynamicSmemBytes;
        /// <summary>
        /// The stream.
        /// </summary>
        public nint stream;
        /// <summary>
        /// The attrs.
        /// </summary>
        public nint attrs;
        /// <summary>
        /// The num attrs.
        /// </summary>
        public uint numAttrs;





    }

    /// <summary>
    /// CUDA 3D dimension structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaDim3
    {
        /// <summary>
        /// The x.
        /// </summary>
        public uint x;
        /// <summary>
        /// The y.
        /// </summary>
        public uint y;
        /// <summary>
        /// The z.
        /// </summary>
        public uint z;





    }

    /// <summary>
    /// CUDA launch attribute structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaLaunchAttribute
    {
        /// <summary>
        /// The id.
        /// </summary>
        public CudaLaunchAttributeID id;
        /// <summary>
        /// The val.
        /// </summary>
        public CudaLaunchAttributeValue val;





    }

    /// <summary>
    /// Programmatic-event member of the launch-attribute union. Mirrors the native
    /// <c>struct { CUevent event; int flags; int triggerAtBlockStart; }</c>. The event is an
    /// opaque driver handle, represented as <see cref="nint"/> — a managed reference type here
    /// (the old <c>CudaEvent</c> delegate) made the containing explicit-layout union fail to
    /// load on every platform (GH #182).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaProgrammaticEvent
    {
        /// <summary>The CUevent handle.</summary>
        public nint @event;
        /// <summary>Event record flags.</summary>
        public int flags;
        /// <summary>Nonzero to trigger the event at block start rather than block end.</summary>
        public int triggerAtBlockStart;
    }

    /// <summary>
    /// Launch-completion-event member of the launch-attribute union. Mirrors the native
    /// <c>struct { CUevent event; int flags; }</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaLaunchCompletionEvent
    {
        /// <summary>The CUevent handle.</summary>
        public nint @event;
        /// <summary>Event record flags.</summary>
        public int flags;
    }

    /// <summary>
    /// CUDA launch attribute value union structure. Mirrors the native
    /// <c>CUlaunchAttributeValue</c> union, which is padded to 64 bytes (<c>char pad[64]</c>).
    /// Every overlapped member must be an unmanaged type: a managed reference (class, delegate,
    /// array) at a [FieldOffset] makes the CLR refuse to load the type — and with it fail
    /// reflection over this entire assembly (GH #182).
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CudaLaunchAttributeValue
    {
        /// <summary>
        /// The access policy window.
        /// </summary>
        [FieldOffset(0)] public CudaAccessPolicyWindow accessPolicyWindow;
        /// <summary>
        /// The cooperative.
        /// </summary>
        [FieldOffset(0)] public int cooperative;
        /// <summary>
        /// The sync policy.
        /// </summary>
        [FieldOffset(0)] public CudaSynchronizationPolicy syncPolicy;
        /// <summary>
        /// The cluster dim.
        /// </summary>
        [FieldOffset(0)] public CudaClusterDimension clusterDim;
        /// <summary>
        /// The cluster scheduling policy preference.
        /// </summary>
        [FieldOffset(0)] public CudaClusterSchedulingPolicy clusterSchedulingPolicyPreference;
        /// <summary>
        /// The programmatic stream serialization allowed.
        /// </summary>
        [FieldOffset(0)] public uint programmaticStreamSerializationAllowed;
        /// <summary>
        /// The programmatic event.
        /// </summary>
        [FieldOffset(0)] public CudaProgrammaticEvent programmaticEvent;
        /// <summary>
        /// The priority.
        /// </summary>
        [FieldOffset(0)] public int priority;
        /// <summary>
        /// The mem sync domain.
        /// </summary>
        [FieldOffset(0)] public CudaLaunchMemSyncDomain memSyncDomain;
        /// <summary>
        /// The mem sync domain map.
        /// </summary>
        [FieldOffset(0)] public CudaLaunchMemSyncDomainMap memSyncDomainMap;
        /// <summary>
        /// The launch completion event.
        /// </summary>
        [FieldOffset(0)] public CudaLaunchCompletionEvent launchCompletionEvent;
        /// <summary>
        /// The device updatable kernel node.
        /// </summary>
        [FieldOffset(0)] public CudaDeviceNumaConfig deviceUpdatableKernelNode;
        /// <summary>
        /// The preferred shmem carveout.
        /// </summary>
        [FieldOffset(0)] public uint preferredShmemCarveout;
    }

    /// <summary>
    /// CUDA access policy window structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaAccessPolicyWindow
    {
        /// <summary>
        /// The base_ptr.
        /// </summary>
        public nint base_ptr;
        /// <summary>
        /// The num_bytes.
        /// </summary>
        public nuint num_bytes;
        /// <summary>
        /// The hit ratio.
        /// </summary>
        public float hitRatio;
        /// <summary>
        /// The hit prop.
        /// </summary>
        public CudaAccessProperty hitProp;
        /// <summary>
        /// The miss prop.
        /// </summary>
        public CudaAccessProperty missProp;





    }

    /// <summary>
    /// CUDA cluster dimension structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaClusterDimension
    {
        /// <summary>
        /// The x.
        /// </summary>
        public uint x;
        /// <summary>
        /// The y.
        /// </summary>
        public uint y;
        /// <summary>
        /// The z.
        /// </summary>
        public uint z;





    }

    /// <summary>
    /// CUDA launch memory sync domain map structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaLaunchMemSyncDomainMap
    {
        /// <summary>
        /// The default_.
        /// </summary>
        public byte default_;
        /// <summary>
        /// The remote.
        /// </summary>
        public byte remote;





    }

    /// <summary>
    /// CUDA device NUMA configuration structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaDeviceNumaConfig : IEquatable<CudaDeviceNumaConfig>
    {
        /// <summary>
        /// The numa node.
        /// </summary>
        public byte numaNode;
        /// <summary>
        /// The numa node id.
        /// </summary>
        public byte numaNodeId;

        /// <summary>
        /// Determines whether the specified CudaDeviceNumaConfig is equal to the current instance.
        /// </summary>
        /// <param name="other">The CudaDeviceNumaConfig to compare with the current instance.</param>
        /// <returns>true if the specified CudaDeviceNumaConfig is equal to the current instance; otherwise, false.</returns>
        public readonly bool Equals(CudaDeviceNumaConfig other)
        {
            return numaNode == other.numaNode &&
                   numaNodeId == other.numaNodeId;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaDeviceNumaConfig other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override readonly int GetHashCode() => HashCode.Combine(numaNode, numaNodeId);

        /// <summary>
        /// Determines whether two specified instances of CudaDeviceNumaConfig are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(CudaDeviceNumaConfig left, CudaDeviceNumaConfig right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified instances of CudaDeviceNumaConfig are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(CudaDeviceNumaConfig left, CudaDeviceNumaConfig right) => !left.Equals(right);
    }
}
