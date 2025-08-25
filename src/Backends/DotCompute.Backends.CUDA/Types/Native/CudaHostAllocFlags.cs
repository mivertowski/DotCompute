// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Flags that control the allocation behavior of CUDA host memory.
    /// </summary>
    /// <remarks>
    /// These flags are used with cudaHostAlloc and related functions to specify
    /// special properties for host memory allocations. Multiple flags can be
    /// combined using bitwise OR operations.
    /// </remarks>
    [Flags]
    public enum CudaHostAllocFlags : uint
    {
        /// <summary>
        /// Default host allocation with no special properties.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Allocate pinned (page-locked) memory.
        /// Pinned memory provides faster transfers to/from GPU memory.
        /// </summary>
        Pinned = 1,

        /// <summary>
        /// Map the allocation into the device address space.
        /// The returned host pointer can be used directly by CUDA kernels.
        /// </summary>
        Mapped = 2,

        /// <summary>
        /// Allocate write-combined memory.
        /// This can improve transfer performance in some cases but may reduce CPU read performance.
        /// </summary>
        WriteCombined = 4,

        /// <summary>
        /// Make the allocation portable across CUDA contexts.
        /// This allows the memory to be used by multiple CUDA contexts.
        /// </summary>
        Portable = 8
    }
}