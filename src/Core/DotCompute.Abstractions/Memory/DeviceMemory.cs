// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Represents a reference to device memory that can be accessed as a span.
/// </summary>
/// <typeparam name="T">The unmanaged type stored in memory.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct DeviceMemory<T> where T : unmanaged
{
    private readonly T* _pointer;
    private readonly int _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceMemory{T}"/> struct.
    /// </summary>
    /// <param name="pointer">Pointer to the device memory.</param>
    /// <param name="length">Length in elements.</param>
    public DeviceMemory(T* pointer, int length)
    {
        _pointer = pointer;
        _length = length;
    }

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
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || start + length > _length)
            throw new ArgumentOutOfRangeException(nameof(length));

        return new DeviceMemory<T>(_pointer + start, length);
    }
}