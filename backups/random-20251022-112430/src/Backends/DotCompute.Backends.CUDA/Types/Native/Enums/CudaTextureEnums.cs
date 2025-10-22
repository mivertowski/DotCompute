// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda texture address mode enumeration.
    /// </summary>
    /// <summary>
    /// CUDA texture address mode enumeration
    /// </summary>
    public enum CudaTextureAddressMode : uint
    {
        /// <summary>
        /// Wrap addressing mode - coordinates are wrapped to the valid range.
        /// </summary>
        Wrap = 0,

        /// <summary>
        /// Clamp addressing mode - coordinates are clamped to the edge of the texture.
        /// </summary>
        Clamp = 1,

        /// <summary>
        /// Mirror addressing mode - coordinates are mirrored at the texture edges.
        /// </summary>
        Mirror = 2,

        /// <summary>
        /// Border addressing mode - coordinates outside the texture return a border color.
        /// </summary>
        Border = 3
    }
    /// <summary>
    /// An cuda texture filter mode enumeration.
    /// </summary>

    /// <summary>
    /// CUDA texture filter mode enumeration
    /// </summary>
    public enum CudaTextureFilterMode : uint
    {
        /// <summary>
        /// Point (nearest neighbor) filtering - returns value of nearest texel.
        /// </summary>
        Point = 0,

        /// <summary>
        /// Linear filtering - performs linear interpolation between texels.
        /// </summary>
        Linear = 1
    }
    /// <summary>
    /// An cuda texture read mode enumeration.
    /// </summary>

    /// <summary>
    /// CUDA texture read mode enumeration
    /// </summary>
    public enum CudaTextureReadMode : uint
    {
        /// <summary>
        /// Read texture data in its element type without normalization.
        /// </summary>
        ElementType = 0,

        /// <summary>
        /// Read texture data as normalized floating-point values (0.0 to 1.0 for unsigned, -1.0 to 1.0 for signed).
        /// </summary>
        NormalizedFloat = 1
    }
}