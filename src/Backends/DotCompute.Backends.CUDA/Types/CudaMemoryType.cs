// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// An cuda memory type enumeration.
    /// </summary>
    /// <summary>
    /// CUDA memory type enumeration
    /// </summary>
    public enum CudaMemoryType
    {
        Device,
        Host,
        Unified,
        Pinned,
        Mapped
    }
}
