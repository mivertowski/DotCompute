// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Represents a handle to device memory.
/// This is a value type for AOT compatibility and zero allocations.
/// </summary>
public readonly struct DeviceMemory : IEquatable<DeviceMemory>
{
    /// <summary>
    /// Gets the device memory pointer.
    /// </summary>
    public IntPtr Handle { get; }

    /// <summary>
    /// Gets the size of the memory allocation in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Gets whether this is a valid device memory handle.
    /// </summary>
    public bool IsValid => Handle != IntPtr.Zero && Size > 0;

    /// <summary>
    /// Creates a new device memory handle.
    /// </summary>
    /// <param name="handle">The device memory pointer.</param>
    /// <param name="size">The size of the allocation in bytes.</param>
    public DeviceMemory(IntPtr handle, long size)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size cannot be negative.");
        }

        Handle = handle;
        Size = size;
    }

    /// <summary>
    /// Creates an invalid device memory handle.
    /// </summary>
    public static DeviceMemory Invalid => new(IntPtr.Zero, 0);

    public bool Equals(DeviceMemory other) => Handle == other.Handle && Size == other.Size;

    public override bool Equals(object? obj) => obj is DeviceMemory other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Handle, Size);

    public static bool operator ==(DeviceMemory left, DeviceMemory right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(DeviceMemory left, DeviceMemory right)
    {
        return !left.Equals(right);
    }

    public override string ToString() => IsValid ? $"DeviceMemory({Handle:X}, {Size} bytes)" : "DeviceMemory(Invalid)";
}
