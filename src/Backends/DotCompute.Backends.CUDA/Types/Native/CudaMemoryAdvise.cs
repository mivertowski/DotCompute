// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Enumeration of memory advice hints that can be applied to unified memory allocations.
    /// </summary>
    /// <remarks>
    /// These advice hints help optimize the performance of unified memory by providing
    /// information about how the memory will be accessed. The CUDA runtime can use
    /// these hints to make better decisions about memory placement and migration.
    /// </remarks>
    public enum CudaMemoryAdvise
    {
        /// <summary>
        /// Remove all memory advice for the memory region.
        /// </summary>
        Unset = 0,

        /// <summary>
        /// Advise that the memory region will be read frequently and should be kept close to the processor.
        /// This can help reduce page faults and improve read performance.
        /// </summary>
        SetReadMostly = 1,

        /// <summary>
        /// Remove the read-mostly advice for the memory region.
        /// This reverts the memory to normal unified memory behavior.
        /// </summary>
        UnsetReadMostly = 2,

        /// <summary>
        /// Advise that the memory region should be located close to the specified device.
        /// This can help reduce memory access latency for that device.
        /// </summary>
        SetPreferredLocation = 3,

        /// <summary>
        /// Remove the preferred location advice for the memory region.
        /// This allows CUDA to freely migrate the memory as needed.
        /// </summary>
        UnsetPreferredLocation = 4,

        /// <summary>
        /// Advise that the memory region will be accessed by the specified device.
        /// This helps CUDA optimize memory placement and migration policies.
        /// </summary>
        SetAccessedBy = 5,

        /// <summary>
        /// Remove the accessed-by advice for the specified device.
        /// This indicates that the device will no longer access the memory region.
        /// </summary>
        UnsetAccessedBy = 6
    }
}