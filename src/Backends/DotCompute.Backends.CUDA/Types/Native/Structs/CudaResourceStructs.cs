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
        /// <summary>
        /// The res type.
        /// </summary>
        public CudaResourceType resType;
        /// <summary>
        /// The array.
        /// </summary>
        public nint array;
        /// <summary>
        /// The mipmap.
        /// </summary>
        public nint mipmap;
        /// <summary>
        /// The dev ptr.
        /// </summary>
        public nint devPtr;
        /// <summary>
        /// The size in bytes.
        /// </summary>
        public nuint sizeInBytes;
        /// <summary>
        /// The format.
        /// </summary>
        public uint format;
        /// <summary>
        /// The num channels.
        /// </summary>
        public uint numChannels;
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
        /// <summary>
        /// The pitch in bytes.
        /// </summary>
        public nuint pitchInBytes;
    }

    /// <summary>
    /// CUDA texture descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaTextureDesc
    {
        /// <summary>
        /// The address mode0.
        /// </summary>
        public CudaTextureAddressMode addressMode0;
        /// <summary>
        /// The address mode1.
        /// </summary>
        public CudaTextureAddressMode addressMode1;
        /// <summary>
        /// The address mode2.
        /// </summary>
        public CudaTextureAddressMode addressMode2;
        /// <summary>
        /// The filter mode.
        /// </summary>
        public CudaTextureFilterMode filterMode;
        /// <summary>
        /// The read mode.
        /// </summary>
        public CudaTextureReadMode readMode;
        /// <summary>
        /// The s r g b.
        /// </summary>
        public int sRGB;
        /// <summary>
        /// The border color0.
        /// </summary>
        public float borderColor0;
        /// <summary>
        /// The border color1.
        /// </summary>
        public float borderColor1;
        /// <summary>
        /// The border color2.
        /// </summary>
        public float borderColor2;
        /// <summary>
        /// The border color3.
        /// </summary>
        public float borderColor3;
        /// <summary>
        /// The normalized coords.
        /// </summary>
        public int normalizedCoords;
        /// <summary>
        /// The max anisotropy.
        /// </summary>
        public uint maxAnisotropy;
        /// <summary>
        /// The mipmap filter mode.
        /// </summary>
        public CudaTextureFilterMode mipmapFilterMode;
        /// <summary>
        /// The mipmap level bias.
        /// </summary>
        public float mipmapLevelBias;
        /// <summary>
        /// The min mipmap level clamp.
        /// </summary>
        public float minMipmapLevelClamp;
        /// <summary>
        /// The max mipmap level clamp.
        /// </summary>
        public float maxMipmapLevelClamp;
        /// <summary>
        /// The disable trilinear optimization.
        /// </summary>
        public int disableTrilinearOptimization;
        /// <summary>
        /// The seamless cubemap.
        /// </summary>
        public int seamlessCubemap;
    }

    /// <summary>
    /// CUDA resource view descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaResourceViewDesc
    {
        /// <summary>
        /// The format.
        /// </summary>
        public CudaResourceViewFormat format;
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
        /// <summary>
        /// The first mipmap level.
        /// </summary>
        public uint firstMipmapLevel;
        /// <summary>
        /// The last mipmap level.
        /// </summary>
        public uint lastMipmapLevel;
        /// <summary>
        /// The first layer.
        /// </summary>
        public uint firstLayer;
        /// <summary>
        /// The last layer.
        /// </summary>
        public uint lastLayer;
    }
}