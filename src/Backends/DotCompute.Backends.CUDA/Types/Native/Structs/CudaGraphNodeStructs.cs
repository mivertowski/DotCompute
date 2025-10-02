// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA host node parameters for graph API
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaHostNodeParams
    {
        public CudaHostFn fn;
        public IntPtr userData;
    }

    /// <summary>
    /// CUDA kernel node parameters for graph API
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
    /// CUDA memory set parameters
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
    /// CUDA memory copy parameters for 3D operations
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
    /// 3D position for CUDA operations
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaPos
    {
        public nuint x;
        public nuint y;
        public nuint z;
    }

    /// <summary>
    /// CUDA memory range attributes
    /// </summary>
    public struct CudaMemRangeAttribute
    {
        public IntPtr Data;
        public nuint DataSize;
        public uint Attribute;
        public IntPtr DevPtr;
        public nuint Count;
    }
}