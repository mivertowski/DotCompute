using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using DotCompute.Memory;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for memory leak detection in the unified memory system.
/// </summary>
public class MemoryLeakDetectionTests : IDisposable
{
    private readonly TestMemoryManager _memoryManager;
    private readonly UnifiedMemoryManager _unifiedManager;
    private readonly List<WeakReference> _trackedObjects;
    
    public MemoryLeakDetectionTests()
    {
        _memoryManager = new TestMemoryManager();
        _unifiedManager = new UnifiedMemoryManager(_memoryManager);
        _trackedObjects = new List<WeakReference>();
    }
    
    [Fact]
    public async Task UnifiedBuffer_DisposedBuffers_ShouldBeGarbageCollected()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(false);
        var bufferRefs = new List<WeakReference>();
        
        // Act - Create and dispose buffers
        for (int i = 0; i < 100; i++)
        {
            var buffer = await _unifiedManager.CreateUnifiedBufferAsync<float>(1024);
            bufferRefs.Add(new WeakReference(buffer));
            buffer.Dispose();
        }
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Assert - All buffers should be collected
        int aliveCount = 0;
        foreach (var weakRef in bufferRefs)
        {
            if (weakRef.IsAlive)
                aliveCount++;
        }
        
        Assert.Equal(0, aliveCount);
        
        // Memory should return close to initial level
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;
        
