// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Buffers;
using System.IO.MemoryMappedFiles;

namespace DotCompute.Memory;

/// <summary>
/// High-performance zero-copy operations using Span&lt;T&gt; and Memory&lt;T&gt;:
/// - Memory-mapped file operations for large datasets
/// - Pinned memory operations with automatic cleanup
/// - Vectorized memory operations with SIMD acceleration
/// - Unsafe pointer operations for maximum performance
/// - Interop-friendly memory layouts for native code
/// - Buffer slicing and views without allocation
/// Target: Eliminate 95%+ of memory copies in hot paths
/// </summary>
public static class ZeroCopyOperations
{
    /// <summary>
    /// Creates a zero-copy slice of a span without bounds checking in release builds.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source span.</param>
    /// <param name="offset">Offset in elements.</param>
    /// <param name="length">Length in elements.</param>
    /// <returns>Sliced span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> UnsafeSlice<T>(this Span<T> source, int offset, int length) =>
#if DEBUG
        source.Slice(offset, length);
#else
        MemoryMarshal.CreateSpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(source), offset),
            length);
#endif



    /// <summary>
    /// Creates a zero-copy read-only slice without bounds checking in release builds.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source span.</param>
    /// <param name="offset">Offset in elements.</param>
    /// <param name="length">Length in elements.</param>
    /// <returns>Sliced read-only span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> UnsafeSlice<T>(this ReadOnlySpan<T> source, int offset, int length) =>
#if DEBUG
        source.Slice(offset, length);
#else
        MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(source), offset),
            length);
#endif




    /// <summary>
    /// Reinterprets a span as a different type without copying.
    /// </summary>
    /// <typeparam name="TFrom">Source type.</typeparam>
    /// <typeparam name="TTo">Target type.</typeparam>
    /// <param name="source">Source span.</param>
    /// <returns>Reinterpreted span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TTo> Cast<TFrom, TTo>(this Span<TFrom> source)

        where TFrom : unmanaged

        where TTo : unmanaged => MemoryMarshal.Cast<TFrom, TTo>(source);

    /// <summary>
    /// Reinterprets a read-only span as a different type without copying.
    /// </summary>
    /// <typeparam name="TFrom">Source type.</typeparam>
    /// <typeparam name="TTo">Target type.</typeparam>
    /// <param name="source">Source span.</param>
    /// <returns>Reinterpreted read-only span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<TTo> Cast<TFrom, TTo>(this ReadOnlySpan<TFrom> source)

        where TFrom : unmanaged

        where TTo : unmanaged => MemoryMarshal.Cast<TFrom, TTo>(source);

    /// <summary>
    /// Gets a reference to the first element of a span for unsafe operations.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="span">Source span.</param>
    /// <returns>Reference to the first element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetReference<T>(this Span<T> span) => ref MemoryMarshal.GetReference(span);

    /// <summary>
    /// Gets a read-only reference to the first element of a span.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="span">Source span.</param>
    /// <returns>Read-only reference to the first element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T GetReference<T>(this ReadOnlySpan<T> span) => ref MemoryMarshal.GetReference(span);

    /// <summary>
    /// Creates a span from a pointer and length with proper lifetime management.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="pointer">Pointer to the data.</param>
    /// <param name="length">Length in elements.</param>
    /// <returns>Span wrapping the pointer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<T> CreateSpan<T>(void* pointer, int length) where T : unmanaged => new(pointer, length);

    /// <summary>
    /// Creates a read-only span from a pointer and length.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="pointer">Pointer to the data.</param>
    /// <param name="length">Length in elements.</param>
    /// <returns>Read-only span wrapping the pointer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ReadOnlySpan<T> CreateReadOnlySpan<T>(void* pointer, int length) where T : unmanaged => new(pointer, length);

    /// <summary>
    /// Copies data between spans using the fastest available method.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source span.</param>
    /// <param name="destination">Destination span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FastCopy<T>(this ReadOnlySpan<T> source, Span<T> destination) where T : unmanaged
    {
        if (source.Length != destination.Length)
        {
            throw new ArgumentException("Source and destination spans must have the same length");
        }

        if (source.IsEmpty)
        {
            return;
        }

        // Use vectorized copy for larger data

        if (source.Length >= 64 && Unsafe.SizeOf<T>() >= 4)
        {
            VectorizedCopy(source, destination);
        }
        else
        {
            source.CopyTo(destination);
        }
    }

    /// <summary>
    /// Vectorized copy operation using SIMD when available.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source span.</param>
    /// <param name="destination">Destination span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void VectorizedCopy<T>(ReadOnlySpan<T> source, Span<T> destination) where T : unmanaged
    {
        var elementSize = Unsafe.SizeOf<T>();
        var totalBytes = source.Length * elementSize;


        fixed (T* srcPtr = source)
        fixed (T* dstPtr = destination)
        {
            Buffer.MemoryCopy(srcPtr, dstPtr, totalBytes, totalBytes);
        }
    }

    /// <summary>
    /// Compares two spans for equality using optimized comparison.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="left">First span.</param>
    /// <param name="right">Second span.</param>
    /// <returns>True if spans are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FastEquals<T>(this ReadOnlySpan<T> left, ReadOnlySpan<T> right) where T : unmanaged
    {
        if (left.Length != right.Length)
        {
            return false;
        }


        if (left.IsEmpty)
        {
            return true;
        }

        // Use vectorized comparison for byte data

        if (typeof(T) == typeof(byte))
        {
            return left.SequenceEqual(right);
        }

        // For other types, use memory comparison

        return MemoryExtensions.SequenceEqual(left, right);
    }

    /// <summary>
    /// Fills a span with a value using vectorized operations when possible.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="span">Span to fill.</param>
    /// <param name="value">Value to fill with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FastFill<T>(this Span<T> span, T value) where T : unmanaged
    {
        if (span.IsEmpty)
        {
            return;
        }

        // Use optimized fill for primitive types

        if (typeof(T) == typeof(byte))
        {
            var byteSpan = MemoryMarshal.AsBytes(span);
            byteSpan.Fill(Unsafe.As<T, byte>(ref value));
        }
        else if (typeof(T) == typeof(int) && Unsafe.As<T, int>(ref value) == 0)
        {
            var byteSpan = MemoryMarshal.AsBytes(span);
            byteSpan.Clear();
        }
        else
        {
            span.Fill(value);
        }
    }

    /// <summary>
    /// Clears a span using the fastest available method.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="span">Span to clear.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FastClear<T>(this Span<T> span) where T : unmanaged
    {
        if (span.IsEmpty)
        {
            return;
        }


        var byteSpan = MemoryMarshal.AsBytes(span);
        byteSpan.Clear();
    }
}

