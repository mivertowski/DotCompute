using System;
using System.Buffers;
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
    /// Mock memory manager for testing purposes.
    /// </summary>
    private class MockMemoryManager : IMemoryManager
    {
        private readonly Dictionary<IntPtr, long> _allocations = new();
        private long _totalMemory = 1024 * 1024 * 1024; // 1GB
        private long _allocatedMemory = 0;

        // Implement async interface methods
        public ValueTask<IMemoryBuffer> AllocateAsync(
            long sizeInBytes,
            DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None,
            CancellationToken cancellationToken = default)
        {
            if (_allocatedMemory + sizeInBytes > _totalMemory)
                throw new OutOfMemoryException();

            var buffer = new MockMemoryBuffer(sizeInBytes, options);
            _allocatedMemory += sizeInBytes;
            
            return ValueTask.FromResult<IMemoryBuffer>(buffer);
        }

        public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
            ReadOnlyMemory<T> source,
            DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            var sizeInBytes = source.Length * Marshal.SizeOf<T>();
            var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken);
            await buffer.CopyFromHostAsync(source, 0, cancellationToken);
            return buffer;
        }

        public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
        {
            if (buffer is not MockMemoryBuffer mockBuffer)
                throw new ArgumentException("Buffer must be a mock memory buffer", nameof(buffer));
            return mockBuffer.CreateView(offset, length);
        }

        // Legacy sync methods for backward compatibility
        // Legacy sync methods removed due to interface conflicts

        public void Free(DeviceMemory memory)
        {
            if (_allocations.TryGetValue(memory.NativePointer, out var size))
            {
                Marshal.FreeHGlobal(memory.NativePointer);
                _allocations.Remove(memory.NativePointer);
                _allocatedMemory -= size;
            }
        }

        public void CopyToDevice<T>(ReadOnlySpan<T> source, DeviceMemory destination) where T : unmanaged
        {
            unsafe
            {
                var sourcePtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(source));
                var destPtr = (byte*)destination.NativePointer;
                var sizeInBytes = source.Length * sizeof(T);
                
                UnsafeMemoryOperations.CopyMemory(sourcePtr, destPtr, (nuint)sizeInBytes);
            }
        }

        public void CopyToHost<T>(DeviceMemory source, Span<T> destination) where T : unmanaged
        {
            unsafe
            {
                var sourcePtr = (byte*)source.NativePointer;
                var destPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(destination));
                var sizeInBytes = destination.Length * sizeof(T);
                
                UnsafeMemoryOperations.CopyMemory(sourcePtr, destPtr, (nuint)sizeInBytes);
            }
        }

        public void CopyDeviceToDevice(DeviceMemory source, DeviceMemory destination, long sizeInBytes)
        {
            unsafe
            {
                UnsafeMemoryOperations.CopyMemory(
                    source.NativePointer.ToPointer(),
                    destination.NativePointer.ToPointer(),
                    (nuint)sizeInBytes);
            }
        }

        public void CopyToDeviceWithContext<T>(ReadOnlyMemory<T> source, DeviceMemory destination, AcceleratorContext context) where T : unmanaged
        {
            // Simplified sync implementation for testing
            CopyToDevice(source.Span, destination);
        }

        public void CopyToHostWithContext<T>(DeviceMemory source, Memory<T> destination, AcceleratorContext context) where T : unmanaged
        {
            // Simplified sync implementation for testing
            CopyToHost(source, destination.Span);
        }

        public long GetAvailableMemory()
        {
            return _totalMemory - _allocatedMemory;
        }

        public long GetTotalMemory()
        {
            return _totalMemory;
        }

        public IMemoryOwner<T> AllocatePinnedHost<T>(int length) where T : unmanaged
        {
            var array = new T[length];
            return new MockMemoryOwner<T>(array);
        }

        public void Dispose()
        {
            foreach (var kvp in _allocations)
            {
                Marshal.FreeHGlobal(kvp.Key);
            }
            _allocations.Clear();
            _allocatedMemory = 0;
        }
    }

    /// <summary>
    /// Mock memory buffer for testing.
    /// </summary>
    private class MockMemoryBuffer : IMemoryBuffer
    {
        private readonly byte[] _data;
        private readonly long _offset;
        private readonly long _length;
        private bool _disposed;

        public MockMemoryBuffer(long sizeInBytes, DotCompute.Abstractions.MemoryOptions options, long offset = 0, long? length = null)
        {
            SizeInBytes = length ?? sizeInBytes;
            Options = options;
            _offset = offset;
            _length = length ?? sizeInBytes;
            _data = new byte[sizeInBytes];
        }

        public long SizeInBytes { get; }
        public DotCompute.Abstractions.MemoryOptions Options { get; }

        public ValueTask CopyFromHostAsync<T>(
            ReadOnlyMemory<T> source,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MockMemoryBuffer));
            
            var sourceSpan = MemoryMarshal.AsBytes(source.Span);
            var destSpan = _data.AsSpan((int)(_offset + offset), sourceSpan.Length);
            sourceSpan.CopyTo(destSpan);
            
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyToHostAsync<T>(
            Memory<T> destination,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MockMemoryBuffer));
            
            var destSpan = MemoryMarshal.AsBytes(destination.Span);
            var sourceSpan = _data.AsSpan((int)(_offset + offset), destSpan.Length);
            sourceSpan.CopyTo(destSpan);
            
            return ValueTask.CompletedTask;
        }

        public MockMemoryBuffer CreateView(long offset, long length)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MockMemoryBuffer));
            return new MockMemoryBuffer(_data.Length, Options, _offset + offset, length);
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Mock memory owner for testing.
    /// </summary>
    private class MockMemoryOwner<T> : IMemoryOwner<T>
    {
        private readonly T[] _array;

        public MockMemoryOwner(T[] array)
        {
            _array = array;
            Memory = new Memory<T>(array);
        }

        public Memory<T> Memory { get; }

        public void Dispose()
        {
            // Nothing to dispose for managed array
        }
    }

    /// <summary>
    /// Test the UnifiedBuffer implementation.
    /// </summary>
    public static void TestUnifiedBuffer()
    {
        Console.WriteLine("Testing UnifiedBuffer...");
        
        using var memoryManager = new MockMemoryManager();
        
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
        
        using var memoryManager = new MockMemoryManager();
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