// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// CUDA resource type enumeration
    /// </summary>
    public enum CudaResourceType : uint
    {
        Array = 0,
        MipmappedArray = 1,
        Linear = 2,
        Pitch2D = 3
    }

    /// <summary>
    /// CUDA resource view format enumeration
    /// </summary>
    public enum CudaResourceViewFormat : uint
    {
        None = 0,
        Uint1X8 = 1,
        Uint1X16 = 2,
        Uint1X32 = 3,
        Uint2X8 = 4,
        Uint2X16 = 5,
        Uint2X32 = 6,
        Uint4X8 = 7,
        Uint4X16 = 8,
        Uint4X32 = 9,
        Sint1X8 = 10,
        Sint1X16 = 11,
        Sint1X32 = 12,
        Sint2X8 = 13,
        Sint2X16 = 14,
        Sint2X32 = 15,
        Sint4X8 = 16,
        Sint4X16 = 17,
        Sint4X32 = 18,
        Float1X16 = 19,
        Float1X32 = 20,
        Float2X16 = 21,
        Float2X32 = 22,
        Float4X16 = 23,
        Float4X32 = 24
    }
}