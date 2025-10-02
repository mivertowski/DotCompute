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
        ReuseFollowEventDependencies = 1,
        ReuseAllowOpportunistic = 2,
        ReuseAllowInternalDependencies = 3,
        ReleaseThreshold = 4,
        AttrReleaseThreshold = 4,
        AttrReservedMemCurrent = 5,
        AttrReservedMemHigh = 6,
        AttrUsedMemCurrent = 7,
        AttrUsedMemHigh = 8,
        // Additional commonly used attributes
        Used = AttrUsedMemCurrent,
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
        None = 0,
        Read = 1,
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
        Invalid = 0,
        Device = 1,
        Host = 2,
        HostNuma = 3,
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
        Invalid = 0,
        Pinned = 1,
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
        None = 0,
        PosixFileDescriptor = 1,
        Win32 = 2,
        Win32Kmt = 4,
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
        None = 0,
        Generic = 1
    }
}