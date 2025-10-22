// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types;

/// <summary>
/// Properties for CUDA memory pool configuration.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CudaMemPoolProps : IEquatable<CudaMemPoolProps>
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

    /// <summary>
    /// Determines whether this instance is equal to another <see cref="CudaMemPoolProps"/>.
    /// </summary>
    /// <param name="other">The other instance to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public readonly bool Equals(CudaMemPoolProps other)
    {
        return AllocationType == other.AllocationType
            && HandleType == other.HandleType
            && Location.Equals(other.Location)
            && WinSecurityAttributes == other.WinSecurityAttributes
            && Reserved1 == other.Reserved1
            && Reserved2 == other.Reserved2
            && Reserved3 == other.Reserved3
            && Reserved4 == other.Reserved4
            && Reserved5 == other.Reserved5
            && Reserved6 == other.Reserved6
            && Reserved7 == other.Reserved7
            && Reserved8 == other.Reserved8;
    }

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public readonly override bool Equals(object? obj)
    {
        return obj is CudaMemPoolProps other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code for this instance.
    /// </summary>
    /// <returns>The hash code.</returns>
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(
            AllocationType,
            HandleType,
            Location,
            WinSecurityAttributes,
            HashCode.Combine(Reserved1, Reserved2, Reserved3, Reserved4),
            HashCode.Combine(Reserved5, Reserved6, Reserved7, Reserved8));
    }

    /// <summary>
    /// Determines whether two <see cref="CudaMemPoolProps"/> instances are equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public static bool operator ==(CudaMemPoolProps left, CudaMemPoolProps right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="CudaMemPoolProps"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if not equal; otherwise, false.</returns>
    public static bool operator !=(CudaMemPoolProps left, CudaMemPoolProps right)
    {
        return !left.Equals(right);
    }
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
public struct CudaMemLocation : IEquatable<CudaMemLocation>
{
    /// <summary>
    /// Gets or sets the location type.
    /// </summary>
    public CudaMemLocationType Type { get; set; }

    /// <summary>
    /// Gets or sets the device or CPU ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Determines whether this instance is equal to another <see cref="CudaMemLocation"/>.
    /// </summary>
    /// <param name="other">The other instance to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public readonly bool Equals(CudaMemLocation other)
    {
        return Type == other.Type && Id == other.Id;
    }

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public readonly override bool Equals(object? obj)
    {
        return obj is CudaMemLocation other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code for this instance.
    /// </summary>
    /// <returns>The hash code.</returns>
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(Type, Id);
    }

    /// <summary>
    /// Determines whether two <see cref="CudaMemLocation"/> instances are equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public static bool operator ==(CudaMemLocation left, CudaMemLocation right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="CudaMemLocation"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if not equal; otherwise, false.</returns>
    public static bool operator !=(CudaMemLocation left, CudaMemLocation right)
    {
        return !left.Equals(right);
    }
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