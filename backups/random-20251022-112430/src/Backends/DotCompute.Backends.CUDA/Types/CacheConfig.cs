// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// An cache config enumeration.
    /// </summary>
    /// <summary>
    /// CUDA cache configuration.
    /// </summary>
    public enum CacheConfig
    {
        PreferNone,
        PreferShared,
        PreferL1,
        PreferEqual
    }
}