// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Enums;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA memory pool properties structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemPoolProps
    {
        public CudaMemAllocationType AllocType;
        public CudaMemAllocationHandleType HandleTypes;
        public CudaMemLocation Location;
        public nint win32SecurityAttributes;
        public nuint maxSize;
        public ushort usage;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 56)]
        public byte[] reserved;

        // Legacy properties for backward compatibility
        public CudaMemAllocationType allocType { get => AllocType; set => AllocType = value; }
        public CudaMemAllocationHandleType handleTypes { get => HandleTypes; set => HandleTypes = value; }
        public CudaMemLocation location { get => Location; set => Location = value; }
    }

    /// <summary>
    /// CUDA memory location structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemLocation
    {
        public CudaMemLocationType Type;
        public int Id;

        // Legacy properties for backward compatibility
        public CudaMemLocationType type { get => Type; set => Type = value; }
        public int id { get => Id; set => Id = value; }
    }

    /// <summary>
    /// CUDA memory access descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAccessDesc
    {
        public CudaMemLocation location;
        public CudaMemAccessFlags flags;
    }

    /// <summary>
    /// CUDA memory allocation properties structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAllocationProp
    {
        public CudaMemAllocationType type;
        public CudaMemAllocationHandleType requestedHandleTypes;
        public CudaMemLocation location;
        public nint win32HandleMetaData;
        public CudaMemAllocationCompType compressionType;
        public ushort usage;
    }

    /// <summary>
    /// CUDA memory allocation node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAllocNodeParams
    {
        public CudaMemPoolProps poolProps;
        public CudaMemAccessDesc accessDescs;
        public nuint accessDescCount;
        public nuint bytesize;
        public nint dptr;
    }
}