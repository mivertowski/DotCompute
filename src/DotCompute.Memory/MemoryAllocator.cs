// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;

namespace DotCompute.Memory;

/// <summary>
/// A high-performance memory allocator that provides aligned memory allocation and efficient memory management.
/// Supports both pinned and unpinned allocations with platform-specific optimizations.
/// </summary>
public sealed class MemoryAllocator : IDisposable
{
    private readonly Lock _lock = new();
    private volatile bool _disposed;
    private long _totalAllocatedBytes;
    private long _totalAllocations;
    private long _totalDeallocations;

    /// <summary>
    /// Gets the total number of bytes allocated.
    /// </summary>
    public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocatedBytes);

    /// <summary>
    /// Gets the total number of allocations made.
    /// </summary>
    public long TotalAllocations => Interlocked.Read(ref _totalAllocations);

    /// <summary>
    /// Gets the total number of deallocations made.
    /// </summary>
    public long TotalDeallocations => Interlocked.Read(ref _totalDeallocations);

    /// <summary>
    /// Allocates aligned memory for the specified type.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <param name="alignment">The alignment requirement in bytes.</param>
    /// <returns>A memory owner for the allocated memory.</returns>
    public IMemoryOwner<T> AllocateAligned<T>(int length, int alignment = UnsafeMemoryOperations.DefaultAlignment) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegative(alignment);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentException("Alignment must be a power of 2.", nameof(alignment));
        }

        var sizeInBytes = length * Unsafe.SizeOf<T>();

        // Allocate extra space for alignment
        var totalSize = sizeInBytes + alignment - 1;
        var memory = AllocateNative(totalSize);

        unsafe
        {
            var alignedPtr = UnsafeMemoryOperations.AlignAddress(memory.ToPointer(), alignment);
            var alignedMemory = new IntPtr(alignedPtr);

            Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);
            Interlocked.Increment(ref _totalAllocations);

            return new AlignedMemoryOwner<T>(this, memory, alignedMemory, length, sizeInBytes);
        }
    }

    /// <summary>
    /// Allocates pinned memory for the specified type.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <returns>A memory owner for the pinned memory.</returns>
    public IMemoryOwner<T> AllocatePinned<T>(int length) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var array = new T[length];
        var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        var sizeInBytes = length * Unsafe.SizeOf<T>();

        Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);
        Interlocked.Increment(ref _totalAllocations);

        return new PinnedMemoryOwner<T>(this, handle, array, sizeInBytes);
    }

    /// <summary>
    /// Allocates memory using the system allocator.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <returns>A memory owner for the allocated memory.</returns>
    public IMemoryOwner<T> Allocate<T>(int length) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return AllocateAligned<T>(length, IntPtr.Size); // Default to pointer alignment
    }

    /// <summary>
    /// Creates a unified buffer using this allocator.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="memoryManager">The memory manager for device operations.</param>
    /// <param name="length">The number of elements to allocate.</param>
    /// <returns>A unified buffer.</returns>
    public IMemoryBuffer<T> CreateUnifiedBuffer<T>(IMemoryManager memoryManager, int length) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(memoryManager);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new UnifiedBuffer<T>(memoryManager, length);
    }

    /// <summary>
    /// Creates a unified buffer with initial data using this allocator.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="memoryManager">The memory manager for device operations.</param>
    /// <param name="data">The initial data to populate the buffer with.</param>
    /// <returns>A unified buffer.</returns>
    public IMemoryBuffer<T> CreateUnifiedBuffer<T>(IMemoryManager memoryManager, ReadOnlySpan<T> data) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(memoryManager);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new UnifiedBuffer<T>(memoryManager, data);
    }

    /// <summary>
    /// Gets statistics about the memory allocator.
    /// </summary>
    /// <returns>Memory allocator statistics.</returns>
    public MemoryAllocatorStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new MemoryAllocatorStatistics(
            TotalAllocatedBytes,
            TotalAllocations,
            TotalDeallocations
        );
    }

    /// <summary>
    /// Releases all resources used by the MemoryAllocator.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }

    #region Internal Methods

    internal void NotifyDeallocation(long sizeInBytes)
    {
        Interlocked.Add(ref _totalAllocatedBytes, -sizeInBytes);
        Interlocked.Increment(ref _totalDeallocations);
    }

    #endregion

    #region Private Methods

    private static IntPtr AllocateNative(int size)
    {
        var ptr = Marshal.AllocHGlobal(size);
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to allocate {size} bytes of native memory.");
        }

        return ptr;
    }

    #endregion
}

/// <summary>
/// A memory owner for aligned memory allocations.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class AlignedMemoryOwner<T> : IMemoryOwner<T> where T : unmanaged
{
    private readonly MemoryAllocator _allocator;
    private readonly IntPtr _originalMemory;
    private readonly IntPtr _alignedMemory;
    private readonly int _length;
    private readonly long _sizeInBytes;
    private volatile bool _disposed;

    public Memory<T> Memory { get; }

    public AlignedMemoryOwner(MemoryAllocator allocator, IntPtr originalMemory, IntPtr alignedMemory, int length, long sizeInBytes)
    {
        _allocator = allocator;
        _originalMemory = originalMemory;
        _alignedMemory = alignedMemory;
        _length = length;
        _sizeInBytes = sizeInBytes;

        unsafe
        {
            var span = new Span<T>(alignedMemory.ToPointer(), length);
            Memory = new Memory<T>(span.ToArray());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Marshal.FreeHGlobal(_originalMemory);
        _allocator.NotifyDeallocation(_sizeInBytes);
    }
}

/// <summary>
/// A memory owner for pinned memory allocations.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class PinnedMemoryOwner<T>(MemoryAllocator allocator, GCHandle handle, T[] array, long sizeInBytes) : IMemoryOwner<T> where T : unmanaged
{
    private readonly MemoryAllocator _allocator = allocator;
    private readonly GCHandle _handle = handle;
    private readonly T[] _array = array;
    private readonly long _sizeInBytes = sizeInBytes;
    private volatile bool _disposed;

    public Memory<T> Memory { get; } = new Memory<T>(array);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_handle.IsAllocated)
        {
            _handle.Free();
        }

        _allocator.NotifyDeallocation(_sizeInBytes);
    }
}

/// <summary>
/// Statistics about a memory allocator.
/// </summary>
/// <param name="TotalAllocatedBytes">The total number of bytes allocated.</param>
/// <param name="TotalAllocations">The total number of allocations made.</param>
/// <param name="TotalDeallocations">The total number of deallocations made.</param>
public record MemoryAllocatorStatistics(
    long TotalAllocatedBytes,
    long TotalAllocations,
    long TotalDeallocations
)
{
    /// <summary>
    /// Gets the current number of active allocations.
    /// </summary>
    public long ActiveAllocations => TotalAllocations - TotalDeallocations;
}
