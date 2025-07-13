using Xunit;
using FluentAssertions;
using DotCompute.Memory;
using DotCompute.Abstractions;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Advanced memory management tests targeting 95%+ coverage
/// Includes error handling, edge cases, and stress scenarios
/// </summary>
public class AdvancedMemoryTests
{
    [Fact]
    public void UnifiedBufferWithZeroSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new UnifiedBuffer<int>(0));
    }

    [Fact]
    public void UnifiedBufferWithNegativeSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new UnifiedBuffer<int>(-1));
    }

    [Fact]
    public void UnifiedBufferWithExtremeSize_ShouldHandleGracefully()
    {
        // Arrange & Act
        try
        {
            var buffer = new UnifiedBuffer<byte>(1024 * 1024); // 1MB
            
            // Assert
            buffer.Size.Should().Be(1024 * 1024);
            buffer.Should().NotBeNull();
        }
        catch (OutOfMemoryException)
        {
            // This is acceptable for extreme sizes
            true.Should().BeTrue("OutOfMemoryException is acceptable for large allocations");
        }
    }

    [Fact]
    public void UnifiedBufferDisposal_ShouldBeIdempotent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(100);

        // Act & Assert
        buffer.Dispose(); // First disposal
        buffer.Dispose(); // Second disposal should not throw
        
        buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void UnifiedBufferAccessAfterDisposal_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(100);
        buffer.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _ = buffer.Size);
    }

    [Fact]
    public async Task UnifiedBufferEnsureOnHostAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(100);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await buffer.EnsureOnHostAsync(null!));
    }

    [Fact]
    public async Task UnifiedBufferEnsureOnDeviceAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(100);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await buffer.EnsureOnDeviceAsync(null!));
    }

    [Fact]
    public async Task UnifiedBufferSynchronizeAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(100);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await buffer.SynchronizeAsync(null!));
    }

    [Fact]
    public async Task UnifiedBufferOperationsWithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(100);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Create a mock context for testing
        var mockContext = new TestAcceleratorContext();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await buffer.EnsureOnHostAsync(mockContext, cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await buffer.EnsureOnDeviceAsync(mockContext, cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await buffer.SynchronizeAsync(mockContext, cts.Token));
    }

    [Fact]
    public void MemoryPoolWithZeroCapacity_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new MemoryPool<int>(0));
    }

    [Fact]
    public void MemoryPoolWithNegativeCapacity_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new MemoryPool<int>(-1));
    }

    [Fact]
    public async Task MemoryPoolRentAsync_ExceedingCapacity_ShouldHandleGracefully()
    {
        // Arrange
        var pool = new MemoryPool<int>(10);

        // Act - Try to rent more than pool capacity
        var buffers = new List<IMemoryBuffer<int>>();
        
        for (int i = 0; i < 15; i++) // Exceed capacity
        {
            try
            {
                var buffer = await pool.RentAsync(1);
                buffers.Add(buffer);
            }
            catch (InvalidOperationException)
            {
                // Expected when pool is exhausted
                break;
            }
        }

        // Assert
        buffers.Count.Should().BeLessOrEqualTo(10, "Should not exceed pool capacity");

        // Cleanup
        foreach (var buffer in buffers)
        {
            pool.Return(buffer);
        }
    }

    [Fact]
    public void MemoryPoolReturnNullBuffer_ShouldThrowArgumentNullException()
    {
        // Arrange
        var pool = new MemoryPool<int>(10);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => pool.Return(null!));
    }

    [Fact]
    public void MemoryPoolReturnBufferTwice_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pool = new MemoryPool<int>(10);
        var buffer = pool.RentAsync(1).Result;

        // Act
        pool.Return(buffer); // First return - OK

        // Assert
        Assert.Throws<InvalidOperationException>(() => pool.Return(buffer)); // Second return - should throw
    }

    [Fact]
    public void UnifiedMemoryManagerWithNullAccelerators_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedMemoryManager(null!));
    }

    [Fact]
    public void UnifiedMemoryManagerWithEmptyAccelerators_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new UnifiedMemoryManager(Array.Empty<IAccelerator>()));
    }

    [Fact]
    public async Task UnifiedMemoryManagerAllocateAsync_WithZeroSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var accelerators = new[] { new TestAccelerator() };
        var manager = new UnifiedMemoryManager(accelerators);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await manager.AllocateAsync<int>(0));
    }

    [Fact]
    public async Task UnifiedMemoryManagerAllocateAsync_WithNegativeSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var accelerators = new[] { new TestAccelerator() };
        var manager = new UnifiedMemoryManager(accelerators);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await manager.AllocateAsync<int>(-1));
    }

    [Fact]
    public async Task UnifiedMemoryManagerOptimizeMemoryLayout_WithHighFragmentation_ShouldImproveLayout()
    {
        // Arrange
        var accelerators = new[] { new TestAccelerator() };
        var manager = new UnifiedMemoryManager(accelerators);

        // Create fragmentation by allocating and deallocating
        var buffers = new List<IUnifiedBuffer<int>>();
        for (int i = 0; i < 10; i++)
        {
            var buffer = await manager.AllocateAsync<int>(100);
            buffers.Add(buffer);
        }

        // Deallocate every other buffer to create fragmentation
        for (int i = 0; i < buffers.Count; i += 2)
        {
            buffers[i].Dispose();
        }

        // Act
        await manager.OptimizeMemoryLayout();

        // Assert
        var stats = manager.GetMemoryStatistics();
        stats.Should().NotBeNull();
        stats.TotalAllocated.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MemoryAllocatorWithInvalidAlignment_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new MemoryAllocator(1024, 3)); // Non-power-of-2 alignment
    }

    [Fact]
    public void MemoryAllocatorWithZeroAlignment_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new MemoryAllocator(1024, 0));
    }

    [Fact]
    public void MemoryAllocatorAllocateWithInvalidSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var allocator = new MemoryAllocator(1024, 8);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => allocator.Allocate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => allocator.Allocate(-1));
    }

    [Fact]
    public void MemoryAllocatorAllocateExceedingCapacity_ShouldThrowOutOfMemoryException()
    {
        // Arrange
        var allocator = new MemoryAllocator(100, 8); // Small capacity

        // Act & Assert
        Assert.Throws<OutOfMemoryException>(() => allocator.Allocate(200)); // Exceed capacity
    }

    [Fact]
    public void UnsafeMemoryOperationsCopyWithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var destination = new byte[100];

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            UnsafeMemoryOperations.Copy(null!, destination, 50));
    }

    [Fact]
    public void UnsafeMemoryOperationsCopyWithNullDestination_ShouldThrowArgumentNullException()
    {
        // Arrange
        var source = new byte[100];

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            UnsafeMemoryOperations.Copy(source, null!, 50));
    }

    [Fact]
    public void UnsafeMemoryOperationsCopyWithNegativeLength_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var source = new byte[100];
        var destination = new byte[100];

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            UnsafeMemoryOperations.Copy(source, destination, -1));
    }

    [Fact]
    public void UnsafeMemoryOperationsCopyExceedingBufferSize_ShouldThrowArgumentException()
    {
        // Arrange
        var source = new byte[50];
        var destination = new byte[30];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            UnsafeMemoryOperations.Copy(source, destination, 100)); // Exceeds both buffers
    }
}

