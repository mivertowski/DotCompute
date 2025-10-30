// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Memory;

/// <summary>
/// Helper methods and utilities for UnifiedBuffer operations.
/// </summary>
public static class UnifiedBufferHelpers
{
    /// <summary>
    /// Creates a unified buffer from an existing array.
    /// </summary>
    public static UnifiedBuffer<T> CreateFromArray<T>(T[] array, IUnifiedMemoryManager memoryManager) where T : unmanaged
    {
        var buffer = new UnifiedBuffer<T>(memoryManager, array.Length);
        buffer.CopyFromAsync(array.AsMemory()).AsTask().GetAwaiter().GetResult();
        return buffer;
    }

    /// <summary>
    /// Creates a unified buffer from a span.
    /// </summary>
    public static UnifiedBuffer<T> CreateFromSpan<T>(ReadOnlySpan<T> span, IUnifiedMemoryManager memoryManager) where T : unmanaged
    {
        var buffer = new UnifiedBuffer<T>(memoryManager, span.Length);
        var array = span.ToArray();
        buffer.CopyFromAsync(array.AsMemory()).AsTask().GetAwaiter().GetResult();
        return buffer;
    }

    /// <summary>
    /// Copies data between unified buffers.
    /// </summary>
    public static async Task CopyAsync<T>(
        UnifiedBuffer<T> source,
        int sourceOffset,
        UnifiedBuffer<T> destination,
        int destinationOffset,
        int length,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (sourceOffset < 0 || sourceOffset + length > (int)source.Length)
        {

            throw new ArgumentOutOfRangeException(nameof(sourceOffset));
        }


        if (destinationOffset < 0 || destinationOffset + length > (int)destination.Length)
        {

            throw new ArgumentOutOfRangeException(nameof(destinationOffset));
        }

        // Use device-to-device copy if both buffers are on the same accelerator

        if (source.Accelerator == destination.Accelerator && source.Accelerator != null)
        {
            await source.CopyToAsync(sourceOffset, destination, destinationOffset, length, cancellationToken);
        }
        else
        {
            // Fall back to CPU-mediated copy
            // Create temporary array for the slice
            var tempArray = new T[length];
            await source.CopyToAsync(tempArray.AsMemory(), cancellationToken);
            await destination.CopyFromAsync(tempArray.AsMemory(), cancellationToken);
        }
    }

    /// <summary>
    /// Fills a buffer with a specific value.
    /// </summary>
    public static async Task FillAsync<T>(
        UnifiedBuffer<T> buffer,
        T value,
        int offset = 0,
        int? length = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var fillLength = length ?? ((int)buffer.Length - offset);

        if (offset < 0 || offset + fillLength > (int)buffer.Length)
        {

            throw new ArgumentOutOfRangeException(nameof(offset));
        }


        await buffer.FillAsync(value, offset, fillLength, cancellationToken);
    }

    /// <summary>
    /// Compares two buffers for equality.
    /// </summary>
    public static async Task<bool> EqualsAsync<T>(
        UnifiedBuffer<T> first,
        UnifiedBuffer<T> second,
        CancellationToken cancellationToken = default) where T : unmanaged, IEquatable<T>
    {
        if (first.Length != second.Length)
        {

            return false;
        }


        // Copy buffers to arrays for comparison
        var firstArray = new T[first.Length];
        var secondArray = new T[second.Length];
        await first.CopyToAsync(firstArray.AsMemory(), cancellationToken);
        await second.CopyToAsync(secondArray.AsMemory(), cancellationToken);

        return firstArray.SequenceEqual(secondArray);
    }

    /// <summary>
    /// Creates a slice view of a buffer.
    /// </summary>
    public static UnifiedBufferSlice<T> CreateSlice<T>(
        UnifiedBuffer<T> buffer,
        int offset,
        int length) where T : unmanaged => new(buffer, offset, length);

    /// <summary>
    /// Creates a type view of a buffer.
    /// </summary>
    public static UnifiedBufferView<TOriginal, TNew> CreateView<TOriginal, TNew>(
        UnifiedBuffer<TOriginal> buffer)
        where TOriginal : unmanaged
        where TNew : unmanaged
    {
        var newLength = buffer.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>() / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
        return new UnifiedBufferView<TOriginal, TNew>(buffer, newLength);
    }
}