// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda resource type enumeration.
    /// </summary>
    /// <summary>
    /// CUDA resource type enumeration
    /// </summary>
    public enum CudaResourceType : uint
    {
        /// <summary>
        /// CUDA array resource.
        /// </summary>
        Array = 0,

        /// <summary>
        /// CUDA mipmapped array resource with multiple resolution levels.
        /// </summary>
        MipmappedArray = 1,

        /// <summary>
        /// Linear memory resource.
        /// </summary>
        Linear = 2,

        /// <summary>
        /// Pitched 2D memory resource.
        /// </summary>
        Pitch2D = 3
    }
    /// <summary>
    /// An cuda resource view format enumeration.
    /// </summary>

    /// <summary>
    /// CUDA resource view format enumeration
    /// </summary>
    public enum CudaResourceViewFormat : uint
    {
        /// <summary>
        /// No format specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// Single-channel unsigned 8-bit integer.
        /// </summary>
        Uint1X8 = 1,

        /// <summary>
        /// Single-channel unsigned 16-bit integer.
        /// </summary>
        Uint1X16 = 2,

        /// <summary>
        /// Single-channel unsigned 32-bit integer.
        /// </summary>
        Uint1X32 = 3,

        /// <summary>
        /// Two-channel unsigned 8-bit integer.
        /// </summary>
        Uint2X8 = 4,

        /// <summary>
        /// Two-channel unsigned 16-bit integer.
        /// </summary>
        Uint2X16 = 5,

        /// <summary>
        /// Two-channel unsigned 32-bit integer.
        /// </summary>
        Uint2X32 = 6,

        /// <summary>
        /// Four-channel unsigned 8-bit integer.
        /// </summary>
        Uint4X8 = 7,

        /// <summary>
        /// Four-channel unsigned 16-bit integer.
        /// </summary>
        Uint4X16 = 8,

        /// <summary>
        /// Four-channel unsigned 32-bit integer.
        /// </summary>
        Uint4X32 = 9,

        /// <summary>
        /// Single-channel signed 8-bit integer.
        /// </summary>
        Sint1X8 = 10,

        /// <summary>
        /// Single-channel signed 16-bit integer.
        /// </summary>
        Sint1X16 = 11,

        /// <summary>
        /// Single-channel signed 32-bit integer.
        /// </summary>
        Sint1X32 = 12,

        /// <summary>
        /// Two-channel signed 8-bit integer.
        /// </summary>
        Sint2X8 = 13,

        /// <summary>
        /// Two-channel signed 16-bit integer.
        /// </summary>
        Sint2X16 = 14,

        /// <summary>
        /// Two-channel signed 32-bit integer.
        /// </summary>
        Sint2X32 = 15,

        /// <summary>
        /// Four-channel signed 8-bit integer.
        /// </summary>
        Sint4X8 = 16,

        /// <summary>
        /// Four-channel signed 16-bit integer.
        /// </summary>
        Sint4X16 = 17,

        /// <summary>
        /// Four-channel signed 32-bit integer.
        /// </summary>
        Sint4X32 = 18,

        /// <summary>
        /// Single-channel 16-bit floating-point.
        /// </summary>
        Float1X16 = 19,

        /// <summary>
        /// Single-channel 32-bit floating-point.
        /// </summary>
        Float1X32 = 20,

        /// <summary>
        /// Two-channel 16-bit floating-point.
        /// </summary>
        Float2X16 = 21,

        /// <summary>
        /// Two-channel 32-bit floating-point.
        /// </summary>
        Float2X32 = 22,

        /// <summary>
        /// Four-channel 16-bit floating-point.
        /// </summary>
        Float4X16 = 23,

        /// <summary>
        /// Four-channel 32-bit floating-point.
        /// </summary>
        Float4X32 = 24
    }
}