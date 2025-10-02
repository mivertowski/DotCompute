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
    /// CUDA kernel node parameters for graph API
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
    /// CUDA memory set parameters
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
    /// CUDA memory copy parameters for 3D operations
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
    /// 3D position for CUDA operations
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaPos
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
    }

    /// <summary>
    /// CUDA memory range attributes
    /// </summary>
    public struct CudaMemRangeAttribute
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
    }
}