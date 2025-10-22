// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Enums;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA external memory handle descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalMemoryHandleDesc
    {
        /// <summary>
        /// The type.
        /// </summary>
        public CudaExternalMemoryHandleType type;
        /// <summary>
        /// The handle.
        /// </summary>
        public nint handle;
        /// <summary>
        /// The win32 handle.
        /// </summary>
        public nint win32Handle;
        /// <summary>
        /// The name.
        /// </summary>
        public nint name;
        /// <summary>
        /// The size.
        /// </summary>
        public nuint size;
        /// <summary>
        /// The flags.
        /// </summary>
        public uint flags;





    }

    /// <summary>
    /// CUDA external memory buffer descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalMemoryBufferDesc
    {
        /// <summary>
        /// The offset.
        /// </summary>
        public nuint offset;
        /// <summary>
        /// The size.
        /// </summary>
        public nuint size;
        /// <summary>
        /// The flags.
        /// </summary>
        public uint flags;





    }

    /// <summary>
    /// CUDA external memory mipmapped array descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalMemoryMipmappedArrayDesc
    {
        /// <summary>
        /// The offset.
        /// </summary>
        public nuint offset;
        /// <summary>
        /// The format desc.
        /// </summary>
        public CudaArrayFormat formatDesc;
        /// <summary>
        /// The extent.
        /// </summary>
        public CudaExtent extent;
        /// <summary>
        /// The flags.
        /// </summary>
        public uint flags;
        /// <summary>
        /// The num levels.
        /// </summary>
        public uint numLevels;





    }

    /// <summary>
    /// CUDA external semaphore handle descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreHandleDesc
    {
        /// <summary>
        /// The type.
        /// </summary>
        public CudaExternalSemaphoreHandleType type;
        /// <summary>
        /// The handle.
        /// </summary>
        public nint handle;
        /// <summary>
        /// The win32 handle.
        /// </summary>
        public nint win32Handle;
        /// <summary>
        /// The name.
        /// </summary>
        public nint name;
        /// <summary>
        /// The flags.
        /// </summary>
        public uint flags;





    }

    /// <summary>
    /// CUDA external semaphore signal parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreSignalParams
    {
        /// <summary>
        /// The flags.
        /// </summary>
        public CudaExternalSemaphoreSignalFlags flags;
        /// <summary>
        /// The node params.
        /// </summary>
        public CudaExternalSemaphoreSignalNodeParams nodeParams;
        /// <summary>
        /// The keyed mutex.
        /// </summary>
        public ulong keyedMutex;
        /// <summary>
        /// The reserved.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public uint[] reserved;





    }

    /// <summary>
    /// CUDA external semaphore wait parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreWaitParams
    {
        /// <summary>
        /// The flags.
        /// </summary>
        public CudaExternalSemaphoreWaitFlags flags;
        /// <summary>
        /// The node params.
        /// </summary>
        public CudaExternalSemaphoreWaitNodeParams nodeParams;
        /// <summary>
        /// The keyed mutex.
        /// </summary>
        public ulong keyedMutex;
        /// <summary>
        /// The reserved.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public uint[] reserved;





    }

    /// <summary>
    /// CUDA external semaphore signal node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreSignalNodeParams
    {
        /// <summary>
        /// The ext sem array.
        /// </summary>
        public nint extSemArray;
        /// <summary>
        /// The params array.
        /// </summary>
        public nint paramsArray;
        /// <summary>
        /// The num ext sems.
        /// </summary>
        public uint numExtSems;





    }

    /// <summary>
    /// CUDA external semaphore wait node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreWaitNodeParams : IEquatable<CudaExternalSemaphoreWaitNodeParams>
    {
        /// <summary>
        /// The ext sem array.
        /// </summary>
        public nint extSemArray;
        /// <summary>
        /// The params array.
        /// </summary>
        public nint paramsArray;
        /// <summary>
        /// The num ext sems.
        /// </summary>
        public uint numExtSems;

        /// <summary>
        /// Determines whether this instance is equal to another CudaExternalSemaphoreWaitNodeParams.
        /// </summary>
        /// <param name="other">The other CudaExternalSemaphoreWaitNodeParams to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public readonly bool Equals(CudaExternalSemaphoreWaitNodeParams other)
        {
            return extSemArray == other.extSemArray &&
                   paramsArray == other.paramsArray &&
                   numExtSems == other.numExtSems;
        }

        /// <summary>
        /// Determines whether this instance is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaExternalSemaphoreWaitNodeParams other && Equals(other);

        /// <summary>
        /// Gets the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override readonly int GetHashCode() => HashCode.Combine(extSemArray, paramsArray, numExtSems);

        /// <summary>
        /// Determines whether two CudaExternalSemaphoreWaitNodeParams instances are equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public static bool operator ==(CudaExternalSemaphoreWaitNodeParams left, CudaExternalSemaphoreWaitNodeParams right) => left.Equals(right);

        /// <summary>
        /// Determines whether two CudaExternalSemaphoreWaitNodeParams instances are not equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if not equal; otherwise, false.</returns>
        public static bool operator !=(CudaExternalSemaphoreWaitNodeParams left, CudaExternalSemaphoreWaitNodeParams right) => !left.Equals(right);
    }
}