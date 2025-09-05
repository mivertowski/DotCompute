// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
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
    public struct CudaMemRangeAttribute
    {
        public IntPtr Data;
        public nuint DataSize;
        public uint Attribute;
        public IntPtr DevPtr;
        public nuint Count;
    }

    /// <summary>
    /// CUDA host node parameters for graph API.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaHostNodeParams
    {
        public CudaHostFn fn;
        public IntPtr userData;
    }

    /// <summary>
    /// CUDA kernel node parameters for graph API.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaKernelNodeParams
    {
        public IntPtr func;                    // Kernel function
        public uint gridDimX;                  // Grid dimensions X
        public uint gridDimY;                  // Grid dimensions Y
        public uint gridDimZ;                  // Grid dimensions Z
        public uint blockDimX;                 // Block dimensions X
        public uint blockDimY;                 // Block dimensions Y
        public uint blockDimZ;                 // Block dimensions Z
        public uint sharedMemBytes;            // Dynamic shared memory size
        public IntPtr kernelParams;            // Kernel parameters
        public IntPtr extra;                   // Extra options
    }

    /// <summary>
    /// CUDA memory copy parameters for 3D operations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemcpy3DParms
    {
        public IntPtr srcArray;               // Source memory array
        public CudaPos srcPos;                // Source position
        public IntPtr srcPtr;                 // Source pointer
        public IntPtr dstArray;               // Destination memory array
        public CudaPos dstPos;                // Destination position
        public IntPtr dstPtr;                 // Destination pointer
        public CudaExtent extent;              // Copy extent
        public CudaMemcpyKind kind;           // Copy kind
    }

    /// <summary>
    /// CUDA memory set parameters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemsetParams
    {
        public IntPtr dst;                    // Destination pointer
        public nuint pitch;                   // Pitch in bytes
        public uint value;                    // Value to set
        public uint elementSize;              // Element size (1, 2, or 4 bytes)
        public nuint width;                   // Width in elements
        public nuint height;                  // Height in elements
    }

    /// <summary>
    /// 3D position for CUDA operations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaPos
    {
        public nuint x;
        public nuint y;
        public nuint z;
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
    /// CUDA stream capture modes.
    /// </summary>
    public enum CudaStreamCaptureMode
    {
        Global = 0,
        ThreadLocal = 1,
        Relaxed = 2
    }

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