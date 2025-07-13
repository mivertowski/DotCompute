// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Comprehensive production integration tests for memory management.
/// Tests real-world scenarios, error conditions, and performance characteristics.
/// </summary>
public class ProductionMemoryIntegrationTests : IDisposable
{
    private readonly CpuMemoryManager _memoryManager;

    public ProductionMemoryIntegrationTests()
    {
        _memoryManager = new CpuMemoryManager();
    }

    [Fact]
    public async Task MemoryAllocationUnderMemoryPressure_ShouldHandleGracefully()
    {
        // Arrange
        const long largeBufferSize = 512 * 1024 * 1024; // 512MB
        var buffers = new List<IMemoryBuffer>();
        var exceptions = new List<Exception>();

        try
        {
            // Act - Allocate until we hit memory pressure
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    var buffer = await _memoryManager.AllocateAsync(largeBufferSize);
                    buffers.Add(buffer);
                }
                catch (OutOfMemoryException ex)
                {
                    exceptions.Add(ex);
                    break; // Expected behavior under memory pressure
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    throw; // Unexpected exception
                }
            }

            // Assert
            buffers.Should().NotBeEmpty("Should allocate at least some buffers before hitting limits");
            
            if (exceptions.Count > 0)
            {
                exceptions.Should().AllBeOfType<OutOfMemoryException>(
                    "Should only throw OutOfMemoryException under memory pressure");
            }

            // Verify that we can still allocate smaller buffers
            var smallBuffer = await _memoryManager.AllocateAsync(1024);
            smallBuffer.Should().NotBeNull();
            await smallBuffer.DisposeAsync();
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ConcurrentMemoryOperationsHighContention_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 20;
        const int operationsPerThread = 500;
        const long bufferSize = 64 * 1024; // 64KB
        
