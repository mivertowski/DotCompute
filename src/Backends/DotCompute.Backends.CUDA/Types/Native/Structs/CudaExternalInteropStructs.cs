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
        public CudaExternalMemoryHandleType type;
        public nint handle;
        public nint win32Handle;
        public nint name;
        public nuint size;
        public uint flags;
    }

    /// <summary>
    /// CUDA external memory buffer descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalMemoryBufferDesc
    {
        public nuint offset;
        public nuint size;
        public uint flags;
    }

    /// <summary>
    /// CUDA external memory mipmapped array descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalMemoryMipmappedArrayDesc
    {
        public nuint offset;
        public CudaArrayFormat formatDesc;
        public CudaExtent extent;
        public uint flags;
        public uint numLevels;
    }

    /// <summary>
    /// CUDA external semaphore handle descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreHandleDesc
    {
        public CudaExternalSemaphoreHandleType type;
        public nint handle;
        public nint win32Handle;
        public nint name;
        public uint flags;
    }

    /// <summary>
    /// CUDA external semaphore signal parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreSignalParams
    {
        public CudaExternalSemaphoreSignalFlags flags;
        public CudaExternalSemaphoreSignalNodeParams nodeParams;
        public ulong keyedMutex;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public uint[] reserved;
    }

    /// <summary>
    /// CUDA external semaphore wait parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreWaitParams
    {
        public CudaExternalSemaphoreWaitFlags flags;
        public CudaExternalSemaphoreWaitNodeParams nodeParams;
        public ulong keyedMutex;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public uint[] reserved;
    }

    /// <summary>
    /// CUDA external semaphore signal node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreSignalNodeParams
    {
        public nint extSemArray;
        public nint paramsArray;
        public uint numExtSems;
    }

    /// <summary>
    /// CUDA external semaphore wait node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreWaitNodeParams
    {
        public nint extSemArray;
        public nint paramsArray;
        public uint numExtSems;
    }
}