// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// CUDA host function delegate.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CudaHostFn(IntPtr userData);

    /// <summary>
    /// CUDA memory range attributes.
    /// </summary>
    public struct CudaMemRangeAttribute : IEquatable<CudaMemRangeAttribute>
    {
        /// <summary>
        /// The data.
        /// </summary>
        public IntPtr Data;
        /// <summary>
        /// The data size.
        /// </summary>
        public nuint DataSize;
        /// <summary>
        /// The attribute.
        /// </summary>
        public uint Attribute;
        /// <summary>
        /// The dev ptr.
        /// </summary>
        public IntPtr DevPtr;
        /// <summary>
        /// The count.
        /// </summary>
        public nuint Count;

        /// <summary>
        /// Determines whether the specified CudaMemRangeAttribute is equal to the current CudaMemRangeAttribute.
        /// </summary>
        /// <param name="other">The CudaMemRangeAttribute to compare with the current instance.</param>
        /// <returns>true if the specified CudaMemRangeAttribute is equal to the current CudaMemRangeAttribute; otherwise, false.</returns>
        public readonly bool Equals(CudaMemRangeAttribute other)
        {
            return Data == other.Data &&
                   DataSize == other.DataSize &&
                   Attribute == other.Attribute &&
                   DevPtr == other.DevPtr &&
                   Count == other.Count;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current CudaMemRangeAttribute.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current CudaMemRangeAttribute; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaMemRangeAttribute other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override readonly int GetHashCode() => HashCode.Combine(Data, DataSize, Attribute, DevPtr, Count);

        /// <summary>
        /// Determines whether two specified CudaMemRangeAttribute structures have the same value.
        /// </summary>
        /// <param name="left">The first CudaMemRangeAttribute to compare.</param>
        /// <param name="right">The second CudaMemRangeAttribute to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(CudaMemRangeAttribute left, CudaMemRangeAttribute right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified CudaMemRangeAttribute structures have different values.
        /// </summary>
        /// <param name="left">The first CudaMemRangeAttribute to compare.</param>
        /// <param name="right">The second CudaMemRangeAttribute to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(CudaMemRangeAttribute left, CudaMemRangeAttribute right) => !left.Equals(right);
    }

    /// <summary>
    /// CUDA host node parameters for graph API.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaHostNodeParams
    {
        /// <summary>
        /// The fn.
        /// </summary>
        public CudaHostFn fn;
        /// <summary>
        /// The user data.
        /// </summary>
        public IntPtr userData;
    }

    /// <summary>
    /// CUDA kernel node parameters for graph API.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaKernelNodeParams
    {
        /// <summary>
        /// The func.
        /// </summary>
        public IntPtr func;                    // Kernel function
        /// <summary>
        /// The grid dim x.
        /// </summary>
        public uint gridDimX;                  // Grid dimensions X
        /// <summary>
        /// The grid dim y.
        /// </summary>
        public uint gridDimY;                  // Grid dimensions Y
        /// <summary>
        /// The grid dim z.
        /// </summary>
        public uint gridDimZ;                  // Grid dimensions Z
        /// <summary>
        /// The block dim x.
        /// </summary>
        public uint blockDimX;                 // Block dimensions X
        /// <summary>
        /// The block dim y.
        /// </summary>
        public uint blockDimY;                 // Block dimensions Y
        /// <summary>
        /// The block dim z.
        /// </summary>
        public uint blockDimZ;                 // Block dimensions Z
        /// <summary>
        /// The shared mem bytes.
        /// </summary>
        public uint sharedMemBytes;            // Dynamic shared memory size
        /// <summary>
        /// The kernel params.
        /// </summary>
        public IntPtr kernelParams;            // Kernel parameters
        /// <summary>
        /// The extra.
        /// </summary>
        public IntPtr extra;                   // Extra options
    }

    /// <summary>
    /// CUDA memory copy parameters for 3D operations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemcpy3DParms
    {
        /// <summary>
        /// The src array.
        /// </summary>
        public IntPtr srcArray;               // Source memory array
        /// <summary>
        /// The src pos.
        /// </summary>
        public CudaPos srcPos;                // Source position
        /// <summary>
        /// The src ptr.
        /// </summary>
        public IntPtr srcPtr;                 // Source pointer
        /// <summary>
        /// The dst array.
        /// </summary>
        public IntPtr dstArray;               // Destination memory array
        /// <summary>
        /// The dst pos.
        /// </summary>
        public CudaPos dstPos;                // Destination position
        /// <summary>
        /// The dst ptr.
        /// </summary>
        public IntPtr dstPtr;                 // Destination pointer
        /// <summary>
        /// The extent.
        /// </summary>
        public CudaExtent extent;              // Copy extent
        /// <summary>
        /// The kind.
        /// </summary>
        public CudaMemcpyKind kind;           // Copy kind
    }

    /// <summary>
    /// CUDA memory set parameters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemsetParams
    {
        /// <summary>
        /// The dst.
        /// </summary>
        public IntPtr dst;                    // Destination pointer
        /// <summary>
        /// The pitch.
        /// </summary>
        public nuint pitch;                   // Pitch in bytes
        /// <summary>
        /// The value.
        /// </summary>
        public uint value;                    // Value to set
        /// <summary>
        /// The element size.
        /// </summary>
        public uint elementSize;              // Element size (1, 2, or 4 bytes)
        /// <summary>
        /// The width.
        /// </summary>
        public nuint width;                   // Width in elements
        /// <summary>
        /// The height.
        /// </summary>
        public nuint height;                  // Height in elements
    }

    /// <summary>
    /// 3D position for CUDA operations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaPos : IEquatable<CudaPos>
    {
        /// <summary>
        /// The x.
        /// </summary>
        public nuint x;
        /// <summary>
        /// The y.
        /// </summary>
        public nuint y;
        /// <summary>
        /// The z.
        /// </summary>
        public nuint z;

        /// <summary>
        /// Determines whether this instance is equal to another <see cref="CudaPos"/>.
        /// </summary>
        /// <param name="other">The other instance to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public readonly bool Equals(CudaPos other) => x == other.x && y == other.y && z == other.z;

        /// <summary>
        /// Determines whether this instance is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public readonly override bool Equals(object? obj) => obj is CudaPos other && Equals(other);

        /// <summary>
        /// Gets the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public readonly override int GetHashCode() => HashCode.Combine(x, y, z);

        /// <summary>
        /// Determines whether two <see cref="CudaPos"/> instances are equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public static bool operator ==(CudaPos left, CudaPos right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="CudaPos"/> instances are not equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if not equal; otherwise, false.</returns>
        public static bool operator !=(CudaPos left, CudaPos right) => !left.Equals(right);
    }

    // CudaExtent is defined in CudaRuntimeExtended.cs

    // CudaMemcpyKind is defined in CudaMemcpyKind.cs

    /// <summary>
    /// CUDA memory advise flags for unified memory.
    /// </summary>
    public enum CudaMemoryAdvise
    {
        SetReadMostly = 1,
        UnsetReadMostly = 2,
        SetPreferredLocation = 3,
        UnsetPreferredLocation = 4,
        SetAccessedBy = 5,
        UnsetAccessedBy = 6
    }
    /// <summary>
    /// An cuda stream capture mode enumeration.
    /// </summary>

    /// <summary>
    /// CUDA stream capture modes.
    /// </summary>
    public enum CudaStreamCaptureMode
    {
        Global = 0,
        ThreadLocal = 1,
        Relaxed = 2
    }
    /// <summary>
    /// An cuda graph instantiate flags enumeration.
    /// </summary>

    /// <summary>
    /// CUDA graph instantiate flags.
    /// </summary>
    [Flags]
    public enum CudaGraphInstantiateFlags : uint
    {
        None = 0x00,
        AutoFreeOnLaunch = 0x01,
        Upload = 0x02,
        DeviceLaunch = 0x04,
        UseNodePriority = 0x08
    }

    // CudaGraphExecUpdateResult is defined in CudaRuntimeExtended.cs

    /// <summary>
    /// CUDA graph node type.
    /// </summary>
    public enum CudaGraphNodeType
    {
        Kernel = 0x00,
        Memcpy = 0x01,
        Memset = 0x02,
        Host = 0x03,
        Graph = 0x04,
        Empty = 0x05,
        WaitEvent = 0x06,
        EventRecord = 0x07,
        ExtSemasignal = 0x08,
        ExtSemawait = 0x09,
        MemAlloc = 0x0a,
        MemFree = 0x0b,
        BatchMemOp = 0x0c,
        Conditional = 0x0d
    }

    /// <summary>
    /// CUDA graph dependency type.
    /// </summary>
    public enum CudaGraphDependencyType
    {
        Default = 0,
        Programmatic = 1
    }
}