        // Allow some tolerance for framework overhead
        Assert.True(memoryIncrease < 1024 * 1024, $"Memory increased by {memoryIncrease} bytes");
    }
    
    [Fact]
    public async Task MemoryPool_ReturnedBuffers_ShouldBeReused()
    {
        // Arrange
        var pool = _unifiedManager.GetPool<int>();
        var firstAllocationCount = _memoryManager.AllocationCount;
        
        // Act - Rent and return buffers
        var buffer1 = pool.Rent(256);
        var buffer1Ptr = GetBufferAddress(buffer1);
        buffer1.Dispose(); // Returns to pool
        
        var buffer2 = pool.Rent(256);
        var buffer2Ptr = GetBufferAddress(buffer2);
        
        // Assert - Same buffer should be reused
        Assert.Equal(buffer1Ptr, buffer2Ptr);
        Assert.Equal(firstAllocationCount + 1, _memoryManager.AllocationCount);
        
        buffer2.Dispose();
    }
    
    [Fact]
    public async Task UnifiedMemoryManager_HandleMemoryPressure_ShouldReleaseUnusedBuffers()
    {
        // Arrange
        var pool = _unifiedManager.GetPool<double>();
        var buffers = new List<IMemoryBuffer<double>>();
        
        // Rent multiple buffers
        for (int i = 0; i < 10; i++)
        {
            buffers.Add(pool.Rent(1024));
        }
        
        // Return half of them
        for (int i = 0; i < 5; i++)
        {
            buffers[i].Dispose();
        }
        
        var statsBeforePressure = pool.GetStatistics();
        
        // Act - Apply memory pressure
        await _unifiedManager.HandleMemoryPressureAsync(0.8);
        
        // Assert - Pool should have released some buffers
        var statsAfterPressure = pool.GetStatistics();
        Assert.True(statsAfterPressure.TotalAllocatedBytes < statsBeforePressure.TotalAllocatedBytes);
        
        // Cleanup
        for (int i = 5; i < 10; i++)
        {
            buffers[i].Dispose();
        }
    }
    
    [Fact]
    public async Task DeviceMemory_AllocationAndDeallocation_ShouldNotLeak()
    {
        // Arrange
        var initialAllocations = _memoryManager.ActiveAllocations.Count;
        var buffers = new List<UnifiedBuffer<byte>>();
        
        // Act - Create buffers that allocate device memory
        for (int i = 0; i < 50; i++)
        {
            var buffer = await _unifiedManager.CreateUnifiedBufferAsync<byte>(4096);
            buffer.EnsureOnDevice(); // Force device allocation
            buffers.Add(buffer);
        }
        
        Assert.Equal(initialAllocations + 50, _memoryManager.ActiveAllocations.Count);
        
        // Dispose all buffers
        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }
        
        // Assert - All device memory should be freed
        Assert.Equal(initialAllocations, _memoryManager.ActiveAllocations.Count);
    }
    
    [Fact]
    public async Task MemoryAllocator_AlignedAllocations_ShouldBeFreedProperly()
    {
        // Arrange
        var allocator = new MemoryAllocator();
        var allocations = new List<IMemoryOwner<float>>();
        var initialStats = allocator.GetStatistics();
        
        // Act - Make multiple aligned allocations
        for (int i = 0; i < 20; i++)
        {
            var aligned = allocator.AllocateAligned<float>(1024, 64);
            allocations.Add(aligned);
        }
        
        var afterAllocStats = allocator.GetStatistics();
        Assert.Equal(20, afterAllocStats.ActiveAllocations);
        
        // Dispose all allocations
        foreach (var alloc in allocations)
        {
            alloc.Dispose();
        }
        
        // Assert - All allocations should be freed
        var finalStats = allocator.GetStatistics();
        Assert.Equal(0, finalStats.ActiveAllocations);
        Assert.Equal(0, finalStats.TotalAllocatedBytes);
    }
    
    [Fact]
    public async Task ConcurrentOperations_ShouldNotLeakMemory()
    {
        // Arrange
        var tasks = new List<Task>();
        var cts = new CancellationTokenSource();
        var allocatedCount = 0;
        var deallocatedCount = 0;
        
        // Act - Run concurrent allocations and deallocations
        for (int i = 0; i < 4; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var buffer = await _unifiedManager.CreateUnifiedBufferAsync<int>(Random.Shared.Next(100, 1000));
                    Interlocked.Increment(ref allocatedCount);
                    
                    // Simulate some work
                    buffer.EnsureOnHost();
                    var span = buffer.AsSpan();
                    span[0] = 42;
                    
                    buffer.Dispose();
                    Interlocked.Increment(ref deallocatedCount);
                    
                    await Task.Yield();
                }
            }));
        }
        
        // Run for 2 seconds
        await Task.Delay(2000);
        cts.Cancel();
        
        await Task.WhenAll(tasks);
        
        // Assert - Allocations and deallocations should match
        Assert.Equal(allocatedCount, deallocatedCount);
        
        // Force cleanup and check memory
        await _unifiedManager.CompactAsync();
        var stats = _unifiedManager.GetStats();
        Assert.Equal(0, stats.ActiveUnifiedBuffers);
    }
    
    [Fact]
    public async Task WeakReferences_ShouldDetectLeaks()
    {
        // Arrange
        var leakyList = new List<UnifiedBuffer<int>>();
        var weakRefs = new List<WeakReference>();
        
        // Act - Create buffers and track with weak references
        for (int i = 0; i < 10; i++)
        {
            var buffer = await _unifiedManager.CreateUnifiedBufferAsync<int>(1024);
            weakRefs.Add(new WeakReference(buffer));
            
            if (i < 5)
            {
                // Intentionally leak these
                leakyList.Add(buffer);
            }
            else
            {
                // Properly dispose these
                buffer.Dispose();
            }
        }
        
        // Force GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Assert - Should detect exactly 5 leaks
        int leakCount = 0;
        foreach (var weakRef in weakRefs)
        {
            if (weakRef.IsAlive)
                leakCount++;
        }
        
        Assert.Equal(5, leakCount);
        
        // Cleanup
        foreach (var buffer in leakyList)
        {
            buffer.Dispose();
        }
    }
    
    private IntPtr GetBufferAddress<T>(IMemoryBuffer<T> buffer) where T : unmanaged
    {
        var memory = buffer.GetMemory();
        unsafe
        {
            fixed (T* ptr = memory.Span)
            {
                return new IntPtr(ptr);
            }
        }
    }
    
    public void Dispose()
    {
        _unifiedManager?.Dispose();
        _memoryManager?.Dispose();
    }
}

/// <summary>
/// Test memory manager that tracks allocations for leak detection.
/// </summary>
internal class TestMemoryManager : IMemoryManager
{
    private readonly Dictionary<IntPtr, AllocationInfo> _allocations = new();
    private readonly object _lock = new();
    private long _totalMemory = 1024L * 1024 * 1024; // 1GB
    private long _allocatedMemory = 0;
    private int _allocationCount = 0;
    
