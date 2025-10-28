// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types;

/// <summary>
/// Properties for CUDA memory pool configuration.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CudaMemPoolProps
{
    /// <summary>
    /// Gets or sets the allocation type.
    /// </summary>
    public CudaMemAllocationType AllocationType { get; set; }

    /// <summary>
    /// Gets or sets the handle type.
    /// </summary>
    public CudaMemHandleType HandleType { get; set; }

    /// <summary>
    /// Gets or sets the location.
    /// </summary>
    public CudaMemLocation Location { get; set; }

    /// <summary>
    /// Gets or sets the Windows security attributes (Windows only).
    /// </summary>
    public IntPtr WinSecurityAttributes { get; set; }

    /// <summary>
    /// Gets or sets the reserved space for future use.
    /// </summary>
    public ulong Reserved1 { get; set; }

    /// <summary>
    /// Gets or sets additional reserved space for future use.
    /// </summary>
    public ulong Reserved2 { get; set; }

    /// <summary>
    /// Gets or sets additional reserved space for future use.
    /// </summary>
    public ulong Reserved3 { get; set; }

    /// <summary>
    /// Gets or sets additional reserved space for future use.
    /// </summary>
    public ulong Reserved4 { get; set; }

    /// <summary>
    /// Gets or sets additional reserved space for future use.
    /// </summary>
    public ulong Reserved5 { get; set; }

    /// <summary>
    /// Gets or sets additional reserved space for future use.
    /// </summary>
    public ulong Reserved6 { get; set; }

    /// <summary>
    /// Gets or sets additional reserved space for future use.
    /// </summary>
    public ulong Reserved7 { get; set; }

    /// <summary>
    /// Gets or sets additional reserved space for future use.
    /// </summary>
    public ulong Reserved8 { get; set; }

    /// <summary>
    /// Creates default memory pool properties.
    /// </summary>
    /// <returns>Default properties.</returns>
    public static CudaMemPoolProps CreateDefault() => new()
    {
        AllocationType = CudaMemAllocationType.Pinned,
        HandleType = CudaMemHandleType.None,
        Location = new CudaMemLocation { Type = CudaMemLocationType.Device, Id = 0 },
        WinSecurityAttributes = IntPtr.Zero,
        Reserved1 = 0,
        Reserved2 = 0,
        Reserved3 = 0,
        Reserved4 = 0,
        Reserved5 = 0,
        Reserved6 = 0,
        Reserved7 = 0,
        Reserved8 = 0
    };
}

/// <summary>
/// CUDA memory allocation type.
/// </summary>
public enum CudaMemAllocationType
{
    /// <summary>
    /// Default allocation.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Pinned memory allocation.
    /// </summary>
    Pinned = 1,

    /// <summary>
    /// Maximum enumeration value.
    /// </summary>
    Max = 0x7FFFFFFF
}

/// <summary>
/// CUDA memory handle type.
/// </summary>
public enum CudaMemHandleType
{
    /// <summary>
    /// No handle.
    /// </summary>
    None = 0,

    /// <summary>
    /// POSIX file descriptor handle.
    /// </summary>
    PosixFileDescriptor = 1,

    /// <summary>
    /// Windows handle.
    /// </summary>
    Win32 = 2,

    /// <summary>
    /// Windows KMT handle.
    /// </summary>
    Win32Kmt = 4,

    /// <summary>
    /// Maximum enumeration value.
    /// </summary>
    Max = 0x7FFFFFFF
}

/// <summary>
/// CUDA memory location.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CudaMemLocation
{
    /// <summary>
    /// Gets or sets the location type.
    /// </summary>
    public CudaMemLocationType Type { get; set; }

    /// <summary>
    /// Gets or sets the device or CPU ID.
    /// </summary>
    public int Id { get; set; }
}

/// <summary>
/// CUDA memory location type.
/// </summary>
public enum CudaMemLocationType
{
    /// <summary>
    /// Invalid location.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// Device memory.
    /// </summary>
    Device = 1,

    /// <summary>
    /// Maximum enumeration value.
    /// </summary>
    Max = 0x7FFFFFFF
}

/// <summary>
/// CUDA memory pool attribute.
/// </summary>
public enum CudaMemPoolAttribute
{
    /// <summary>
    /// Whether pool can be reused.
    /// </summary>
    ReuseFollowEventDependencies = 1,

    /// <summary>
    /// Allow reuse if all dependencies are satisfied.
    /// </summary>
    ReuseAllowOpportunistic,

    /// <summary>
    /// Allow internal dependencies.
    /// </summary>
    ReuseAllowInternalDependencies,

    /// <summary>
    /// Release threshold.
    /// </summary>
    AttrReleaseThreshold,

    /// <summary>
    /// Reserved memory.
    /// </summary>
    AttrReservedMemCurrent,

    /// <summary>
    /// Reserved memory high watermark.
    /// </summary>
    AttrReservedMemHigh,

    /// <summary>
    /// Used memory.
    /// </summary>
    AttrUsedMemCurrent,

    /// <summary>
    /// Used memory high watermark.
    /// </summary>
    AttrUsedMemHigh
}