// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;

namespace DotCompute.Core.Memory.Utilities;

/// <summary>
/// Helper utilities for buffer operations.
/// </summary>
internal static class BufferHelpers
{
    /// <summary>
    /// Calculates aligned size for memory allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AlignSize(long size, int alignment = 256) => (size + alignment - 1) & ~(alignment - 1);


    /// <summary>
    /// Validates buffer parameters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateBufferParameters<T>(int count) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        
        var elementSize = Unsafe.SizeOf<T>();
        var totalSize = (long)count * elementSize;
        
        if (totalSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(count), 
                $"Buffer size ({totalSize} bytes) exceeds maximum allowed size.");
        }
    }


    /// <summary>
    /// Checks if a type is blittable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBlittable<T>() where T : unmanaged => true; // unmanaged constraint ensures blittability


    /// <summary>
    /// Gets the size of an element type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetElementSize<T>() where T : unmanaged => Unsafe.SizeOf<T>();


    /// <summary>
    /// Gets the element count from a buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetElementCount<T>(DotCompute.Abstractions.IUnifiedMemoryBuffer<T> buffer) where T : unmanaged =>
        // TODO: Implement proper element count retrieval from buffer
        // For now, assume buffer.SizeInBytes / sizeof(T)
        (int)(buffer.SizeInBytes / GetElementSize<T>());


    /// <summary>
    /// Validates transfer parameters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateTransferParameters<T>(int sourceLength, int sourceOffset, 
        int destinationLength, int destinationOffset, int count) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        
        if (sourceOffset + count > sourceLength)
        {
            throw new ArgumentOutOfRangeException(nameof(count), 
                "Source range exceeds buffer bounds.");
        }
        
        if (destinationOffset + count > destinationLength)
        {
            throw new ArgumentOutOfRangeException(nameof(count), 
                "Destination range exceeds buffer bounds.");
        }
    }
}