        var tasks = new Task[threadCount];
        var exceptions = new List<Exception>();
        var totalOperations = 0;
        var successfulOperations = 0;

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks[i] = Task.Run(async () =>
            {
                var threadBuffers = new List<IMemoryBuffer>();
                
                try
                {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        Interlocked.Increment(ref totalOperations);
                        
                        // Mix of operations
                        var operation = (j % 4) switch
                        {
                            0 => "allocate",
                            1 => "copy",
                            2 => "view",
                            _ => "dispose"
                        };

                        switch (operation)
                        {
                            case "allocate":
                                var buffer = await _memoryManager.AllocateAsync(bufferSize);
                                threadBuffers.Add(buffer);
                                break;
                                
                            case "copy":
                                if (threadBuffers.Count > 0)
                                {
                                    var sourceBuffer = threadBuffers[threadId % threadBuffers.Count];
                                    var testData = new byte[1024];
                                    new Random(threadId + j).NextBytes(testData);
                                    
                                    await sourceBuffer.CopyFromHostAsync<byte>(testData);
                                    
                                    var resultData = new byte[1024];
                                    await sourceBuffer.CopyToHostAsync<byte>(resultData);
                                    
                                    resultData.Should().BeEquivalentTo(testData);
                                }
                                break;
                                
                            case "view":
                                if (threadBuffers.Count > 0)
                                {
                                    var sourceBuffer = threadBuffers[threadId % threadBuffers.Count];
                                    var view = _memoryManager.CreateView(sourceBuffer, 0, Math.Min(1024, sourceBuffer.SizeInBytes));
                                    threadBuffers.Add(view);
                                }
                                break;
                                
                            case "dispose":
                                if (threadBuffers.Count > 5) // Keep some buffers
                                {
                                    var bufferToDispose = threadBuffers[0];
                                    threadBuffers.RemoveAt(0);
                                    await bufferToDispose.DisposeAsync();
                                }
                                break;
                        }
                        
                        Interlocked.Increment(ref successfulOperations);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
                finally
                {
                    // Cleanup thread buffers
                    foreach (var buffer in threadBuffers)
                    {
                        try
                        {
                            await buffer.DisposeAsync();
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty($"No exceptions should occur during concurrent operations. Exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
        successfulOperations.Should().Be(totalOperations, "All operations should complete successfully");
        
        // Memory should be properly cleaned up
        var finalAllocatedBytes = _memoryManager.TotalAllocatedBytes;
        finalAllocatedBytes.Should().BeLessOrEqualTo(1024 * 1024, "Most memory should be freed after operations complete");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    [InlineData(long.MinValue)]
    public async Task AllocateAsyncWithInvalidSizes_ShouldThrowArgumentOutOfRangeException(long invalidSize)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _memoryManager.AllocateAsync(invalidSize).AsTask());
    }

    [Fact]
    public async Task CreateViewWithInvalidParameters_ShouldThrowAppropriateExceptions()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);

        try
        {
            // Test invalid offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _memoryManager.CreateView(buffer, -1, 100));
            
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _memoryManager.CreateView(buffer, 2000, 100)); // Offset beyond buffer

            // Test invalid length
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _memoryManager.CreateView(buffer, 0, -1));
            
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _memoryManager.CreateView(buffer, 0, 2000)); // Length beyond buffer

            // Test offset + length exceeding buffer size
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _memoryManager.CreateView(buffer, 500, 600)); // 500 + 600 > 1024
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposedBufferShouldThrowObjectDisposedException()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);
        await buffer.DisposeAsync();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => buffer.SizeInBytes);
        Assert.Throws<ObjectDisposedException>(() => buffer.Flags);
        Assert.Throws<ObjectDisposedException>(() => buffer.GetMemory());
        
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            buffer.CopyFromHostAsync<byte>(new byte[10]));
        
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            buffer.CopyToHostAsync<byte>(new byte[10]));
    }

    [Fact]
    public async Task MemoryManagerWhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var memoryManager = new CpuMemoryManager();
        memoryManager.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            memoryManager.AllocateAsync(1024).AsTask());
    }

    [Fact]
    public async Task LargeBufferOperationsShouldHandleCorrectly()
    {
        // Arrange
        const long largeSize = 256 * 1024 * 1024; // 256MB
        
        try
        {
            var buffer = await _memoryManager.AllocateAsync(largeSize);
            
            try
            {
                // Test basic properties
                buffer.SizeInBytes.Should().Be(largeSize);
                
                // Test partial data operations
                var testData = new byte[1024];
                new Random(42).NextBytes(testData);
                
                await buffer.CopyFromHostAsync<byte>(testData, offset: 0);
                await buffer.CopyFromHostAsync<byte>(testData, offset: largeSize - 1024);
                
                var resultData = new byte[1024];
                await buffer.CopyToHostAsync<byte>(resultData, offset: 0);
                resultData.Should().BeEquivalentTo(testData);
                
                await buffer.CopyToHostAsync<byte>(resultData, offset: largeSize - 1024);
                resultData.Should().BeEquivalentTo(testData);
                
                // Test view creation on large buffer
                var view = _memoryManager.CreateView(buffer, largeSize / 2, 1024);
                view.SizeInBytes.Should().Be(1024);
                await view.DisposeAsync();
            }
            finally
            {
                await buffer.DisposeAsync();
            }
        }
        catch (OutOfMemoryException)
        {
            // Skip test if system doesn't have enough memory
            return;
        }
    }

    [Fact]
    public async Task MemoryAlignmentShouldBeRespected()
    {
        // Arrange & Act
        var buffers = new List<IMemoryBuffer>();
        
        try
        {
            for (int i = 0; i < 100; i++)
            {
                var buffer = await _memoryManager.AllocateAsync(1024);
                buffers.Add(buffer);
                
                // Check memory alignment
                unsafe
                {
                    var memory = buffer.GetMemory();
                    fixed (byte* ptr = memory.Span)
                    {
                        var address = (long)ptr;
                        // Memory should be at least pointer-aligned
                        (address % IntPtr.Size).Should().Be(0, 
                            $"Buffer {i} should be properly aligned. Address: 0x{address:X}");
                    }
                }
            }
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ExtremeCopyOperationsShouldHandleEdgeCases()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(10000);
        
        try
        {
            // Test zero-length copy
            await buffer.CopyFromHostAsync<byte>(Array.Empty<byte>());
            await buffer.CopyToHostAsync<byte>(Array.Empty<byte>());
            
            // Test single byte copy
            await buffer.CopyFromHostAsync<byte>(new byte[] { 42 });
            var result = new byte[1];
            await buffer.CopyToHostAsync<byte>(result);
            result[0].Should().Be(42);
            
            // Test copy at buffer boundaries
            var boundaryData = new byte[] { 1, 2, 3 };
            await buffer.CopyFromHostAsync<byte>(boundaryData, offset: buffer.SizeInBytes - 3);
            
            var boundaryResult = new byte[3];
            await buffer.CopyToHostAsync<byte>(boundaryResult, offset: buffer.SizeInBytes - 3);
            boundaryResult.Should().BeEquivalentTo(boundaryData);
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task MemoryFragmentationUnderVariedWorkload_ShouldMaintainPerformance()
    {
        // Arrange
        const int iterations = 1000;
        var allocationTimes = new List<long>();
        var sizes = new[] { 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
        var random = new Random(42);
        var activeBuffers = new List<IMemoryBuffer>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var size = sizes[random.Next(sizes.Length)];
            var stopwatch = Stopwatch.StartNew();
            
            var buffer = await _memoryManager.AllocateAsync(size);
            stopwatch.Stop();
            
            allocationTimes.Add(stopwatch.ElapsedTicks);
            activeBuffers.Add(buffer);
            
            // Randomly dispose some buffers to create fragmentation
            if (activeBuffers.Count > 50 && random.Next(100) < 30)
            {
                var bufferToDispose = activeBuffers[random.Next(activeBuffers.Count)];
                activeBuffers.Remove(bufferToDispose);
                await bufferToDispose.DisposeAsync();
            }
        }

        // Assert
        var averageAllocationTime = allocationTimes.Average();
        var maxAllocationTime = allocationTimes.Max();
        
        // Allocation times should remain reasonable even under fragmentation
        maxAllocationTime.Should().BeLessThan(averageAllocationTime * 10, 
            "Maximum allocation time should not be excessively higher than average");

        // Cleanup
        foreach (var buffer in activeBuffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task TypedMemoryOperationsShouldHandleAllPrimitiveTypes()
    {
        // Test different primitive types
        await TestTypedOperations<byte>(new byte[] { 1, 2, 3, 255 });
        await TestTypedOperations<sbyte>(new sbyte[] { -128, -1, 0, 1, 127 });
        await TestTypedOperations<short>(new short[] { -32768, -1, 0, 1, 32767 });
        await TestTypedOperations<ushort>(new ushort[] { 0, 1, 32767, 65535 });
        await TestTypedOperations<int>(new int[] { int.MinValue, -1, 0, 1, int.MaxValue });
        await TestTypedOperations<uint>(new uint[] { 0, 1, int.MaxValue, uint.MaxValue });
        await TestTypedOperations<long>(new long[] { long.MinValue, -1, 0, 1, long.MaxValue });
        await TestTypedOperations<ulong>(new ulong[] { 0, 1, long.MaxValue, ulong.MaxValue });
        await TestTypedOperations<float>(new float[] { float.MinValue, -1.5f, 0.0f, 1.5f, float.MaxValue });
        await TestTypedOperations<double>(new double[] { double.MinValue, -1.5, 0.0, 1.5, double.MaxValue });
    }

    private async Task TestTypedOperations<T>(T[] testData) where T : unmanaged
    {
        var buffer = await _memoryManager.AllocateAsync(testData.Length * sizeof(T) * 4);
        
        try
        {
            await buffer.CopyFromHostAsync<T>(testData);
            
            var result = new T[testData.Length];
            await buffer.CopyToHostAsync<T>(result);
            
            result.Should().BeEquivalentTo(testData, $"Type {typeof(T).Name} should copy correctly");
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task MemoryLeakDetectionAfterIntensiveOperations_ShouldShowNoLeaks()
    {
        // Arrange
        var initialMemory = _memoryManager.TotalAllocatedBytes;
        const int operationCycles = 100;

        // Act - Perform intensive memory operations
        for (int cycle = 0; cycle < operationCycles; cycle++)
        {
            var buffers = new List<IMemoryBuffer>();
            
            // Allocate various sizes
            for (int i = 0; i < 20; i++)
            {
                var size = (i + 1) * 1024;
                var buffer = await _memoryManager.AllocateAsync(size);
                buffers.Add(buffer);
            }
            
            // Create views
            var views = new List<IMemoryBuffer>();
            foreach (var buffer in buffers.Take(10))
            {
                var view = _memoryManager.CreateView(buffer, 0, 512);
                views.Add(view);
            }
            
            // Perform operations
            var testData = new byte[256];
            new Random(cycle).NextBytes(testData);
            
            foreach (var buffer in buffers.Where(b => b.SizeInBytes >= 256))
            {
                await buffer.CopyFromHostAsync<byte>(testData);
                var result = new byte[256];
                await buffer.CopyToHostAsync<byte>(result);
            }
            
            // Cleanup all resources
            foreach (var view in views)
            {
                await view.DisposeAsync();
            }
            
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }

        // Assert - No memory leaks
        var finalMemory = _memoryManager.TotalAllocatedBytes;
        finalMemory.Should().Be(initialMemory, "All allocated memory should be freed after operations");
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
    }
}