/// <summary>
/// Memory mapping utilities for zero-copy file operations.
/// </summary>
public static class MemoryMappedOperations
{
    /// <summary>
    /// Creates a memory-mapped view of a file for zero-copy access.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="access">Memory map access mode.</param>
    /// <returns>Memory-mapped span.</returns>
    public static MemoryMappedSpan<T> CreateMemoryMappedSpan<T>(
        string filePath,

        MemoryMappedFileAccess access = MemoryMappedFileAccess.Read) where T : unmanaged => new(filePath, access);
}

/// <summary>
/// Represents a memory-mapped span for zero-copy file access.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class MemoryMappedSpan<T> : IDisposable where T : unmanaged
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly long _length;
    private bool _disposed;

    internal MemoryMappedSpan(string filePath, MemoryMappedFileAccess access)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _length = fileInfo.Length / Unsafe.SizeOf<T>();
        _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, "mmf", fileInfo.Length, access);
        _accessor = _mmf.CreateViewAccessor(0, fileInfo.Length, access);
    }

    /// <summary>
    /// Gets the length of the memory-mapped span in elements.
    /// </summary>
    public long Length => _length;

    /// <summary>
    /// Gets a span view of the memory-mapped data.
    /// </summary>
    /// <returns>Span representing the memory-mapped data.</returns>
    public unsafe Span<T> AsSpan()
    {
        ThrowIfDisposed();
        var ptr = _accessor.SafeMemoryMappedViewHandle.DangerousGetHandle();
        return new Span<T>(ptr.ToPointer(), (int)_length);
    }

    /// <summary>
    /// Gets a read-only span view of the memory-mapped data.
    /// </summary>
    /// <returns>Read-only span representing the memory-mapped data.</returns>
    public unsafe ReadOnlySpan<T> AsReadOnlySpan()
    {
        ThrowIfDisposed();
        var ptr = _accessor.SafeMemoryMappedViewHandle.DangerousGetHandle();
        return new ReadOnlySpan<T>(ptr.ToPointer(), (int)_length);
    }

    /// <summary>
    /// Gets a slice of the memory-mapped data.
    /// </summary>
    /// <param name="offset">Offset in elements.</param>
    /// <param name="length">Length in elements.</param>
    /// <returns>Sliced span.</returns>
    public Span<T> AsSpan(long offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException();
        }


        return AsSpan().Slice((int)offset, length);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _accessor?.Dispose();
            _mmf?.Dispose();
        }
    }
}

