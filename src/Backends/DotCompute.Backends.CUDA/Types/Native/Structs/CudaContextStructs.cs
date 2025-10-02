// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Enums;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA context creation parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaContextCreateParams
    {
        /// <summary>
        /// The ctx.
        /// </summary>
        public nint ctx;
        /// <summary>
        /// The flags.
        /// </summary>
        public uint flags;
        /// <summary>
        /// The nv sci sync attr list.
        /// </summary>
        public nint nvSciSyncAttrList;
        /// <summary>
        /// The priority.
        /// </summary>
        public uint priority;
    }

    /// <summary>
    /// CUDA array format structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaArrayFormat
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
        /// <summary>
        /// The w.
        /// </summary>
        public uint w;
        /// <summary>
        /// The f.
        /// </summary>
        public CudaArrayFormatKind f;
    }

    /// <summary>
    /// CUDA extent structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExtent
    {
        /// <summary>
        /// The width.
        /// </summary>
        public nuint width;
        /// <summary>
        /// The height.
        /// </summary>
        public nuint height;
        /// <summary>
        /// The depth.
        /// </summary>
        public nuint depth;
    }
}