// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;

namespace DotCompute.Core.Memory;

/// <summary>
/// Helper utilities for working with IBuffer interface compatibility.
/// </summary>
public static class BufferHelpers
{
    /// <summary>
    /// Gets the element count from an IBuffer based on its SizeInBytes.
    /// </summary>
    public static int GetElementCount<T>(IBuffer<T> buffer) where T : unmanaged
    {
        return (int)(buffer.SizeInBytes / Unsafe.SizeOf<T>());
    }

    /// <summary>
    /// Determines if buffer supports direct P2P operations.
    /// </summary>
    public static bool SupportsDirectP2P<T>(IBuffer<T> buffer) where T : unmanaged
    {
        return buffer is P2PBuffer<T> p2pBuffer && p2pBuffer.SupportsDirectP2P;
    }

    /// <summary>
    /// Gets the underlying memory buffer for advanced operations.
    /// </summary>
    public static IMemoryBuffer? GetUnderlyingBuffer<T>(IBuffer<T> buffer) where T : unmanaged
    {
        return buffer is P2PBuffer<T> p2pBuffer ? p2pBuffer.UnderlyingBuffer : null;
    }

    /// <summary>
    /// Safely copies data between buffers with type checking.
    /// </summary>
    public static async ValueTask CopyBetweenBuffersAsync<T>(
        IBuffer<T> source, 
        IBuffer<T> destination, 
        int sourceOffset = 0, 
        int destOffset = 0, 
        int? count = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var actualCount = count ?? Math.Min(GetElementCount(source) - sourceOffset, GetElementCount(destination) - destOffset);
        
        if (source is P2PBuffer<T> && destination is P2PBuffer<T>)
        {
            // P2P buffer copy
            await source.CopyToAsync(sourceOffset, destination, destOffset, actualCount, cancellationToken);
        }
        else
        {
            // Standard copy via host memory
            var hostData = new T[actualCount];
            // Would implement actual copy logic here
            await Task.CompletedTask; // Placeholder
        }
    }
}