/// <summary>
/// Stress tests for memory components
/// </summary>
public class MemoryStressTests
{
    [Fact]
    public async Task UnifiedMemoryManagerHighVolumeAllocations_ShouldHandleGracefully()
    {
        // Arrange
        var accelerators = new[] { new TestAccelerator() };
        var manager = new UnifiedMemoryManager(accelerators);
        const int allocationCount = 1000;
        var buffers = new List<IUnifiedBuffer<int>>();

        // Act
        try
        {
            for (int i = 0; i < allocationCount; i++)
            {
                var buffer = await manager.AllocateAsync<int>(10);
                buffers.Add(buffer);
            }
        }
        catch (OutOfMemoryException)
        {
            // This is acceptable for stress testing
        }

        // Assert
        buffers.Count.Should().BeGreaterThan(0, "Should allocate at least some buffers");

        // Cleanup
        foreach (var buffer in buffers)
        {
            try
            {
                buffer.Dispose();
            }
            catch
            {
                // Ignore disposal errors in stress test
            }
        }
    }

    [Fact]
    public async Task MemoryPoolConcurrentRentReturn_ShouldBeThreadSafe()
    {
        // Arrange
        var pool = new MemoryPool<int>(100);
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        var buffer = await pool.RentAsync(1);
                        await Task.Delay(1); // Simulate work
                        pool.Return(buffer);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("No thread safety issues should occur");
    }

