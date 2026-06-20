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
    /// <remarks>
    /// Values MUST match the canonical <c>cudaHostAlloc*</c> constants from
    /// <c>cuda_runtime.h</c>; <c>cudaHostAlloc</c> rejects unknown bits with
    /// <c>cudaErrorInvalidValue</c>, so incorrect values cause allocation failure.
    /// </remarks>
    [Flags]
    public enum CudaHostAllocFlags : uint
    {
        /// <summary>
        /// Default host allocation (page-locked / pinned, no special properties).
        /// Corresponds to <c>cudaHostAllocDefault</c> (0x00).
        /// </summary>
        Default = 0x00,

        /// <summary>
        /// Make the allocation portable across CUDA contexts.
        /// This allows the memory to be used by multiple CUDA contexts.
        /// Corresponds to <c>cudaHostAllocPortable</c> (0x01).
        /// </summary>
        Portable = 0x01,

        /// <summary>
        /// Map the allocation into the device address space.
        /// The returned host pointer can be used directly by CUDA kernels.
        /// Corresponds to <c>cudaHostAllocMapped</c> (0x02).
        /// </summary>
        Mapped = 0x02,

        /// <summary>
        /// Allocate write-combined memory.
        /// This can improve transfer performance in some cases but may reduce CPU read performance.
        /// Corresponds to <c>cudaHostAllocWriteCombined</c> (0x04).
        /// </summary>
        WriteCombined = 0x04,

        /// <summary>
        /// Alias for <see cref="Default"/>; all <c>cudaHostAlloc</c> allocations are page-locked (pinned).
        /// </summary>
        Pinned = Default
    }
}
