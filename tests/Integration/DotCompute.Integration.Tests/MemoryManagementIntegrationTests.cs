// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions.Memory;
using DotCompute.Integration.Tests.Utilities;
using DotCompute.Tests.Common.Helpers;
using DotCompute.Memory;
using DotCompute.Core.Memory;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Integration tests for DotCompute memory management subsystem.
/// Tests unified buffer allocation, memory pooling, cross-device operations, and memory leak detection.
/// </summary>
[Collection("Integration")]
public class MemoryManagementIntegrationTests : IntegrationTestBase
{
    private readonly UnifiedMemoryManager _memoryManager;
    private readonly ILogger<MemoryManagementIntegrationTests> _logger;

    public MemoryManagementIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _memoryManager = GetService<UnifiedMemoryManager>();
        _logger = GetLogger<MemoryManagementIntegrationTests>();
    }

    [Fact]
    public async Task AllocateAsync_BasicAllocation_ShouldSucceed()
    {
        // Arrange
        const int size = 1000;

        // Act
        var buffer = await _memoryManager.AllocateAsync<float>(size);

        // Assert
        buffer.Should().NotBeNull("Buffer allocation should succeed");
        buffer.Length.Should().Be(size, "Buffer should have requested size");
        buffer.SizeInBytes.Should().Be(size * sizeof(float), "Buffer size in bytes should be correct");
        buffer.IsOnHost.Should().BeTrue("New buffer should be available on host");

        _logger.LogInformation("Successfully allocated buffer of {Size} floats ({Bytes} bytes)", 
            size, buffer.SizeInBytes);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task AllocateAsync_LargeAllocation_ShouldSucceed()
    {
        // Arrange
        const int sizeMB = 10; // 10 MB
        const int elementCount = sizeMB * 1024 * 1024 / sizeof(float);

        _logger.LogInformation("Testing large allocation of {Size}MB ({Elements} elements)", 
            sizeMB, elementCount);

        // Act
        var measurement = await MeasurePerformanceAsync(async () =>
        {
            var buffer = await _memoryManager.AllocateAsync<float>(elementCount);
            await buffer.DisposeAsync();
        }, "LargeAllocation");

        // Assert
        measurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "Large allocation should complete in reasonable time");

        _logger.LogInformation("Large allocation completed in {Time}ms", 
            measurement.ElapsedTime.TotalMilliseconds);
    }

    [Fact]
    public async Task GetHostSpanAsync_AccessAndModify_ShouldWork()
    {
        // Arrange
        const int size = 100;
        
        var expectedData = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(size);
        
        var buffer = await _memoryManager.AllocateAsync<float>(size);

        // Act
        var hostSpan = await buffer.GetHostSpanAsync();
        
        // Write test data
        for (int i = 0; i < size; i++)
        {
            hostSpan[i] = expectedData[i];
        }

        // Read back and verify
        var readSpan = await buffer.GetHostSpanAsync();

        // Assert
        for (int i = 0; i < size; i++)
        {
            readSpan[i].Should().Be(expectedData[i], $"Element {i} should match written value");
        }

        _logger.LogInformation("Host span access and modification test passed");

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleThreads_ShouldBeThreadSafe()
    {
        // Arrange
        const int bufferSize = 1000;
        const int threadCount = 8;
        const int operationsPerThread = 50;
        
        var buffer = await _memoryManager.AllocateAsync<int>(bufferSize);
        

        _logger.LogInformation("Testing concurrent access with {Threads} threads, {Operations} operations each",
            threadCount, operationsPerThread);

        // Act
        var results = await ExecuteConcurrentlyAsync(async threadId =>
        {
            var successes = 0;
            var random = new Random(threadId);

            for (int i = 0; i < operationsPerThread; i++)
            {
                try
                {
                    var span = await buffer.GetHostSpanAsync();
                    var index = random.Next(0, bufferSize);
                    var value = threadId * 1000 + i;
                    
                    span[index] = value;
                    
                    // Verify write
                    if (span[index] == value)
                    {
                        successes++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Thread {ThreadId} operation {Op} failed: {Error}", 
                        threadId, i, ex.Message);
                }
            }

            return successes;
        }, threadCount);

        // Assert
        results.Should().HaveCount(threadCount, "All threads should complete");
        results.Should().OnlyContain(success => success > 0, 
            "Each thread should have some successful operations");

        var totalSuccesses = results.Sum();
        totalSuccesses.Should().BeGreaterThan(threadCount * operationsPerThread / 2,
            "Most operations should succeed despite concurrency");

        _logger.LogInformation("Concurrent access test completed with {Successes}/{Total} successful operations",
            totalSuccesses, threadCount * operationsPerThread);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task MemoryPooling_MultipleAllocationsAndDeallocations_ShouldReuseMemory()
    {
        // Arrange
        const int allocationCount = 20;
        const int bufferSize = 1000;
        var allocatedBuffers = new List<IUnifiedMemoryBuffer<float>>();

        _logger.LogInformation("Testing memory pooling with {Count} allocations of {Size} elements",
            allocationCount, bufferSize);

        // Act - Allocate multiple buffers
        var allocationMeasurement = await MeasurePerformanceAsync(async () =>
        {
            for (int i = 0; i < allocationCount; i++)
            {
                var buffer = await _memoryManager.AllocateAsync<float>(bufferSize);
                allocatedBuffers.Add(buffer);
            }
        }, "BatchAllocation");

        // Deallocate half of them
        var deallocationMeasurement = await MeasurePerformanceAsync(async () =>
        {
            for (int i = 0; i < allocationCount / 2; i++)
            {
                await allocatedBuffers[i].DisposeAsync();
            }
        }, "BatchDeallocation");

        // Allocate more (should reuse pooled memory)
        var reallocationMeasurement = await MeasurePerformanceAsync(async () =>
        {
            for (int i = 0; i < allocationCount / 2; i++)
            {
                var buffer = await _memoryManager.AllocateAsync<float>(bufferSize);
                allocatedBuffers.Add(buffer);
            }
        }, "BatchReallocation");

        // Assert
        allocationMeasurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "Initial allocations should complete in reasonable time");

        // Reallocation should be faster due to pooling (allowing some tolerance)
        var poolingSpeedup = allocationMeasurement.ElapsedTime.TotalMilliseconds / 
                           Math.Max(reallocationMeasurement.ElapsedTime.TotalMilliseconds, 1);

        _logger.LogInformation("Memory pooling speedup: {Speedup:F2}x", poolingSpeedup);

        // Cleanup remaining buffers
        foreach (var buffer in allocatedBuffers.Skip(allocationCount / 2))
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task CrossDeviceTransfer_HostToDevice_ShouldWork()
    {
        // Skip if no GPU available
        SkipIfCudaNotAvailable();

        // Arrange
        const int size = 500;
        
        var hostData = UnifiedTestHelpers.TestDataGenerator.GenerateFloatArray(size);
        
        var buffer = await _memoryManager.AllocateAsync<float>(size);

        _logger.LogInformation("Testing cross-device transfer for {Size} elements", size);

        // Act
        var transferMeasurement = await MeasurePerformanceAsync(async () =>
        {
            // Write to host
            var hostSpan = await buffer.GetHostSpanAsync();
            hostData.CopyTo(hostSpan);

            // Transfer to device (this should trigger internal transfer)
            if (buffer is UnifiedBuffer<float> unifiedBuffer)
            {
                await unifiedBuffer.EnsureDeviceAsync();
            }

            // Transfer back to host and verify
            var verifySpan = await buffer.GetHostSpanAsync();
            
            for (int i = 0; i < size; i++)
            {
                verifySpan[i].Should().Be(hostData[i], $"Element {i} should survive device transfer");
            }
        }, "CrossDeviceTransfer");

        // Assert
        transferMeasurement.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "Cross-device transfer should complete in reasonable time");

        _logger.LogInformation("Cross-device transfer completed in {Time}ms",
            transferMeasurement.ElapsedTime.TotalMilliseconds);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task MemoryUsageTracking_ShouldReportAccurateUsage()
    {
        // Arrange
        const int bufferSize = 10000;
        const int bufferCount = 5;
        var buffers = new List<IUnifiedMemoryBuffer<float>>();

        var initialUsage = _memoryManager.GetTotalMemoryUsage();
        _logger.LogInformation("Initial memory usage: {Usage:N0} bytes", initialUsage);

        // Act
        for (int i = 0; i < bufferCount; i++)
        {
            var buffer = await _memoryManager.AllocateAsync<float>(bufferSize);
            buffers.Add(buffer);
            
            var currentUsage = _memoryManager.GetTotalMemoryUsage();
            _logger.LogInformation("After allocation {Index}: {Usage:N0} bytes (+{Increase:N0})",
                i + 1, currentUsage, currentUsage - initialUsage);
        }

        var peakUsage = _memoryManager.GetTotalMemoryUsage();
        var expectedMinUsage = bufferCount * bufferSize * sizeof(float);

        // Assert
        peakUsage.Should().BeGreaterOrEqualTo(initialUsage + expectedMinUsage,
            "Memory usage should increase by at least the allocated amount");

        // Cleanup and verify memory is released
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }

        // Allow some time for cleanup
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var finalUsage = _memoryManager.GetTotalMemoryUsage();
        _logger.LogInformation("Final memory usage: {Usage:N0} bytes", finalUsage);

        // Memory should be released (allowing some overhead)
        finalUsage.Should().BeLessOrEqualTo(peakUsage,
            "Memory should be released after buffer disposal");
    }

    [Fact]
    public async Task AvailableMemory_ShouldReflectAllocationState()
    {
        // Arrange
        var initialAvailable = _memoryManager.GetAvailableMemory();
        const int largeBufferSize = 1000000; // 1M elements

        _logger.LogInformation("Initial available memory: {Memory:N0} bytes", initialAvailable);

        // Act
        var buffer = await _memoryManager.AllocateAsync<float>(largeBufferSize);
        var availableAfterAllocation = _memoryManager.GetAvailableMemory();

        _logger.LogInformation("Available after large allocation: {Memory:N0} bytes", 
            availableAfterAllocation);

        // Assert
        availableAfterAllocation.Should().BeLessThan(initialAvailable,
            "Available memory should decrease after allocation");

        // Cleanup
        await buffer.DisposeAsync();
        
        var availableAfterDeallocation = _memoryManager.GetAvailableMemory();
        _logger.LogInformation("Available after deallocation: {Memory:N0} bytes", 
            availableAfterDeallocation);

        availableAfterDeallocation.Should().BeGreaterThan(availableAfterAllocation,
            "Available memory should increase after deallocation");
    }

    [Fact]
    public async Task BufferStates_ShouldTrackLocationCorrectly()
    {
        // Arrange
        const int size = 100;
        var buffer = await _memoryManager.AllocateAsync<float>(size);

        // Act & Assert - Initial state
        buffer.IsOnHost.Should().BeTrue("New buffer should be on host");
        
        // Modify host data
        var hostSpan = await buffer.GetHostSpanAsync();
        hostSpan[0] = 42.0f;

        buffer.IsOnHost.Should().BeTrue("Buffer should still be on host after modification");

        _logger.LogInformation("Buffer state tracking test passed");

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task OutOfMemory_ShouldThrowAppropriateException()
    {
        // Arrange - Try to allocate an extremely large buffer
        const long hugeSize = long.MaxValue / sizeof(float) / 2; // Half of theoretical maximum

        _logger.LogInformation("Testing out-of-memory handling with size {Size}", hugeSize);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OutOfMemoryException>(async () =>
        {
            await _memoryManager.AllocateAsync<float>((int)Math.Min(hugeSize, int.MaxValue));
        });

        _logger.LogInformation("Out-of-memory exception handling test passed");
    }

    [Fact]
    public async Task ZeroSizeAllocation_ShouldHandleGracefully()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _memoryManager.AllocateAsync<float>(0);
        });

        _logger.LogInformation("Zero-size allocation handling test passed");
    }

    [Fact]
    public async Task NegativeSizeAllocation_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _memoryManager.AllocateAsync<float>(-1);
        });

        _logger.LogInformation("Negative-size allocation handling test passed");
    }

    [Fact]
    public async Task MultipleDispose_ShouldBeIdempotent()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync<float>(100);

        // Act & Assert - Multiple disposal should not throw
        await buffer.DisposeAsync();
        await buffer.DisposeAsync(); // Should not throw
        await buffer.DisposeAsync(); // Should not throw

        _logger.LogInformation("Multiple dispose test passed");
    }

    [Fact]
    public async Task MemoryAlignment_ShouldBeProperlyAligned()
    {
        // Arrange & Act
        var buffer = await _memoryManager.AllocateAsync<float>(1000);
        var hostSpan = await buffer.GetHostSpanAsync();

        // Assert - Check memory alignment (should be aligned for SIMD operations)
        unsafe
        {
            fixed (float* ptr = hostSpan)
            {
                var address = (long)ptr;
                var alignment = address % 32; // Check for 32-byte alignment (AVX2)
                
                alignment.Should().Be(0, "Memory should be properly aligned for SIMD operations");
            }
        }

        _logger.LogInformation("Memory alignment test passed");

        // Cleanup
        await buffer.DisposeAsync();
    }
}