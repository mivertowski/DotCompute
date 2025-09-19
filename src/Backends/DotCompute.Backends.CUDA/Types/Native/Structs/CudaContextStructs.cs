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
        public nint ctx;
        public uint flags;
        public nint nvSciSyncAttrList;
        public uint priority;
    }

    /// <summary>
    /// CUDA array format structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaArrayFormat
    {
        public uint x;
        public uint y;
        public uint z;
        public uint w;
        public CudaArrayFormatKind f;
    }

    /// <summary>
    /// CUDA extent structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExtent
    {
        public nuint width;
        public nuint height;
        public nuint depth;
    }
}