// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda mem pool attribute enumeration.
    /// </summary>
    /// <summary>
    /// CUDA memory pool attribute enumeration
    /// </summary>
    public enum CudaMemPoolAttribute : uint
    {
        /// <summary>
        /// Reuse memory following event dependencies for synchronization.
        /// </summary>
        ReuseFollowEventDependencies = 1,

        /// <summary>
        /// Allow opportunistic memory reuse when dependencies are not strict.
        /// </summary>
        ReuseAllowOpportunistic = 2,

        /// <summary>
        /// Allow reuse of memory based on internal dependency tracking.
        /// </summary>
        ReuseAllowInternalDependencies = 3,

        /// <summary>
        /// Threshold for releasing memory back to the system.
        /// </summary>
        ReleaseThreshold = 4,

        /// <summary>
        /// Attribute for configuring the release threshold.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1069:Enums values should not be duplicated",
            Justification = "NVIDIA CUDA API defines both ReleaseThreshold and AttrReleaseThreshold with value 4 for backward compatibility")]
        AttrReleaseThreshold = 4,

        /// <summary>
        /// Current amount of reserved memory in the pool.
        /// </summary>
        AttrReservedMemCurrent = 5,

        /// <summary>
        /// High watermark of reserved memory in the pool.
        /// </summary>
        AttrReservedMemHigh = 6,

        /// <summary>
        /// Current amount of used memory in the pool.
        /// </summary>
        AttrUsedMemCurrent = 7,

        /// <summary>
        /// High watermark of used memory in the pool.
        /// </summary>
        AttrUsedMemHigh = 8,

        /// <summary>
        /// Alias for current used memory attribute.
        /// </summary>
        Used = AttrUsedMemCurrent,

        /// <summary>
        /// Alias for current reserved memory attribute.
        /// </summary>
        Reserved = AttrReservedMemCurrent
    }
    /// <summary>
    /// An cuda mem access flags enumeration.
    /// </summary>

    /// <summary>
    /// CUDA memory access flags enumeration
    /// </summary>
    public enum CudaMemAccessFlags : uint
    {
        /// <summary>
        /// No memory access permissions.
        /// </summary>
        None = 0,

        /// <summary>
        /// Read-only memory access permission.
        /// </summary>
        Read = 1,

        /// <summary>
        /// Read and write memory access permissions.
        /// </summary>
        ReadWrite = 3
    }
    /// <summary>
    /// An cuda mem location type enumeration.
    /// </summary>

    /// <summary>
    /// CUDA memory location type enumeration
    /// </summary>
    public enum CudaMemLocationType : uint
    {
        /// <summary>
        /// Invalid or unspecified memory location.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// Memory located on a CUDA device (GPU).
        /// </summary>
        Device = 1,

        /// <summary>
        /// Memory located on the host (CPU).
        /// </summary>
        Host = 2,

        /// <summary>
        /// Memory located on a specific NUMA node on the host.
        /// </summary>
        HostNuma = 3,

        /// <summary>
        /// Memory located on the current NUMA node of the calling thread.
        /// </summary>
        HostNumaCurrent = 4
    }
    /// <summary>
    /// An cuda mem allocation type enumeration.
    /// </summary>

    /// <summary>
    /// CUDA memory allocation type enumeration
    /// </summary>
    public enum CudaMemAllocationType : uint
    {
        /// <summary>
        /// Invalid or unspecified allocation type.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// Pinned (page-locked) memory allocation for faster host-device transfers.
        /// </summary>
        Pinned = 1,

        /// <summary>
        /// Maximum value for allocation type enumeration.
        /// </summary>
        Max = 0xFFFFFFFF
    }
    /// <summary>
    /// An cuda mem allocation handle type enumeration.
    /// </summary>

    /// <summary>
    /// CUDA memory allocation handle type enumeration
    /// </summary>
    public enum CudaMemAllocationHandleType : uint
    {
        /// <summary>
        /// No handle type specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// POSIX file descriptor handle for inter-process memory sharing on Linux.
        /// </summary>
        PosixFileDescriptor = 1,

        /// <summary>
        /// Windows handle for inter-process memory sharing.
        /// </summary>
        Win32 = 2,

        /// <summary>
        /// Windows Kernel Mode handle for inter-process memory sharing.
        /// </summary>
        Win32Kmt = 4,

        /// <summary>
        /// Maximum value for handle type enumeration.
        /// </summary>
        Max = 0xFFFFFFFF
    }
    /// <summary>
    /// An cuda mem allocation comp type enumeration.
    /// </summary>

    /// <summary>
    /// CUDA memory allocation compression type enumeration
    /// </summary>
    public enum CudaMemAllocationCompType : byte
    {
        /// <summary>
        /// No compression applied to memory allocation.
        /// </summary>
        None = 0,

        /// <summary>
        /// Generic compression algorithm applied to memory allocation.
        /// </summary>
        Generic = 1
    }
}