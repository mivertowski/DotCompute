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
        Wrap = 0,
        Clamp = 1,
        Mirror = 2,
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
        Point = 0,
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
        ElementType = 0,
        NormalizedFloat = 1
    }
}