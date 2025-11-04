// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;

namespace DotCompute.Abstractions.Extensions;

/// <summary>
/// Extension methods for <see cref="IUnifiedMemoryBuffer{T}"/> providing convenient read/write operations.
/// </summary>
public static class UnifiedMemoryBufferExtensions
{
    /// <summary>
    /// Writes data from a source memory to the buffer (convenience wrapper for <see cref="IUnifiedMemoryBuffer{T}.CopyFromAsync"/>).
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="source">The source data to write.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when source length exceeds buffer capacity.</exception>
    /// <remarks>
    /// This is a convenience method that calls <see cref="IUnifiedMemoryBuffer{T}.CopyFromAsync"/> internally.
    /// Use this when you want clearer intent in code ("write to buffer" vs "copy from source").
    /// </remarks>
    /// <example>
    /// <code>
    /// var data = new float[] { 1.0f, 2.0f, 3.0f };
    /// await buffer.WriteAsync(data.AsMemory());
    /// </code>
    /// </example>
    public static ValueTask WriteAsync<T>(
        this IUnifiedMemoryBuffer<T> buffer,
        ReadOnlyMemory<T> source,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

        if (source.Length > buffer.Length)
        {
            throw new ArgumentException(
                $"Source length ({source.Length}) exceeds buffer capacity ({buffer.Length})",
                nameof(source));
        }

        return buffer.CopyFromAsync(source, cancellationToken);
    }

    /// <summary>
    /// Writes a single value to the buffer at the specified index.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="index">The index to write at.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    /// <remarks>
    /// This method ensures the buffer is on the host, writes the single value, and marks the buffer as dirty.
    /// </remarks>
    public static async ValueTask WriteAsync<T>(
        this IUnifiedMemoryBuffer<T> buffer,
        int index,
        T value,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, buffer.Length, nameof(index));

        await buffer.EnsureOnHostAsync(default, cancellationToken);
        var span = buffer.AsSpan();
        span[index] = value;
        buffer.MarkHostDirty();
    }

    /// <summary>
    /// Reads all data from the buffer into a new array (convenience wrapper for CopyToAsync).
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that returns an array containing the buffer's data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <remarks>
    /// This is a convenience method that allocates a new array and calls CopyToAsync internally.
    /// Use this when you want clearer intent in code ("read from buffer" vs "copy to destination").
    /// For better performance with pre-allocated arrays, use <see cref="ReadAsync{T}(IUnifiedMemoryBuffer{T}, Memory{T}, CancellationToken)"/> instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// var data = await buffer.ReadAsync();
    /// Console.WriteLine($"Read {data.Length} elements");
    /// </code>
    /// </example>
    public static async ValueTask<T[]> ReadAsync<T>(
        this IUnifiedMemoryBuffer<T> buffer,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

        var result = new T[buffer.Length];
        await buffer.CopyToAsync(result.AsMemory(), cancellationToken);
        return result;
    }

    /// <summary>
    /// Reads data from the buffer into a pre-allocated destination memory.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="destination">The destination memory to read into.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous read operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when destination is smaller than buffer length.</exception>
    /// <remarks>
    /// This overload is more efficient than <see cref="ReadAsync{T}(IUnifiedMemoryBuffer{T}, CancellationToken)"/>
    /// when you have a pre-allocated destination array, as it avoids an additional allocation.
    /// </remarks>
    /// <example>
    /// <code>
    /// var data = new float[buffer.Length];
    /// await buffer.ReadAsync(data.AsMemory());
    /// </code>
    /// </example>
    public static ValueTask ReadAsync<T>(
        this IUnifiedMemoryBuffer<T> buffer,
        Memory<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

        if (destination.Length < buffer.Length)
        {
            throw new ArgumentException(
                $"Destination length ({destination.Length}) is smaller than buffer length ({buffer.Length})",
                nameof(destination));
        }

        return buffer.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// Reads a single value from the buffer at the specified index.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="index">The index to read from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that returns the value at the specified index.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    /// <remarks>
    /// This method ensures the buffer is on the host and reads the single value.
    /// </remarks>
    public static async ValueTask<T> ReadAsync<T>(
        this IUnifiedMemoryBuffer<T> buffer,
        int index,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, buffer.Length, nameof(index));

        await buffer.EnsureOnHostAsync(default, cancellationToken);
        return buffer.AsReadOnlySpan()[index];
    }

    /// <summary>
    /// Reads a portion of the buffer into a new array.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="offset">The offset to start reading from.</param>
    /// <param name="count">The number of elements to read.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that returns an array containing the requested portion of the buffer's data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when offset or count is out of range.</exception>
    public static async ValueTask<T[]> ReadAsync<T>(
        this IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
        ArgumentOutOfRangeException.ThrowIfNegative(offset, nameof(offset));
        ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(count));

        if (offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                $"Offset ({offset}) + count ({count}) exceeds buffer length ({buffer.Length})");
        }

        var slice = buffer.Slice(offset, count);
        var result = new T[count];
        await slice.CopyToAsync(result.AsMemory(), cancellationToken);
        return result;
    }
}
