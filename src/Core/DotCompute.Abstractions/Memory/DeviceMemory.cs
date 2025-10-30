// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Represents a reference to device memory that can be accessed as a span.
/// </summary>
/// <typeparam name="T">The unmanaged type stored in memory.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="DeviceMemory{T}"/> struct.
/// </remarks>
/// <param name="devicePtr">Pointer to the device memory.</param>
/// <param name="length">Length in elements.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct DeviceMemory<T>(T* devicePtr, int length) : IEquatable<DeviceMemory<T>> where T : unmanaged
{
    private readonly T* _pointer = devicePtr;
    private readonly int _length = length;

    /// <summary>
    /// Gets the length of the memory region in elements.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets whether this memory reference is empty.
    /// </summary>
    public bool IsEmpty => _length == 0 || _pointer == null;

    /// <summary>
    /// Gets a span representing the device memory.
    /// WARNING: This should only be used when device memory is accessible from host.
    /// </summary>
    public Span<T> Span => new(_pointer, _length);

    /// <summary>
    /// Gets a read-only span representing the device memory.
    /// WARNING: This should only be used when device memory is accessible from host.
    /// </summary>
    public ReadOnlySpan<T> ReadOnlySpan => new(_pointer, _length);

    /// <summary>
    /// Gets the raw pointer to device memory.
    /// </summary>
    public T* Pointer => _pointer;

    /// <summary>
    /// Creates a slice of this device memory.
    /// </summary>
    /// <param name="start">Starting index.</param>
    /// <param name="length">Length of slice.</param>
    /// <returns>A new DeviceMemory representing the slice.</returns>
    public DeviceMemory<T> Slice(int start, int length)
    {
        if (start < 0 || start >= _length)
        {

            throw new ArgumentOutOfRangeException(nameof(start));
        }


        if (length < 0 || start + length > _length)
        {

            throw new ArgumentOutOfRangeException(nameof(length));
        }


        return new DeviceMemory<T>(_pointer + start, length);
    }

    /// <summary>
    /// Determines whether the current instance is equal to another DeviceMemory instance.
    /// </summary>
    /// <param name="other">The DeviceMemory instance to compare with this instance.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public bool Equals(DeviceMemory<T> other)
        => _pointer == other._pointer && _length == other._length;

    /// <summary>
    /// Determines whether the current instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>true if obj is a DeviceMemory and is equal to this instance; otherwise, false.</returns>
    public override bool Equals(object? obj)
        => obj is DeviceMemory<T> other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current instance.</returns>
    public override int GetHashCode()
        => HashCode.Combine((IntPtr)_pointer, _length);

    /// <summary>
    /// Determines whether two DeviceMemory instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(DeviceMemory<T> left, DeviceMemory<T> right)
        => left.Equals(right);

    /// <summary>
    /// Determines whether two DeviceMemory instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(DeviceMemory<T> left, DeviceMemory<T> right)
        => !left.Equals(right);
}