    public Dictionary<IntPtr, AllocationInfo> ActiveAllocations => _allocations;
    public int AllocationCount => _allocationCount;
    
    public IAccelerator Accelerator => throw new NotImplementedException();
    
    public DeviceMemory Allocate(long sizeInBytes)
    {
        lock (_lock)
        {
            if (_allocatedMemory + sizeInBytes > _totalMemory)
                throw new OutOfMemoryException();
            
            var ptr = Marshal.AllocHGlobal((int)sizeInBytes);
            _allocations[ptr] = new AllocationInfo 
            { 
                Size = sizeInBytes, 
                AllocationTime = DateTime.UtcNow,
                StackTrace = Environment.StackTrace
            };
            _allocatedMemory += sizeInBytes;
            _allocationCount++;
            
            return new DeviceMemory(ptr, sizeInBytes);
        }
    }
    
    public DeviceMemory AllocateAligned(long sizeInBytes, int alignment)
    {
        return Allocate(sizeInBytes + alignment);
    }
    
    public void Free(DeviceMemory memory)
    {
        lock (_lock)
        {
            if (_allocations.TryGetValue(memory.NativePointer, out var info))
            {
                Marshal.FreeHGlobal(memory.NativePointer);
                _allocations.Remove(memory.NativePointer);
                _allocatedMemory -= info.Size;
            }
        }
    }
    
    public void CopyToDevice<T>(ReadOnlySpan<T> source, DeviceMemory destination) where T : unmanaged
    {
        unsafe
        {
            var sourcePtr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(source));
            var destPtr = destination.NativePointer.ToPointer();
            var sizeInBytes = source.Length * sizeof(T);
            
            Buffer.MemoryCopy(sourcePtr, destPtr, destination.SizeInBytes, sizeInBytes);
        }
    }
    
    public void CopyToHost<T>(DeviceMemory source, Span<T> destination) where T : unmanaged
    {
        unsafe
        {
            var sourcePtr = source.NativePointer.ToPointer();
            var destPtr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(destination));
            var sizeInBytes = destination.Length * sizeof(T);
            
            Buffer.MemoryCopy(sourcePtr, destPtr, sizeInBytes, sizeInBytes);
        }
    }
    
    public void CopyDeviceToDevice(DeviceMemory source, DeviceMemory destination, long sizeInBytes)
    {
        unsafe
        {
            Buffer.MemoryCopy(
                source.NativePointer.ToPointer(),
                destination.NativePointer.ToPointer(),
                destination.SizeInBytes,
                sizeInBytes);
        }
    }
    
    public void CopyToDeviceWithContext<T>(ReadOnlyMemory<T> source, DeviceMemory destination, AcceleratorContext context) where T : unmanaged
    {
        CopyToDevice(source.Span, destination);
    }
    
    public void CopyToHostWithContext<T>(DeviceMemory source, Memory<T> destination, AcceleratorContext context) where T : unmanaged
    {
        CopyToHost(source, destination.Span);
    }
    
    public long GetAvailableMemory() => _totalMemory - _allocatedMemory;
    
    public long GetTotalMemory() => _totalMemory;
    
    public IMemoryOwner<T> AllocatePinnedHost<T>(int length) where T : unmanaged
    {
        var array = new T[length];
        return new TestMemoryOwner<T>(array);
    }
    
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var kvp in _allocations.ToList())
            {
                Marshal.FreeHGlobal(kvp.Key);
            }
            _allocations.Clear();
            _allocatedMemory = 0;
        }
    }
    
    internal class AllocationInfo
    {
        public long Size { get; set; }
        public DateTime AllocationTime { get; set; }
        public string StackTrace { get; set; } = string.Empty;
    }
}

internal class TestMemoryOwner<T> : IMemoryOwner<T>
{
    private readonly T[] _array;
    
    public TestMemoryOwner(T[] array)
    {
        _array = array;
        Memory = new Memory<T>(array);
    }
    
    public Memory<T> Memory { get; }
    
    public void Dispose() { }
}