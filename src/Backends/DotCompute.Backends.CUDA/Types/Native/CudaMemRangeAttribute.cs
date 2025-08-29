// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Enumeration of memory range attributes that can be queried for unified memory allocations.
    /// </summary>
    /// <remarks>
    /// These attributes provide information about the current state and properties of
    /// unified memory ranges, allowing applications to query memory location preferences,
    /// access patterns, and other optimization hints.
    /// </remarks>
    public enum CudaMemRangeAttribute
    {
        /// <summary>
        /// The preferred location for the memory range.
        /// </summary>
        PreferredLocation = 1,

        /// <summary>
        /// The last prefetch location for the memory range.
        /// </summary>
        LastPrefetchLocation = 2,

        /// <summary>
        /// Whether the memory range is accessed mostly for reading.
        /// </summary>
        ReadMostly = 3,

        /// <summary>
        /// The devices that are accessing the memory range.
        /// </summary>
        AccessedBy = 4,

        /// <summary>
        /// The global memory offset for the memory range.
        /// </summary>
        GlobalOffset = 5,

        /// <summary>
        /// The activity state of the memory range.
        /// </summary>
        Activity = 6
    }
}