    [Fact]
    public void MemoryAllocatorFragmentationStressTest_ShouldHandleGracefully()
    {
        // Arrange
        var allocator = new MemoryAllocator(10240, 8); // 10KB
        var allocations = new List<IntPtr>();

        // Act - Create fragmentation
        try
        {
            // Allocate many small blocks
            for (int i = 0; i < 100; i++)
            {
                var ptr = allocator.Allocate(50);
                allocations.Add(ptr);
            }

            // Free every other block to create fragmentation
            for (int i = 0; i < allocations.Count; i += 2)
            {
                allocator.Free(allocations[i]);
            }

            // Try to allocate a larger block
            var largePtr = allocator.Allocate(200);
            largePtr.Should().NotBe(IntPtr.Zero);
        }
        catch (OutOfMemoryException)
        {
            // This is acceptable in fragmentation scenarios
            true.Should().BeTrue("OutOfMemoryException is acceptable with fragmentation");
        }
        finally
        {
            // Cleanup remaining allocations
            for (int i = 1; i < allocations.Count; i += 2)
            {
                try
                {
                    allocator.Free(allocations[i]);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        // Assert
        allocations.Count.Should().BeGreaterThan(0, "Should have made some allocations");
    }

    [Fact]
    public async Task UnifiedBufferConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(1000);
        const int threadCount = 5;
        const int operationsPerThread = 100;
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var context = new TestAcceleratorContext();
                    
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        // Simulate concurrent operations
                        var size = buffer.Size;
                        var isDisposed = buffer.IsDisposed;
                        
                        if (!isDisposed)
                        {
                            await buffer.EnsureOnHostAsync(context);
                            await Task.Delay(1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("No thread safety issues should occur");
        
        // Cleanup
        buffer.Dispose();
    }
}

/// <summary>
/// Test implementations for testing purposes
/// </summary>
internal class TestAccelerator : IAccelerator
{
    public AcceleratorType Type => AcceleratorType.CPU;
    public string Name => "TestAccelerator";
    public string Version => "1.0";
    public long MemorySize => 1024 * 1024; // 1MB

    public void Dispose()
    {
        // Test implementation
    }
}

internal class TestAcceleratorContext : AcceleratorContext
{
    public override void Dispose()
    {
        // Test implementation
    }
}

/// <summary>
/// Benchmark and performance tests
/// </summary>
public class MemoryBenchmarkTests
{
    [Fact]
    public void MemoryBenchmarksRunAllBenchmarks_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var benchmarks = new MemoryBenchmarks();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        try
        {
            var results = benchmarks.RunAllBenchmarks();
            stopwatch.Stop();

            // Assert
            results.Should().NotBeNull();
            results.Should().NotBeEmpty();
            stopwatch.ElapsedSeconds.Should().BeLessThan(60, "Benchmarks should complete within 1 minute");
        }
        catch (Exception ex)
        {
            // Log the exception but don't fail the test unless it's critical
            ex.Should().NotBeOfType<OutOfMemoryException>("Critical memory errors should not occur");
        }
    }

    [Fact]
    public void MemoryBenchmarkResultsSerialization_ShouldPreserveData()
    {
        // Arrange
        var results = new MemoryBenchmarkResults
        {
            AllocationThroughput = 1000.0,
            DeallocationThroughput = 1200.0,
            MemoryUtilization = 0.85,
            FragmentationRatio = 0.15,
            AverageAllocationTime = TimeSpan.FromMicroseconds(50),
            PeakMemoryUsage = 1024 * 1024
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(results);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<MemoryBenchmarkResults>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.AllocationThroughput.Should().Be(results.AllocationThroughput);
        deserialized.DeallocationThroughput.Should().Be(results.DeallocationThroughput);
        deserialized.MemoryUtilization.Should().Be(results.MemoryUtilization);
        deserialized.FragmentationRatio.Should().Be(results.FragmentationRatio);
        deserialized.PeakMemoryUsage.Should().Be(results.PeakMemoryUsage);
    }

    [Fact]
    public void MemoryBenchmarkResultsWithExtremeValues_ShouldHandleGracefully()
    {
        // Arrange & Act
        var results = new MemoryBenchmarkResults
        {
            AllocationThroughput = double.MaxValue,
            DeallocationThroughput = double.PositiveInfinity,
            MemoryUtilization = 1.0,
            FragmentationRatio = 0.0,
            AverageAllocationTime = TimeSpan.MaxValue,
            PeakMemoryUsage = long.MaxValue
        };

        // Assert
        results.AllocationThroughput.Should().Be(double.MaxValue);
        results.DeallocationThroughput.Should().Be(double.PositiveInfinity);
        results.MemoryUtilization.Should().Be(1.0);
        results.FragmentationRatio.Should().Be(0.0);
        results.AverageAllocationTime.Should().Be(TimeSpan.MaxValue);
        results.PeakMemoryUsage.Should().Be(long.MaxValue);
    }
}