/// <summary>
/// Pinned memory utilities for interop scenarios.
/// </summary>
public static class PinnedMemoryOperations
{
    /// <summary>
    /// Creates a pinned memory handle for a span.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="span">Span to pin.</param>
    /// <returns>Pinned memory handle.</returns>
    public static PinnedMemoryHandle<T> Pin<T>(this Span<T> span) where T : unmanaged => new(span);

    /// <summary>
    /// Creates a pinned memory handle for a read-only span.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="span">Span to pin.</param>
    /// <returns>Pinned memory handle.</returns>
    public static PinnedMemoryHandle<T> Pin<T>(this ReadOnlySpan<T> span) where T : unmanaged => new(span);
}

/// <summary>
/// Represents a pinned memory handle with automatic cleanup.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public readonly struct PinnedMemoryHandle<T> : IDisposable, IEquatable<PinnedMemoryHandle<T>> where T : unmanaged
{
    private readonly MemoryHandle _handle;

    internal PinnedMemoryHandle(Span<T> span)
    {
        _handle = span.ToArray().AsMemory().Pin();
    }

    internal PinnedMemoryHandle(ReadOnlySpan<T> span)
    {
        _handle = span.ToArray().AsMemory().Pin();
    }

    /// <summary>
    /// Gets the pinned pointer.
    /// </summary>
    public unsafe void* Pointer => _handle.Pointer;

    /// <summary>
    /// Gets the pinned pointer as IntPtr.
    /// </summary>
    public unsafe IntPtr IntPtr => new(_handle.Pointer);

    /// <summary>
    /// Determines whether this instance is equal to another pinned memory handle.
    /// Two handles are equal if they point to the same memory location.
    /// </summary>
    /// <param name="other">The other pinned memory handle to compare.</param>
    /// <returns>True if the handles point to the same memory; otherwise, false.</returns>
    public unsafe bool Equals(PinnedMemoryHandle<T> other) => _handle.Pointer == other._handle.Pointer;

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if the object is a PinnedMemoryHandle&lt;T&gt; pointing to the same memory; otherwise, false.</returns>
    public override unsafe bool Equals(object? obj) => obj is PinnedMemoryHandle<T> other && Equals(other);

    /// <summary>
    /// Returns a hash code for this pinned memory handle based on the pointer address.
    /// </summary>
    /// <returns>A hash code representing the pinned memory address.</returns>
    public override unsafe int GetHashCode() => ((IntPtr)_handle.Pointer).GetHashCode();

    /// <summary>
    /// Determines whether two pinned memory handles are equal.
    /// </summary>
    /// <param name="left">The first handle to compare.</param>
    /// <param name="right">The second handle to compare.</param>
    /// <returns>True if the handles point to the same memory; otherwise, false.</returns>
    public static bool operator ==(PinnedMemoryHandle<T> left, PinnedMemoryHandle<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two pinned memory handles are not equal.
    /// </summary>
    /// <param name="left">The first handle to compare.</param>
    /// <param name="right">The second handle to compare.</param>
    /// <returns>True if the handles point to different memory; otherwise, false.</returns>
    public static bool operator !=(PinnedMemoryHandle<T> left, PinnedMemoryHandle<T> right) => !left.Equals(right);

    /// <summary>
    /// Performs dispose.
    /// </summary>
    public void Dispose() => _handle.Dispose();
}
