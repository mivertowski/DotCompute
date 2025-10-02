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
        /// <summary>
        /// The alloc type.
        /// </summary>
        public CudaMemAllocationType AllocType;
        /// <summary>
        /// The handle types.
        /// </summary>
        public CudaMemAllocationHandleType HandleTypes;
        /// <summary>
        /// The location.
        /// </summary>
        public CudaMemLocation Location;
        /// <summary>
        /// The win32 security attributes.
        /// </summary>
        public nint win32SecurityAttributes;
        /// <summary>
        /// The max size.
        /// </summary>
        public nuint maxSize;
        /// <summary>
        /// The usage.
        /// </summary>
        public ushort usage;
        /// <summary>
        /// The reserved.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 56)]
        public byte[] reserved;
        /// <summary>
        /// Gets or sets the alloc type.
        /// </summary>
        /// <value>The alloc type.</value>

        // Legacy properties for backward compatibility
        public CudaMemAllocationType allocType { get => AllocType; set => AllocType = value; }
        /// <summary>
        /// Gets or sets the handle types.
        /// </summary>
        /// <value>The handle types.</value>
        public CudaMemAllocationHandleType handleTypes { get => HandleTypes; set => HandleTypes = value; }
        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        /// <value>The location.</value>
        public CudaMemLocation location { get => Location; set => Location = value; }
    }

    /// <summary>
    /// CUDA memory location structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemLocation
    {
        /// <summary>
        /// The type.
        /// </summary>
        public CudaMemLocationType Type;
        /// <summary>
        /// The id.
        /// </summary>
        public int Id;
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>

        // Legacy properties for backward compatibility
        public CudaMemLocationType type { get => Type; set => Type = value; }
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public int id { get => Id; set => Id = value; }
    }

    /// <summary>
    /// CUDA memory access descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAccessDesc
    {
        /// <summary>
        /// The location.
        /// </summary>
        public CudaMemLocation location;
        /// <summary>
        /// The flags.
        /// </summary>
        public CudaMemAccessFlags flags;
    }

    /// <summary>
    /// CUDA memory allocation properties structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAllocationProp
    {
        /// <summary>
        /// The type.
        /// </summary>
        public CudaMemAllocationType type;
        /// <summary>
        /// The requested handle types.
        /// </summary>
        public CudaMemAllocationHandleType requestedHandleTypes;
        /// <summary>
        /// The location.
        /// </summary>
        public CudaMemLocation location;
        /// <summary>
        /// The win32 handle meta data.
        /// </summary>
        public nint win32HandleMetaData;
        /// <summary>
        /// The compression type.
        /// </summary>
        public CudaMemAllocationCompType compressionType;
        /// <summary>
        /// The usage.
        /// </summary>
        public ushort usage;
    }

    /// <summary>
    /// CUDA memory allocation node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAllocNodeParams
    {
        /// <summary>
        /// The pool props.
        /// </summary>
        public CudaMemPoolProps poolProps;
        /// <summary>
        /// The access descs.
        /// </summary>
        public CudaMemAccessDesc accessDescs;
        /// <summary>
        /// The access desc count.
        /// </summary>
        public nuint accessDescCount;
        /// <summary>
        /// The bytesize.
        /// </summary>
        public nuint bytesize;
        /// <summary>
        /// The dptr.
        /// </summary>
        public nint dptr;
    }
}