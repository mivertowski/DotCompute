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
    public struct CudaExternalSemaphoreWaitNodeParams
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
}