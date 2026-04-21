// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Backends.CPU.Extensions;

/// <summary>
/// Extension methods for CpuMemoryBuffer to provide generic access methods
/// required by the memory manager and backward compatibility.
/// </summary>
public static class CpuMemoryBufferExtensions
{
    /// <summary>
    /// Gets a typed span from a CpuMemoryBuffer by casting the underlying byte span.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The buffer to get the span from.</param>
    /// <returns>A span of the specified type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the buffer size is not compatible with the element type.</exception>
    public static Span<T> GetSpan<T>(this CpuMemoryBuffer buffer) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var byteSpan = buffer.AsSpan();

        unsafe
        {
            var elementSize = sizeof(T);
            if (byteSpan.Length % elementSize != 0)
            {
                throw new ArgumentException($"Buffer size {byteSpan.Length} is not compatible with element size {elementSize}");
            }
        }

        return MemoryMarshal.Cast<byte, T>(byteSpan);
    }

    /// <summary>
    /// Gets a typed read-only span from a CpuMemoryBuffer by casting the underlying byte span.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The buffer to get the span from.</param>
    /// <returns>A read-only span of the specified type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the buffer size is not compatible with the element type.</exception>
    public static ReadOnlySpan<T> GetReadOnlySpan<T>(this CpuMemoryBuffer buffer) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var byteSpan = buffer.AsReadOnlySpan();

        unsafe
        {
            var elementSize = sizeof(T);
            if (byteSpan.Length % elementSize != 0)
            {
                throw new ArgumentException($"Buffer size {byteSpan.Length} is not compatible with element size {elementSize}");
            }
        }

        return MemoryMarshal.Cast<byte, T>(byteSpan);
    }
}
