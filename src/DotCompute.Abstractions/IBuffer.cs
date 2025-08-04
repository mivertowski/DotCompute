// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Represents a generic buffer that can be used for accelerator computations.
/// This interface extends IMemoryBuffer with additional functionality.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public interface IBuffer<T> : IMemoryBuffer where T : unmanaged
{
    /// <summary>
    /// Gets the accelerator this buffer is associated with.
    /// </summary>
    public IAccelerator Accelerator { get; }

    /// <summary>
    /// Creates a slice of this buffer.
    /// </summary>
    /// <param name="offset">The offset in elements.</param>
    /// <param name="length">The length of the slice in elements.</param>
    /// <returns>A slice of this buffer.</returns>
    public IBuffer<T> Slice(int offset, int length);

    /// <summary>
    /// Creates a view of this buffer with a different element type.
    /// </summary>
    /// <typeparam name="TNew">The new element type.</typeparam>
    /// <returns>A view of this buffer as the new type.</returns>
    public IBuffer<TNew> AsType<TNew>() where TNew : unmanaged;

    /// <summary>
    /// Copies data from this buffer to another buffer.
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    public ValueTask CopyToAsync(IBuffer<T> destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies data from this buffer to another buffer with specified ranges.
    /// </summary>
    /// <param name="sourceOffset">The offset in this buffer.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="destinationOffset">The offset in the destination buffer.</param>
    /// <param name="count">The number of elements to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    public ValueTask CopyToAsync(
        int sourceOffset,
        IBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills this buffer with a specified value.
    /// </summary>
    /// <param name="value">The value to fill with.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the fill operation.</returns>
    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills a portion of this buffer with a specified value.
    /// </summary>
    /// <param name="value">The value to fill with.</param>
    /// <param name="offset">The offset to start filling at.</param>
    /// <param name="count">The number of elements to fill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the fill operation.</returns>
    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps this buffer to host memory for direct access.
    /// </summary>
    /// <param name="mode">The mapping mode.</param>
    /// <returns>A mapped memory region.</returns>
    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite);

    /// <summary>
    /// Maps a portion of this buffer to host memory for direct access.
    /// </summary>
    /// <param name="offset">The offset to start mapping at.</param>
    /// <param name="length">The number of elements to map.</param>
    /// <param name="mode">The mapping mode.</param>
    /// <returns>A mapped memory region.</returns>
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite);

    /// <summary>
    /// Asynchronously maps this buffer to host memory.
    /// </summary>
    /// <param name="mode">The mapping mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that returns the mapped memory region.</returns>
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a mapped memory region.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public readonly struct MappedMemory<T> : IDisposable, IEquatable<MappedMemory<T>> where T : unmanaged
{
    private readonly IBuffer<T> _buffer;
    private readonly Memory<T> _memory;
    private readonly MapMode _mode;

    /// <summary>
    /// Gets the mapped memory.
    /// </summary>
    public Memory<T> Memory => _memory;

    /// <summary>
    /// Gets the mapping mode.
    /// </summary>
    public MapMode Mode => _mode;

    /// <summary>
    /// Gets a span to the mapped memory.
    /// </summary>
    public Span<T> Span => _memory.Span;

    internal MappedMemory(IBuffer<T> buffer, Memory<T> memory, MapMode mode)
    {
        _buffer = buffer;
        _memory = memory;
        _mode = mode;
    }

    /// <summary>
    /// Unmaps the memory region.
    /// </summary>
    public void Dispose()
    {
        // Buffer should handle unmapping
    }

    public override bool Equals(object? obj) => throw new NotImplementedException();

    public override int GetHashCode() => throw new NotImplementedException();

    public static bool operator ==(MappedMemory<T> left, MappedMemory<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MappedMemory<T> left, MappedMemory<T> right)
    {
        return !(left == right);
    }

    public bool Equals(MappedMemory<T> other) => throw new NotImplementedException();
}

/// <summary>
/// Specifies the mapping mode for buffer access.
/// </summary>
[Flags]
public enum MapMode
{
    /// <summary>
    /// Map for reading only.
    /// </summary>
    Read = 1,

    /// <summary>
    /// Map for writing only.
    /// </summary>
    Write = 2,

    /// <summary>
    /// Map for both reading and writing.
    /// </summary>
    ReadWrite = Read | Write,

    /// <summary>
    /// Discard the previous contents of the buffer.
    /// </summary>
    Discard = 4,

    /// <summary>
    /// Map without blocking (may return invalid mapping).
    /// </summary>
    NoWait = 8
}
