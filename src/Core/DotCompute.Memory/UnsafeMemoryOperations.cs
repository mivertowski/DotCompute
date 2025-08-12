// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Memory;

/// <summary>
/// Provides high-performance unsafe memory operations with platform-specific optimizations.
/// Includes zero-copy operations, proper memory alignment, and SIMD optimizations.
/// </summary>
public static unsafe class UnsafeMemoryOperations
{
    /// <summary>
    /// The default memory alignment for SIMD operations.
    /// </summary>
    public const int DefaultAlignment = 32; // 256-bit alignment for AVX2

    /// <summary>
    /// Copies memory from source to destination with optimal performance.
    /// Uses vectorized operations when possible.
    /// </summary>
    /// <param name="source">The source memory pointer.</param>
    /// <param name="destination">The destination memory pointer.</param>
    /// <param name="byteCount">The number of bytes to copy.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyMemory(void* source, void* destination, nuint byteCount)
    {
        if (byteCount == 0)
        {
            return;
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        // Use platform-specific optimized copy
        if (Avx2.IsSupported && byteCount >= 32)
        {
            CopyMemoryAvx2(source, destination, byteCount);
        }
        else if (Sse2.IsSupported && byteCount >= 16)
        {
            CopyMemorySse2(source, destination, byteCount);
        }
        else
        {
            CopyMemoryScalar(source, destination, byteCount);
        }
    }

    /// <summary>
    /// Copies memory from source to destination using generic types.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source span.</param>
    /// <param name="destination">The destination span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyMemory<T>(ReadOnlySpan<T> source, Span<T> destination) where T : unmanaged
    {
        if (source.Length != destination.Length)
        {
            throw new ArgumentException("Source and destination must have the same length.");
        }

        if (source.Length == 0)
        {
            return;
        }

        var byteCount = (nuint)(source.Length * sizeof(T));

        fixed (T* src = source)
        fixed (T* dst = destination)
        {
            CopyMemory(src, dst, byteCount);
        }
    }

    /// <summary>
    /// Fills memory with a specified value using vectorized operations.
    /// </summary>
    /// <param name="destination">The destination memory pointer.</param>
    /// <param name="value">The value to fill with.</param>
    /// <param name="byteCount">The number of bytes to fill.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillMemory(void* destination, byte value, nuint byteCount)
    {
        if (byteCount == 0)
        {
            return;
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        // Use platform-specific optimized fill
        if (Avx2.IsSupported && byteCount >= 32)
        {
            FillMemoryAvx2(destination, value, byteCount);
        }
        else if (Sse2.IsSupported && byteCount >= 16)
        {
            FillMemorySse2(destination, value, byteCount);
        }
        else
        {
            FillMemoryScalar(destination, value, byteCount);
        }
    }

    /// <summary>
    /// Fills memory with a specified value using generic types.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="destination">The destination span.</param>
    /// <param name="value">The value to fill with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillMemory<T>(Span<T> destination, T value) where T : unmanaged
    {
        if (destination.Length == 0)
        {
            return;
        }

        // For simple types, use vectorized operations
        if (typeof(T) == typeof(byte))
        {
            var byteValue = Unsafe.As<T, byte>(ref value);
            fixed (T* dst = destination)
            {
                FillMemory(dst, byteValue, (nuint)(destination.Length * sizeof(T)));
            }
        }
        else if (typeof(T) == typeof(int) && Avx2.IsSupported)
        {
            FillMemoryInt32Avx2(destination, Unsafe.As<T, int>(ref value));
        }
        else if (typeof(T) == typeof(float) && Avx.IsSupported)
        {
            FillMemoryFloatAvx(destination, Unsafe.As<T, float>(ref value));
        }
        else
        {
            // Fallback to scalar fill
            destination.Fill(value);
        }
    }

    /// <summary>
    /// Zeros out memory using vectorized operations.
    /// </summary>
    /// <param name="destination">The destination memory pointer.</param>
    /// <param name="byteCount">The number of bytes to zero.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ZeroMemory(void* destination, nuint byteCount) => FillMemory(destination, 0, byteCount);

    /// <summary>
    /// Zeros out memory using generic types.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="destination">The destination span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ZeroMemory<T>(Span<T> destination) where T : unmanaged
    {
        if (destination.Length == 0)
        {
            return;
        }

        fixed (T* dst = destination)
        {
            ZeroMemory(dst, (nuint)(destination.Length * sizeof(T)));
        }
    }

    /// <summary>
    /// Checks if a memory address is aligned to the specified boundary.
    /// </summary>
    /// <param name="address">The memory address.</param>
    /// <param name="alignment">The alignment boundary.</param>
    /// <returns>True if the address is aligned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(void* address, int alignment) => ((nuint)address & (nuint)(alignment - 1)) == 0;

    /// <summary>
    /// Aligns a memory address to the specified boundary.
    /// </summary>
    /// <param name="address">The memory address.</param>
    /// <param name="alignment">The alignment boundary.</param>
    /// <returns>The aligned address.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* AlignAddress(void* address, int alignment)
    {
        var mask = (nuint)(alignment - 1);
        return (void*)(((nuint)address + mask) & ~mask);
    }

    /// <summary>
    /// Calculates the padding needed to align an address.
    /// </summary>
    /// <param name="address">The memory address.</param>
    /// <param name="alignment">The alignment boundary.</param>
    /// <returns>The padding needed in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculatePadding(void* address, int alignment)
    {
        var mask = (nuint)(alignment - 1);
        var aligned = ((nuint)address + mask) & ~mask;
        return (int)(aligned - (nuint)address);
    }

    #region Platform-Specific Implementations

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMemoryAvx2(void* source, void* destination, nuint byteCount)
    {
        var src = (byte*)source;
        var dst = (byte*)destination;
        var remaining = byteCount;

        // Process 32-byte chunks with AVX2
        while (remaining >= 32)
        {
            var vector = Avx.LoadVector256(src);
            Avx.Store(dst, vector);
            src += 32;
            dst += 32;
            remaining -= 32;
        }

        // Process remaining bytes
        if (remaining > 0)
        {
            CopyMemoryScalar(src, dst, remaining);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMemorySse2(void* source, void* destination, nuint byteCount)
    {
        var src = (byte*)source;
        var dst = (byte*)destination;
        var remaining = byteCount;

        // Process 16-byte chunks with SSE2
        while (remaining >= 16)
        {
            var vector = Sse2.LoadVector128(src);
            Sse2.Store(dst, vector);
            src += 16;
            dst += 16;
            remaining -= 16;
        }

        // Process remaining bytes
        if (remaining > 0)
        {
            CopyMemoryScalar(src, dst, remaining);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyMemoryScalar(void* source, void* destination, nuint byteCount)
    {
        var src = (byte*)source;
        var dst = (byte*)destination;
        var remaining = byteCount;

        // Process 8-byte chunks
        while (remaining >= 8)
        {
            *(ulong*)dst = *(ulong*)src;
            src += 8;
            dst += 8;
            remaining -= 8;
        }

        // Process 4-byte chunks
        while (remaining >= 4)
        {
            *(uint*)dst = *(uint*)src;
            src += 4;
            dst += 4;
            remaining -= 4;
        }

        // Process remaining bytes
        while (remaining > 0)
        {
            *dst = *src;
            src++;
            dst++;
            remaining--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillMemoryAvx2(void* destination, byte value, nuint byteCount)
    {
        var dst = (byte*)destination;
        var remaining = byteCount;
        var vector = Vector256.Create(value);

        // Process 32-byte chunks with AVX2
        while (remaining >= 32)
        {
            Avx.Store(dst, vector);
            dst += 32;
            remaining -= 32;
        }

        // Process remaining bytes
        if (remaining > 0)
        {
            FillMemoryScalar(dst, value, remaining);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillMemorySse2(void* destination, byte value, nuint byteCount)
    {
        var dst = (byte*)destination;
        var remaining = byteCount;
        var vector = Vector128.Create(value);

        // Process 16-byte chunks with SSE2
        while (remaining >= 16)
        {
            Sse2.Store(dst, vector);
            dst += 16;
            remaining -= 16;
        }

        // Process remaining bytes
        if (remaining > 0)
        {
            FillMemoryScalar(dst, value, remaining);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillMemoryScalar(void* destination, byte value, nuint byteCount)
    {
        var dst = (byte*)destination;
        var remaining = byteCount;

        // Create patterns for efficient filling
        var pattern64 = ((ulong)value << 56) | ((ulong)value << 48) | ((ulong)value << 40) | ((ulong)value << 32) |
                       ((ulong)value << 24) | ((ulong)value << 16) | ((ulong)value << 8) | value;

        var pattern32 = ((uint)value << 24) | ((uint)value << 16) | ((uint)value << 8) | value;

        // Process 8-byte chunks
        while (remaining >= 8)
        {
            *(ulong*)dst = pattern64;
            dst += 8;
            remaining -= 8;
        }

        // Process 4-byte chunks
        while (remaining >= 4)
        {
            *(uint*)dst = pattern32;
            dst += 4;
            remaining -= 4;
        }

        // Process remaining bytes
        while (remaining > 0)
        {
            *dst = value;
            dst++;
            remaining--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillMemoryInt32Avx2<T>(Span<T> destination, int value) where T : unmanaged
    {
        var vector = Vector256.Create(value);
        var span = MemoryMarshal.Cast<T, int>(destination);

        fixed (int* dst = span)
        {
            var ptr = dst;
            var remaining = span.Length;

            // Process 8-int chunks with AVX2
            while (remaining >= 8)
            {
                Avx.Store(ptr, vector);
                ptr += 8;
                remaining -= 8;
            }

            // Process remaining elements
            while (remaining > 0)
            {
                *ptr = value;
                ptr++;
                remaining--;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillMemoryFloatAvx<T>(Span<T> destination, float value) where T : unmanaged
    {
        var vector = Vector256.Create(value);
        var span = MemoryMarshal.Cast<T, float>(destination);

        fixed (float* dst = span)
        {
            var ptr = dst;
            var remaining = span.Length;

            // Process 8-float chunks with AVX
            while (remaining >= 8)
            {
                Avx.Store(ptr, vector);
                ptr += 8;
                remaining -= 8;
            }

            // Process remaining elements
            while (remaining > 0)
            {
                *ptr = value;
                ptr++;
                remaining--;
            }
        }
    }

    #endregion
}
