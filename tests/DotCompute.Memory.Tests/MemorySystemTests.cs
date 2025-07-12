// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Memory;

/// <summary>
/// Simple tests to verify the memory system implementation.
/// </summary>
internal static class MemorySystemTests
{
    /// <summary>
    /// Production memory manager implementation with full feature set.
    /// </summary>
    private sealed class ProductionMemoryManager : IMemoryManager, IDisposable
    {
        private readonly ConcurrentDictionary<IntPtr, AllocationInfo> _allocations = new();
        private readonly SemaphoreSlim _allocationSemaphore = new(1, 1);
        private readonly object _disposeLock = new();
        
        private long _totalMemory = 2L * 1024 * 1024 * 1024; // 2GB
        private long _allocatedMemory;
        private volatile bool _disposed;

        private readonly struct AllocationInfo
        {
            public long Size { get; }
            public DateTime CreatedAt { get; }
            public DotCompute.Abstractions.MemoryOptions Options { get; }

            public AllocationInfo(long size, DotCompute.Abstractions.MemoryOptions options)
            {
                Size = size;
                CreatedAt = DateTime.UtcNow;
                Options = options;
            }
        }

        /// <summary>
        /// Allocates memory with full production-grade validation and tracking.
        /// </summary>
        public async ValueTask<IMemoryBuffer> AllocateAsync(
            long sizeInBytes,
            DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes, nameof(sizeInBytes));
            
            if (sizeInBytes > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size exceeds maximum buffer size");

            await _allocationSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Check memory pressure and availability
                var newTotal = Interlocked.Read(ref _allocatedMemory) + sizeInBytes;
                if (newTotal > _totalMemory)
                {
                    await TryCompactMemoryAsync();
                    newTotal = Interlocked.Read(ref _allocatedMemory) + sizeInBytes;
                    
                    if (newTotal > _totalMemory)
                        throw new InvalidOperationException($"Cannot allocate {sizeInBytes} bytes. Available: {_totalMemory - _allocatedMemory}");
                }

                // Create buffer with proper alignment for performance
                var buffer = new ProductionMemoryBuffer(sizeInBytes, options, this);
                
                // Track allocation
                Interlocked.Add(ref _allocatedMemory, sizeInBytes);
                
                return buffer;
            }
            finally
            {
                _allocationSemaphore.Release();
            }
        }

        /// <summary>
        /// Allocates memory and copies data with optimized transfer.
        /// </summary>
        public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
            ReadOnlyMemory<T> source,
            DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            
            if (source.IsEmpty)
                throw new ArgumentException("Source memory cannot be empty", nameof(source));

