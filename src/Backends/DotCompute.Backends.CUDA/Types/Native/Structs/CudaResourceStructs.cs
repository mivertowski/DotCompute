// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Enums;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA resource descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaResourceDesc
    {
        public CudaResourceType resType;
        public nint array;
        public nint mipmap;
        public nint devPtr;
        public nuint sizeInBytes;
        public uint format;
        public uint numChannels;
        public nuint width;
        public nuint height;
        public nuint depth;
        public nuint pitchInBytes;
    }

    /// <summary>
    /// CUDA texture descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaTextureDesc
    {
        public CudaTextureAddressMode addressMode0;
        public CudaTextureAddressMode addressMode1;
        public CudaTextureAddressMode addressMode2;
        public CudaTextureFilterMode filterMode;
        public CudaTextureReadMode readMode;
        public int sRGB;
        public float borderColor0;
        public float borderColor1;
        public float borderColor2;
        public float borderColor3;
        public int normalizedCoords;
        public uint maxAnisotropy;
        public CudaTextureFilterMode mipmapFilterMode;
        public float mipmapLevelBias;
        public float minMipmapLevelClamp;
        public float maxMipmapLevelClamp;
        public int disableTrilinearOptimization;
        public int seamlessCubemap;
    }

    /// <summary>
    /// CUDA resource view descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaResourceViewDesc
    {
        public CudaResourceViewFormat format;
        public nuint width;
        public nuint height;
        public nuint depth;
        public uint firstMipmapLevel;
        public uint lastMipmapLevel;
        public uint firstLayer;
        public uint lastLayer;
    }
}