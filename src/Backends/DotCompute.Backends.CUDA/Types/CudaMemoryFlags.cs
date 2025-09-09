// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA memory allocation flags and options
    /// </summary>
    [Flags]
    public enum CudaMemoryFlags : uint
    {
        None = 0,
        DeviceLocal = 1,
        HostVisible = 2,
        HostCoherent = 4,
        HostCached = 8,
        LazilyAllocated = 16,
        Protected = 32,
        Unified = 64,
        Pinned = 128
    }
}