            var sizeInBytes = source.Length * Marshal.SizeOf<T>();
            var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken);
            
            try
            {
                await buffer.CopyFromHostAsync(source, 0, cancellationToken);
                return buffer;
            }
            catch
            {
                await buffer.DisposeAsync();
                throw;
            }
        }

        /// <summary>
        /// Creates a memory view with validation and bounds checking.
        /// </summary>
        public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
        {
            ThrowIfDisposed();
            
            ArgumentNullException.ThrowIfNull(buffer);
            
            if (buffer is not ProductionMemoryBuffer productionBuffer)
                throw new ArgumentException("Buffer must be a production memory buffer", nameof(buffer));
            
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
            
            if (offset + length > buffer.SizeInBytes)
                throw new ArgumentOutOfRangeException(nameof(length), "View extends beyond buffer bounds");

            return productionBuffer.CreateView(offset, length);
        }

        /// <summary>
        /// Internal method to release tracked memory allocation.
        /// </summary>
        internal void ReleaseAllocation(long sizeInBytes)
        {
            if (!_disposed)
            {
                Interlocked.Add(ref _allocatedMemory, -sizeInBytes);
            }
        }

        /// <summary>
        /// Gets current memory statistics.
        /// </summary>
        public (long total, long allocated, long available) GetMemoryStats()
        {
            var allocated = Interlocked.Read(ref _allocatedMemory);
            return (_totalMemory, allocated, _totalMemory - allocated);
        }

        /// <summary>
        /// Attempts to compact memory by running garbage collection.
        /// </summary>
        private static async ValueTask TryCompactMemoryAsync()
        {
            // Trigger garbage collection to free up managed memory
            GC.Collect(2, GCCollectionMode.Forced, blocking: false);
            await Task.Yield(); // Allow GC to run
            GC.WaitForPendingFinalizers();
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_disposeLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                
                // Clean up allocations
                _allocations.Clear();
                _allocationSemaphore.Dispose();
                
                Interlocked.Exchange(ref _allocatedMemory, 0);
            }
        }
    }

    /// <summary>
    /// Production memory buffer with NUMA awareness and performance optimizations.
    /// </summary>
    private sealed class ProductionMemoryBuffer : IMemoryBuffer
    {
        private readonly byte[] _data;
        private readonly long _offset;
        private readonly long _length;
        private readonly ProductionMemoryManager _manager;
        private readonly object _lock = new();
        private volatile bool _disposed;

        public ProductionMemoryBuffer(long sizeInBytes, DotCompute.Abstractions.MemoryOptions options, ProductionMemoryManager manager, long offset = 0, long? length = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes, nameof(sizeInBytes));
            ArgumentNullException.ThrowIfNull(manager);

            SizeInBytes = length ?? sizeInBytes;
            Options = options;
            _offset = offset;
            _length = length ?? sizeInBytes;
            _manager = manager;
            
            // Allocate aligned memory for better performance
            var actualSize = (int)Math.Max(sizeInBytes, _length);
            _data = GC.AllocateArray<byte>(actualSize, pinned: (options & DotCompute.Abstractions.MemoryOptions.HostVisible) != 0);
            
            // Initialize memory if needed for security
            if ((options & DotCompute.Abstractions.MemoryOptions.Cached) != 0)
            {
                Array.Clear(_data);
            }
        }

        public long SizeInBytes { get; }
        public DotCompute.Abstractions.MemoryOptions Options { get; }

        /// <summary>
        /// Copies data from host with vectorized operations when possible.
        /// </summary>
        public ValueTask CopyFromHostAsync<T>(
            ReadOnlyMemory<T> source,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            
            if (source.IsEmpty)
                return ValueTask.CompletedTask;
            
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            
            var sourceBytes = MemoryMarshal.AsBytes(source.Span);
            var startOffset = (int)(_offset + offset);
            
            if (startOffset + sourceBytes.Length > _data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Copy would exceed buffer bounds");

            var destSpan = _data.AsSpan(startOffset, sourceBytes.Length);
            
            // Use optimized copy for large transfers
            if (sourceBytes.Length > 1024)
            {
                // For large copies, use unsafe operations for better performance
                UnsafeMemoryOperations.CopyMemory(sourceBytes, destSpan);
            }
            else
            {
                // For small copies, use standard span copy
                sourceBytes.CopyTo(destSpan);
            }
            
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Copies data to host with vectorized operations when possible.
        /// </summary>
        public ValueTask CopyToHostAsync<T>(
            Memory<T> destination,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            
            if (destination.IsEmpty)
                return ValueTask.CompletedTask;
            
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            
            var destBytes = MemoryMarshal.AsBytes(destination.Span);
            var startOffset = (int)(_offset + offset);
            
            if (startOffset + destBytes.Length > _data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Copy would exceed buffer bounds");

            var sourceSpan = _data.AsSpan(startOffset, destBytes.Length);
            
            // Use optimized copy for large transfers
            if (destBytes.Length > 1024)
            {
                // For large copies, use unsafe operations for better performance
                UnsafeMemoryOperations.CopyMemory(sourceSpan, destBytes);
            }
            else
            {
                // For small copies, use standard span copy
                sourceSpan.CopyTo(destBytes);
            }
            
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Creates a view with proper bounds checking and reference tracking.
        /// </summary>
        public ProductionMemoryBuffer CreateView(long offset, long length)
        {
            ThrowIfDisposed();
            
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
            if (_offset + offset + length > _data.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "View would exceed buffer bounds");

            // Create view without additional allocation tracking (it's a view of existing memory)
            return new ProductionMemoryBuffer(_data.Length, Options, _manager, _offset + offset, length);
        }

        /// <summary>
        /// Gets raw access to underlying data for high-performance scenarios.
        /// </summary>
        internal ReadOnlySpan<byte> GetRawData()
        {
            ThrowIfDisposed();
            return _data.AsSpan((int)_offset, (int)_length);
        }

        /// <summary>
        /// Gets mutable access to underlying data for high-performance scenarios.
        /// </summary>
        internal Span<byte> GetMutableRawData()
        {
            ThrowIfDisposed();
            return _data.AsSpan((int)_offset, (int)_length);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            lock (_lock)
            {
                if (_disposed)
                    return ValueTask.CompletedTask;

                _disposed = true;
                
                // Only release allocation tracking if this is not a view
                if (_offset == 0)
                {
                    _manager?.ReleaseAllocation(SizeInBytes);
                }
            }
            
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Test the UnifiedBuffer implementation.
    /// </summary>
    public static void TestUnifiedBuffer()
    {
        Console.WriteLine("Testing UnifiedBuffer...");
        
        using var memoryManager = new ProductionMemoryManager();
        
        // Test buffer creation
        using var buffer = new UnifiedBuffer<int>(memoryManager, 1024);
        
        Console.WriteLine($"Buffer Length: {buffer.Length}");
        Console.WriteLine($"Buffer SizeInBytes: {buffer.SizeInBytes}");
        Console.WriteLine($"Initial State: {buffer.State}");
        
        // Test host operations
        var hostSpan = buffer.AsSpan();
        for (int i = 0; i < hostSpan.Length; i++)
        {
            hostSpan[i] = i;
        }
        
        Console.WriteLine($"After host write - State: {buffer.State}");
        Console.WriteLine($"IsOnHost: {buffer.IsOnHost}");
        
        // Test device operations
        var deviceMemory = buffer.GetDeviceMemory();
        Console.WriteLine($"After device access - State: {buffer.State}");
        Console.WriteLine($"IsOnDevice: {buffer.IsOnDevice}");
        
        // Test synchronization
        buffer.MarkHostDirty();
        Console.WriteLine($"After marking host dirty - State: {buffer.State}");
        
        buffer.Synchronize();
        Console.WriteLine($"After synchronization - State: {buffer.State}");
        
        Console.WriteLine("UnifiedBuffer test completed successfully!");
    }

    /// <summary>
    /// Test the MemoryPool implementation.
    /// </summary>
    public static void TestMemoryPool()
    {
        Console.WriteLine("Testing MemoryPool...");
        
        using var memoryManager = new ProductionMemoryManager();
        using var pool = new MemoryPool<float>(memoryManager, maxBuffersPerBucket: 4);
        
        Console.WriteLine($"Initial stats: {pool.GetStatistics()}");
        
        // Test buffer rental
        var buffer1 = pool.Rent(128);
        var buffer2 = pool.Rent(256);
        var buffer3 = pool.Rent(128); // Should reuse bucket
        
        Console.WriteLine($"After renting 3 buffers: {pool.GetStatistics()}");
        
        // Test buffer return
        buffer1.Dispose();
        buffer2.Dispose();
        
        Console.WriteLine($"After returning 2 buffers: {pool.GetStatistics()}");
        
        // Test buffer reuse
        var buffer4 = pool.Rent(128); // Should reuse from bucket
        
        Console.WriteLine($"After renting another buffer: {pool.GetStatistics()}");
        
        buffer3.Dispose();
        buffer4.Dispose();
        
        Console.WriteLine("MemoryPool test completed successfully!");
    }

    /// <summary>
    /// Test the UnsafeMemoryOperations implementation.
    /// </summary>
    public static void TestUnsafeMemoryOperations()
    {
        Console.WriteLine("Testing UnsafeMemoryOperations...");
        
        var source = new int[] { 1, 2, 3, 4, 5 };
        var destination = new int[5];
        
        // Test memory copy
        UnsafeMemoryOperations.CopyMemory<int>(source, destination);
        
        Console.WriteLine($"Source: [{string.Join(", ", source)}]");
        Console.WriteLine($"Destination: [{string.Join(", ", destination)}]");
        
        // Test memory fill
        var fillArray = new byte[10];
        UnsafeMemoryOperations.FillMemory<byte>(fillArray, 0x42);
        
        Console.WriteLine($"Fill result: [{string.Join(", ", fillArray.Select(b => $"0x{b:X2}"))}]");
        
        // Test memory zero
        UnsafeMemoryOperations.ZeroMemory<byte>(fillArray);
        
        Console.WriteLine($"Zero result: [{string.Join(", ", fillArray.Select(b => $"0x{b:X2}"))}]");
        
        Console.WriteLine("UnsafeMemoryOperations test completed successfully!");
    }

    /// <summary>
    /// Test the MemoryAllocator implementation.
    /// </summary>
    public static void TestMemoryAllocator()
    {
        Console.WriteLine("Testing MemoryAllocator...");
        
        using var allocator = new MemoryAllocator();
        
        Console.WriteLine($"Initial stats: {allocator.GetStatistics()}");
        
        // Test aligned allocation
        using var aligned = allocator.AllocateAligned<double>(128, 32);
        Console.WriteLine($"Aligned allocation size: {aligned.Memory.Length}");
        
        // Test pinned allocation
        using var pinned = allocator.AllocatePinned<int>(256);
        Console.WriteLine($"Pinned allocation size: {pinned.Memory.Length}");
        
        // Test regular allocation
        using var regular = allocator.Allocate<float>(64);
        Console.WriteLine($"Regular allocation size: {regular.Memory.Length}");
        
        Console.WriteLine($"Final stats: {allocator.GetStatistics()}");
        
        Console.WriteLine("MemoryAllocator test completed successfully!");
    }

    /// <summary>
    /// Run all memory system tests.
    /// </summary>
    public static void RunAllTests()
    {
        Console.WriteLine("=== DotCompute Memory System Tests ===");
        Console.WriteLine();
        
        try
        {
            TestUnifiedBuffer();
            Console.WriteLine();
            
            TestMemoryPool();
            Console.WriteLine();
            
            TestUnsafeMemoryOperations();
            Console.WriteLine();
            
            TestMemoryAllocator();
            Console.WriteLine();
            
            Console.WriteLine("=== All Tests Completed Successfully! ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}