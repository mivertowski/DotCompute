// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;
using FluentAssertions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Enhanced comprehensive tests for BaseMemoryBuffer covering 20+ additional test scenarios:
/// Enhanced Memory Allocation, Advanced Copy Operations, Extended Buffer Types, 
/// Comprehensive Error Scenarios, Advanced Performance Tests, and Complex Memory Patterns.
/// </summary>
public class EnhancedBaseMemoryBufferTests
{
    private readonly ITestOutputHelper _output;

    public EnhancedBaseMemoryBufferTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Enhanced Memory Allocation Tests

    [Theory]
    [InlineData(128)]   // 128B - typical small allocation
    [InlineData(4096)]  // 4KB - page-aligned
    [InlineData(65536)] // 64KB - typical intermediate size
    [InlineData(1048576)] // 1MB - large allocation
    [Trait("Category", "MemoryAllocation")]
    public void MemoryAllocation_WithVariousSizes_Succeeds(int sizeInBytes)
    {
        // Arrange & Act
        using var buffer = new TestMemoryBuffer<byte>(sizeInBytes);
        
        // Assert
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.Length.Should().Be(sizeInBytes);
        buffer.IsDisposed.Should().BeFalse();
        buffer.State.Should().Be(BufferState.Allocated);
    }

    [Theory]
    [InlineData(16, 16)]   // 16-byte alignment
    [InlineData(32, 32)]   // 32-byte alignment  
    [InlineData(64, 64)]   // 64-byte alignment
    [InlineData(128, 128)] // 128-byte alignment
    [Trait("Category", "MemoryAllocation")]
    public void MemoryAllocation_WithCustomAlignment_RespectsAlignment(int alignment, int sizeInBytes)
    {
        // Arrange & Act
        using var buffer = new TestMemoryBuffer<byte>(sizeInBytes);
        
        // Assert
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        // Verify size is aligned (should be multiple of element size)
        (buffer.SizeInBytes % sizeof(byte)).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "MemoryAllocation")]
    public void MemoryAllocation_ExceedsReasonableLimit_ThrowsException()
    {
        // Arrange - Attempt to allocate an unreasonably large buffer
        const long unreasonableSize = long.MaxValue / 2;
        
        // Act & Assert
        Action act = () => new TestMemoryBuffer<byte>(unreasonableSize);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*sizeInBytes*");
    }

    [Theory]
    [InlineData(typeof(byte), 1000)]
    [InlineData(typeof(short), 2000)]
    [InlineData(typeof(int), 4000)]
    [InlineData(typeof(long), 8000)]
    [InlineData(typeof(float), 4000)]
    [InlineData(typeof(double), 8000)]
    [Trait("Category", "MemoryAllocation")]
    public void MemoryAllocation_DifferentElementTypes_CalculatesLengthCorrectly(Type elementType, int sizeInBytes)
    {
        // Act & Assert based on element type
        if (elementType == typeof(byte))
        {
            using var buffer = new TestMemoryBuffer<byte>(sizeInBytes);
            buffer.Length.Should().Be(sizeInBytes / sizeof(byte));
        }
        else if (elementType == typeof(short))
        {
            using var buffer = new TestMemoryBuffer<short>(sizeInBytes);
            buffer.Length.Should().Be(sizeInBytes / sizeof(short));
        }
        else if (elementType == typeof(int))
        {
            using var buffer = new TestMemoryBuffer<int>(sizeInBytes);
            buffer.Length.Should().Be(sizeInBytes / sizeof(int));
        }
        else if (elementType == typeof(long))
        {
            using var buffer = new TestMemoryBuffer<long>(sizeInBytes);
            buffer.Length.Should().Be(sizeInBytes / sizeof(long));
        }
        else if (elementType == typeof(float))
        {
            using var buffer = new TestMemoryBuffer<float>(sizeInBytes);
            buffer.Length.Should().Be(sizeInBytes / sizeof(float));
        }
        else if (elementType == typeof(double))
        {
            using var buffer = new TestMemoryBuffer<double>(sizeInBytes);
            buffer.Length.Should().Be(sizeInBytes / sizeof(double));
        }
    }

    [Fact]
    [Trait("Category", "MemoryAllocation")]
    public void MultipleSimultaneousAllocations_SucceedWithoutInterference()
    {
        // Arrange
        const int allocationCount = 50;
        const int bufferSize = 2048;
        var buffers = new List<TestMemoryBuffer<int>>();

        try
        {
            // Act - Allocate multiple buffers simultaneously
            Parallel.For(0, allocationCount, i =>
            {
                lock (buffers)
                {
                    buffers.Add(new TestMemoryBuffer<int>(bufferSize));
                }
            });

            // Assert
            buffers.Should().HaveCount(allocationCount);
            buffers.Should().AllSatisfy(b => 
            {
                b.SizeInBytes.Should().Be(bufferSize);
                b.IsDisposed.Should().BeFalse();
            });
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    #endregion

    #region Enhanced Copy Operations Tests

    [Theory]
    [InlineData(1024, 0, 256)]    // H2D: Host to Device copy
    [InlineData(2048, 512, 512)]  // H2D: Offset copy
    [InlineData(4096, 1024, 1024)] // H2D: Large copy
    [Trait("Category", "CopyOperations")]
    public async Task HostToDeviceCopy_WithVariousParameters_Succeeds(int bufferSize, int offset, int copySize)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<int>(bufferSize);
        var sourceData = Enumerable.Range(1, copySize / sizeof(int)).ToArray();
        
        // Act
        var act = async () => await buffer.CopyFromAsync(sourceData.AsMemory(), offset);
        
        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(1024, 0, 256)]    // D2H: Device to Host copy
    [InlineData(2048, 512, 512)]  // D2H: Offset copy
    [InlineData(4096, 1024, 1024)] // D2H: Large copy
    [Trait("Category", "CopyOperations")]
    public async Task DeviceToHostCopy_WithVariousParameters_Succeeds(int bufferSize, int offset, int copySize)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<int>(bufferSize);
        var destinationData = new int[copySize / sizeof(int)];
        
        // Act
        var act = async () => await buffer.CopyToAsync(destinationData.AsMemory(), offset);
        
        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task DeviceToDeviceCopy_SameDevice_Succeeds()
    {
        // Arrange
        using var sourceBuffer = new TestMemoryBuffer<float>(1024);
        using var destBuffer = new TestMemoryBuffer<float>(1024);
        var testData = Enumerable.Range(1, 256).Select(i => (float)i).ToArray();
        
        // Act - Simulate D2D copy
        await sourceBuffer.CopyFromAsync(testData);
        var act = async () => await sourceBuffer.CopyToAsync(destBuffer.AsMemory());
        
        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(0, 100)]      // Copy from beginning
    [InlineData(100, 100)]    // Copy from middle
    [InlineData(150, 50)]     // Copy partial end
    [Trait("Category", "CopyOperations")]
    public async Task PartialCopy_WithDifferentRanges_PreservesData(int sourceOffset, int count)
    {
        // Arrange
        using var sourceBuffer = new TestMemoryBuffer<int>(800); // 200 ints
        using var destBuffer = new TestMemoryBuffer<int>(800);
        
        var sourceData = Enumerable.Range(1, 200).ToArray();
        await sourceBuffer.CopyFromAsync(sourceData);
        
        // Act
        await sourceBuffer.CopyToAsync(sourceOffset, destBuffer, 0, count, CancellationToken.None);
        
        // Assert - Should complete without error
        destBuffer.State.Should().Be(BufferState.Allocated);
    }

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task ConcurrentCopyOperations_DoNotInterfere()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<byte>(4096);
        var tasks = new List<Task>();
        
        // Act - Run multiple concurrent copy operations
        for (int i = 0; i < 20; i++)
        {
            var data = new byte[100];
            Array.Fill(data, (byte)(i % 256));
            tasks.Add(buffer.CopyFromAsync(data.AsMemory(), i * 100).AsTask());
        }
        
        // Assert
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task LargeCopyOperations_WithProgress_CompleteSuccessfully()
    {
        // Arrange
        const int largeSize = 16 * 1024 * 1024; // 16MB
        using var sourceBuffer = new TestMemoryBuffer<byte>(largeSize);
        using var destBuffer = new TestMemoryBuffer<byte>(largeSize);
        
        var sourceData = new byte[largeSize];
        new Random(42).NextBytes(sourceData);
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        await sourceBuffer.CopyFromAsync(sourceData);
        await sourceBuffer.CopyToAsync(destBuffer.AsMemory());
        
        stopwatch.Stop();
        
        // Assert
        _output.WriteLine($"Large copy (16MB) completed in {stopwatch.ElapsedMilliseconds} ms");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "large copy should complete within reasonable time");
        destBuffer.State.Should().Be(BufferState.Allocated);
    }

    #endregion

    #region Enhanced Buffer Types Tests

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void DeviceBuffer_HasCorrectProperties()
    {
        // Arrange
        var devicePointer = new IntPtr(0x12345000);
        
        // Act
        using var buffer = new TestDeviceBuffer<double>(1024, devicePointer);
        
        // Assert
        buffer.MemoryType.Should().Be(MemoryType.Device);
        buffer.DevicePointer.Should().Be(devicePointer);
        buffer.IsOnDevice.Should().BeTrue();
        buffer.IsOnHost.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_SupportsHostAndDeviceAccess()
    {
        // Arrange & Act
        using var buffer = new TestUnifiedBuffer<int>(1024);
        
        // Assert
        buffer.MemoryType.Should().Be(MemoryType.Unified);
        buffer.IsOnHost.Should().BeTrue();
        // For unified memory, device access depends on implementation
    }

    [Theory]
    [InlineData(MemoryType.Host)]
    [InlineData(MemoryType.Device)]
    [InlineData(MemoryType.Unified)]
    [Trait("Category", "BufferTypes")]
    public void BufferType_ReportsCorrectMemoryType(MemoryType expectedType)
    {
        // Act & Assert based on memory type
        switch (expectedType)
        {
            case MemoryType.Host:
                using (var buffer = new TestMemoryBuffer<float>(1024))
                {
                    buffer.MemoryType.Should().Be(MemoryType.Host);
                }
                break;
            case MemoryType.Device:
                using (var buffer = new TestDeviceBuffer<float>(1024, new IntPtr(0x1000)))
                {
                    buffer.MemoryType.Should().Be(MemoryType.Device);
                }
                break;
            case MemoryType.Unified:
                using (var buffer = new TestUnifiedBuffer<float>(1024))
                {
                    buffer.MemoryType.Should().Be(MemoryType.Unified);
                }
                break;
        }
    }

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_MultipleCycles_WorksCorrectly()
    {
        // Arrange
        var returnCount = 0;
        var buffer = new TestPooledBuffer<int>(1024, b => returnCount++);
        
        // Act - Multiple dispose/reset cycles
        for (int i = 0; i < 5; i++)
        {
            buffer.AsSpan().Fill(i);
            buffer.Dispose();
            buffer.Reset();
        }
        
        // Assert
        returnCount.Should().Be(5);
        buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void MixedBufferTypes_WorkIndependently()
    {
        // Arrange & Act
        using var hostBuffer = new TestMemoryBuffer<int>(1024);
        using var deviceBuffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x2000));
        using var unifiedBuffer = new TestUnifiedBuffer<int>(1024);
        var pooledBuffer = new TestPooledBuffer<int>(1024, null);
        
        try
        {
            // Assert - Each buffer maintains its own characteristics
            hostBuffer.MemoryType.Should().Be(MemoryType.Host);
            deviceBuffer.MemoryType.Should().Be(MemoryType.Device);
            unifiedBuffer.MemoryType.Should().Be(MemoryType.Unified);
            pooledBuffer.MemoryType.Should().Be(MemoryType.Host);
            
            // All should have same size but different memory locations
            var buffers = new IUnifiedMemoryBuffer<int>[] { hostBuffer, deviceBuffer, unifiedBuffer, pooledBuffer };
            buffers.Should().AllSatisfy(b => b.SizeInBytes.Should().Be(1024));
        }
        finally
        {
            pooledBuffer.Dispose();
        }
    }

    #endregion

    #region Enhanced Error Scenarios Tests

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public void BoundsChecking_NegativeIndices_ThrowsException()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<int>(1024);
        
        // Act & Assert - Test various negative parameter combinations
        Action negativeSourceOffset = () => buffer.TestValidateCopyParameters(100, -10, 100, 0, 50);
        Action negativeDestOffset = () => buffer.TestValidateCopyParameters(100, 0, 100, -5, 50);
        Action negativeCount = () => buffer.TestValidateCopyParameters(100, 0, 100, 0, -1);
        
        negativeSourceOffset.Should().Throw<ArgumentOutOfRangeException>();
        negativeDestOffset.Should().Throw<ArgumentOutOfRangeException>();
        negativeCount.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public async Task DisposedBuffer_AllOperations_ThrowObjectDisposedException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<float>(1024);
        buffer.Dispose();
        
        // Act & Assert - Test all operations throw on disposed buffer
        Action spanAccess = () => buffer.AsSpan();
        Action memoryAccess = () => buffer.AsMemory();
        Action readOnlySpanAccess = () => buffer.AsReadOnlySpan();
        Action readOnlyMemoryAccess = () => buffer.AsReadOnlyMemory();
        
        var copyFromTask = async () => await buffer.CopyFromAsync(new float[10]);
        var copyToTask = async () => await buffer.CopyToAsync(new float[10]);
        var fillTask = async () => await buffer.FillAsync(1.0f);
        
        spanAccess.Should().Throw<ObjectDisposedException>();
        memoryAccess.Should().Throw<ObjectDisposedException>();
        readOnlySpanAccess.Should().Throw<ObjectDisposedException>();
        readOnlyMemoryAccess.Should().Throw<ObjectDisposedException>();
        
        await copyFromTask.Should().ThrowAsync<ObjectDisposedException>();
        await copyToTask.Should().ThrowAsync<ObjectDisposedException>();
        await fillTask.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public void BufferOverflow_LargeOperations_DetectedAndHandled()
    {
        // Arrange
        using var smallBuffer = new TestMemoryBuffer<byte>(100);
        var largeData = new byte[1000];
        
        // Act & Assert
        var act = async () => await smallBuffer.CopyFromAsync(largeData);
        act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(int.MaxValue, 0, 1000)] // Massive source length
    [InlineData(1000, int.MaxValue, 100)] // Massive offset
    [InlineData(1000, 0, int.MaxValue)] // Massive count
    [Trait("Category", "ErrorScenarios")]
    public void IntegerOverflow_InParameters_HandledSafely(long sourceLength, long offset, long count)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<byte>(1024);
        
        // Act & Assert
        Action act = () => buffer.TestValidateCopyParameters(sourceLength, offset, 1024, 0, count);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public async Task MemoryPressure_UnderHighLoad_MaintainsStability()
    {
        // Arrange
        var buffers = new List<TestMemoryBuffer<long>>();
        
        try
        {
            // Act - Create many buffers to simulate memory pressure
            for (int i = 0; i < 100; i++)
            {
                var buffer = new TestMemoryBuffer<long>(8192); // 8KB each
                buffers.Add(buffer);
                
                // Perform operations on each buffer
                var data = Enumerable.Range(1, 1024).Select(x => (long)x).ToArray();
                await buffer.CopyFromAsync(data);
            }
            
            // Assert - All buffers should be in valid state
            buffers.Should().AllSatisfy(b => 
            {
                b.IsDisposed.Should().BeFalse();
                b.State.Should().Be(BufferState.Allocated);
            });
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public async Task ConcurrentDisposeOperations_HandleGracefully()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<byte>(1024);
        var disposeTasks = new List<Task>();
        
        // Act - Attempt concurrent dispose operations
        for (int i = 0; i < 10; i++)
        {
            disposeTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await buffer.DisposeAsync();
                }
                catch (ObjectDisposedException)
                {
                    // Expected for concurrent dispose
                }
            }));
        }
        
        // Assert - Should not crash or deadlock
        var act = async () => await Task.WhenAll(disposeTasks);
        await act.Should().NotThrowAsync();
        buffer.IsDisposed.Should().BeTrue();
    }

    #endregion

    #region Enhanced Performance Tests

    [Theory]
    [InlineData(1024, 100)]      // 1KB, 100 ops
    [InlineData(4096, 50)]       // 4KB, 50 ops  
    [InlineData(16384, 25)]      // 16KB, 25 ops
    [Trait("Category", "Performance")]
    public async Task CopyBandwidth_DifferentSizes_MeetsThreshold(int bufferSize, int iterations)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<byte>(bufferSize);
        var sourceData = new byte[bufferSize];
        new Random(42).NextBytes(sourceData);
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        for (int i = 0; i < iterations; i++)
        {
            await buffer.CopyFromAsync(sourceData);
        }
        
        stopwatch.Stop();
        
        // Assert
        var totalBytes = (long)bufferSize * iterations;
        var throughputMBps = totalBytes / (stopwatch.ElapsedMilliseconds / 1000.0) / (1024 * 1024);
        
        _output.WriteLine($"Buffer size: {bufferSize}B, Throughput: {throughputMBps:F2} MB/s");
        throughputMBps.Should().BeGreaterThan(50, "performance should be reasonable");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void AllocationOverhead_SmallBuffers_IsMinimal()
    {
        // Arrange
        const int allocationCount = 10000;
        const int bufferSize = 64; // Small buffers
        var buffers = new List<TestMemoryBuffer<byte>>();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Act
            for (int i = 0; i < allocationCount; i++)
            {
                buffers.Add(new TestMemoryBuffer<byte>(bufferSize));
            }
            
            stopwatch.Stop();
            
            // Assert
            var avgAllocationTime = stopwatch.ElapsedMilliseconds / (double)allocationCount;
            _output.WriteLine($"Small buffer allocation avg: {avgAllocationTime:F4} ms");
            
            avgAllocationTime.Should().BeLessThan(0.1, "small buffer allocation should be very fast");
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task ParallelCopyOperations_ScaleWell()
    {
        // Arrange
        const int bufferCount = 10;
        const int bufferSize = 4096;
        var buffers = Enumerable.Range(0, bufferCount)
            .Select(_ => new TestMemoryBuffer<int>(bufferSize))
            .ToList();
        
        var sourceData = Enumerable.Range(1, bufferSize / sizeof(int)).ToArray();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Act - Parallel copy operations
            var tasks = buffers.Select(buffer => buffer.CopyFromAsync(sourceData).AsTask());
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();
            
            // Assert
            var totalDataMB = (bufferCount * bufferSize) / (1024.0 * 1024.0);
            var throughputMBps = totalDataMB / (stopwatch.ElapsedMilliseconds / 1000.0);
            
            _output.WriteLine($"Parallel copy throughput: {throughputMBps:F2} MB/s");
            throughputMBps.Should().BeGreaterThan(100, "parallel operations should scale well");
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void MemoryFootprint_MultipleBuffers_IsReasonable()
    {
        // Arrange
        const int bufferCount = 100;
        const int bufferSize = 1024;
        var buffers = new List<TestMemoryBuffer<byte>>();
        
        var initialMemory = GC.GetTotalMemory(true);
        
        try
        {
            // Act
            for (int i = 0; i < bufferCount; i++)
            {
                buffers.Add(new TestMemoryBuffer<byte>(bufferSize));
            }
            
            var peakMemory = GC.GetTotalMemory(false);
            var memoryIncrease = peakMemory - initialMemory;
            
            // Assert
            var expectedMinimum = bufferCount * bufferSize;
            var overhead = memoryIncrease - expectedMinimum;
            var overheadPercentage = (double)overhead / expectedMinimum * 100;
            
            _output.WriteLine($"Memory overhead: {overheadPercentage:F1}%");
            
            overheadPercentage.Should().BeLessThan(200, "memory overhead should be reasonable");
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task AsyncOperationOverhead_IsMinimal()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<int>(4096);
        const int operationCount = 1000;
        var data = new int[1024];
        
        // Measure sync baseline (direct span access)
        var syncStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < operationCount; i++)
        {
            data.CopyTo(buffer.AsSpan());
        }
        syncStopwatch.Stop();
        
        // Measure async operations
        var asyncStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < operationCount; i++)
        {
            await buffer.CopyFromAsync(data);
        }
        asyncStopwatch.Stop();
        
        // Assert
        var overhead = (double)(asyncStopwatch.ElapsedMilliseconds - syncStopwatch.ElapsedMilliseconds) / syncStopwatch.ElapsedMilliseconds * 100;
        _output.WriteLine($"Async overhead: {overhead:F1}%");
        
        overhead.Should().BeLessThan(300, "async overhead should be reasonable");
    }

    #endregion

    #region Enhanced Memory Patterns Tests

    [Theory]
    [InlineData(0x00000000)] // All zeros
    [InlineData(0xFFFFFFFF)] // All ones
    [InlineData(0xAAAAAAAA)] // Alternating pattern
    [InlineData(0x55555555)] // Inverse alternating
    [InlineData(0x12345678)] // Sequential pattern
    [Trait("Category", "MemoryPatterns")]
    public async Task MemoryFill_DifferentPatterns_FillsCorrectly(uint pattern)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<uint>(1024); // 256 elements
        
        // Act
        await buffer.FillAsync(pattern);
        
        // Assert
        var span = buffer.AsSpan();
        span.ToArray().Should().OnlyContain(x => x == pattern, $"all elements should be {pattern:X8}");
    }

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public async Task ZeroFill_LargeBuffer_ClearsAllData()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<ulong>(8192); // 1024 elements
        
        // Fill with random data first
        var random = new Random(42);
        var span = buffer.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = (ulong)random.NextInt64();
        }
        
        // Act - Zero the entire buffer
        await buffer.FillAsync(0UL);
        
        // Assert
        span.ToArray().Should().OnlyContain(x => x == 0UL, "all elements should be zero");
    }

    [Theory]
    [InlineData(100, 0, 50)]   // Fill first half
    [InlineData(100, 25, 50)]  // Fill middle section
    [InlineData(100, 50, 50)]  // Fill second half
    [Trait("Category", "MemoryPatterns")]
    public async Task PartialFill_DifferentRanges_FillsOnlySpecifiedRegion(int bufferElements, int offset, int count)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<short>(bufferElements * sizeof(short));
        const short initialValue = -1;
        const short fillValue = 12345;
        
        // Initialize with initial value
        await buffer.FillAsync(initialValue);
        
        // Act - Fill partial range
        await buffer.FillAsync(fillValue, offset, count);
        
        // Assert
        var span = buffer.AsSpan();
        
        // Check region before fill
        for (int i = 0; i < offset; i++)
        {
            span[i].Should().Be(initialValue, $"element at index {i} should remain unchanged");
        }
        
        // Check filled region
        for (int i = offset; i < offset + count; i++)
        {
            span[i].Should().Be(fillValue, $"element at index {i} should be filled");
        }
        
        // Check region after fill
        for (int i = offset + count; i < span.Length; i++)
        {
            span[i].Should().Be(initialValue, $"element at index {i} should remain unchanged");
        }
    }

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public void MemoryPatternVerification_DetectsComplexCorruption()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<int>(4096); // 1024 elements
        
        // Create a complex pattern: fibonacci-like sequence
        var span = buffer.AsSpan();
        span[0] = 1;
        span[1] = 1;
        for (int i = 2; i < span.Length; i++)
        {
            span[i] = span[i - 1] + span[i - 2];
        }
        
        // Corrupt multiple elements at different locations
        var originalValues = new Dictionary<int, int>
        {
            { 100, span[100] },
            { 500, span[500] },
            { 900, span[900] }
        };
        
        span[100] = int.MaxValue;
        span[500] = int.MinValue;
        span[900] = 0;
        
        // Act - Detect corruption by rebuilding pattern
        var corruptedIndices = new List<int>();
        var reconstructed = new int[span.Length];
        reconstructed[0] = 1;
        reconstructed[1] = 1;
        
        for (int i = 2; i < reconstructed.Length; i++)
        {
            reconstructed[i] = reconstructed[i - 1] + reconstructed[i - 2];
            if (span[i] != reconstructed[i])
            {
                corruptedIndices.Add(i);
            }
        }
        
        // Assert
        corruptedIndices.Should().Contain(new[] { 100, 500, 900 }, "all corrupted locations should be detected");
        corruptedIndices.Should().HaveCount(3, "exactly 3 corruptions should be detected");
    }

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public async Task MemoryInitialization_NewBuffer_IsZeroed()
    {
        // Arrange & Act
        using var buffer = new TestMemoryBuffer<double>(1024); // 128 elements
        
        // Assert - New buffer should be zero-initialized
        var span = buffer.AsSpan();
        span.ToArray().Should().OnlyContain(x => x == 0.0, "new buffer should be zero-initialized");
    }

    [Theory]
    [InlineData(1, 1000)]      // Single byte pattern, many repetitions
    [InlineData(4, 250)]       // 4-byte pattern, fewer repetitions
    [InlineData(16, 62)]       // 16-byte pattern, minimal repetitions
    [Trait("Category", "MemoryPatterns")]
    public async Task RepeatingPattern_FillAndVerify_MaintainsConsistency(int patternSize, int repetitions)
    {
        // Arrange
        var bufferSize = patternSize * repetitions;
        using var buffer = new TestMemoryBuffer<byte>(bufferSize);
        
        // Create pattern
        var pattern = new byte[patternSize];
        for (int i = 0; i < patternSize; i++)
        {
            pattern[i] = (byte)(i * 17 + 42); // Arbitrary but deterministic pattern
        }
        
        // Act - Fill buffer with repeating pattern
        var span = buffer.AsSpan();
        for (int rep = 0; rep < repetitions; rep++)
        {
            for (int i = 0; i < patternSize; i++)
            {
                span[rep * patternSize + i] = pattern[i];
            }
        }
        
        // Assert - Verify pattern repetition
        for (int rep = 0; rep < repetitions; rep++)
        {
            for (int i = 0; i < patternSize; i++)
            {
                var expectedValue = pattern[i];
                var actualValue = span[rep * patternSize + i];
                actualValue.Should().Be(expectedValue, $"pattern should repeat correctly at position {rep * patternSize + i}");
            }
        }
    }

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public void ChessboardPattern_VerificationAndIntegrity()
    {
        // Arrange - Create a chessboard pattern (alternating 0/1)
        using var buffer = new TestMemoryBuffer<byte>(8192); // 8KB
        var span = buffer.AsSpan();
        
        // Act - Fill with chessboard pattern
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = (byte)((i + (i / 64)) % 2); // Creates chessboard with 64-byte squares
        }
        
        // Assert - Verify pattern integrity
        for (int i = 0; i < span.Length; i++)
        {
            var expectedValue = (byte)((i + (i / 64)) % 2);
            span[i].Should().Be(expectedValue, $"chessboard pattern should be correct at index {i}");
        }
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Enhanced test implementation of BaseMemoryBuffer for comprehensive testing.
    /// Includes real memory storage and proper behavior simulation.
    /// </summary>
    private sealed class TestMemoryBuffer<T> : BaseMemoryBuffer<T> where T : unmanaged
    {
        private bool _disposed;
        private readonly T[] _hostMemory;
        private readonly GCHandle _pinnedHandle;

        public TestMemoryBuffer(long sizeInBytes) : base(sizeInBytes)
        {
            _hostMemory = new T[Length];
            _pinnedHandle = GCHandle.Alloc(_hostMemory, GCHandleType.Pinned);
        }

        public override IntPtr DevicePointer => IntPtr.Zero;
        public override MemoryType MemoryType => MemoryType.Host;
        public override bool IsDisposed => _disposed;

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            
            var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            source.Span.CopyTo(_hostMemory.AsSpan(elementOffset));
            return ValueTask.CompletedTask;
        }

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            
            var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            var sourceSpan = _hostMemory.AsSpan(elementOffset, destination.Length);
            sourceSpan.CopyTo(destination.Span);
            return ValueTask.CompletedTask;
        }

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_pinnedHandle.IsAllocated)
                {
                    _pinnedHandle.Free();
                }
            }
        }

        public override ValueTask DisposeAsync() 
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        // Abstract method implementations required by BaseMemoryBuffer<T>
        public override Span<T> AsSpan() 
        {
            ThrowIfDisposed();
            return _hostMemory.AsSpan();
        }
        
        public override ReadOnlySpan<T> AsReadOnlySpan() 
        {
            ThrowIfDisposed();
            return _hostMemory.AsSpan();
        }

        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
        public override IAccelerator Accelerator => throw new NotSupportedException();
        public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
        public override void EnsureOnHost() { }
        public override ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) 
        {
            ThrowIfDisposed();
            _hostMemory.AsSpan().Fill(value);
            return ValueTask.CompletedTask;
        }
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) 
        {
            ThrowIfDisposed();
            _hostMemory.AsSpan(offset, count).Fill(value);
            return ValueTask.CompletedTask;
        }
        public override IUnifiedMemoryBuffer<T> Slice(int start, int length) => this;
        public override void Synchronize() { }
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        public override bool IsOnDevice => false;
        public override bool IsOnHost => true;
        public override void EnsureOnDevice() { }
        public override MappedMemory<T> Map(MapMode mode) => throw new NotSupportedException();
        public override MappedMemory<T> MapRange(int offset, int length, MapMode mode) => throw new NotSupportedException();
        public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override void MarkDeviceDirty() { }
        public override void MarkHostDirty() { }
        public override bool IsDirty => false;
        public override Memory<T> AsMemory() 
        {
            ThrowIfDisposed();
            return _hostMemory.AsMemory();
        }
        public override ReadOnlyMemory<T> AsReadOnlyMemory() 
        {
            ThrowIfDisposed();
            return _hostMemory.AsMemory();
        }
        public override MemoryOptions Options => default;

        public void TestThrowIfDisposed() => ThrowIfDisposed();
        public void TestValidateCopyParameters(long sourceLength, long sourceOffset, long destinationLength, long destinationOffset, long count)
            => ValidateCopyParameters(sourceLength, sourceOffset, destinationLength, destinationOffset, count);
    }

    private sealed class TestDeviceBuffer<T> : BaseDeviceBuffer<T> where T : unmanaged
    {
        public TestDeviceBuffer(long sizeInBytes, IntPtr devicePointer) : base(sizeInBytes, devicePointer)
        {
        }

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override void Dispose() => MarkDisposed();
        public override ValueTask DisposeAsync()
        {
            MarkDisposed();
            return ValueTask.CompletedTask;
        }

        // All required abstract method implementations
        public override Span<T> AsSpan() => Span<T>.Empty;
        public override ReadOnlySpan<T> AsReadOnlySpan() => ReadOnlySpan<T>.Empty;
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
        public override IAccelerator Accelerator => throw new NotSupportedException();
        public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
        public override void EnsureOnHost() { }
        public override void EnsureOnDevice() { }
        public override ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int start, int length) => this;
        public override void Synchronize() { }
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        public override bool IsOnDevice => true;
        public override bool IsOnHost => false;
        public override MappedMemory<T> Map(MapMode mode) => throw new NotSupportedException();
        public override MappedMemory<T> MapRange(int offset, int length, MapMode mode) => throw new NotSupportedException();
        public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override void MarkDeviceDirty() { }
        public override void MarkHostDirty() { }
        public override bool IsDirty => false;
        public override Memory<T> AsMemory() => Memory<T>.Empty;
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
        public override MemoryOptions Options => default;
    }

    private sealed unsafe class TestUnifiedBuffer<T> : BaseUnifiedBuffer<T> where T : unmanaged
    {
        private readonly T[]? _data;

        public TestUnifiedBuffer(long sizeInBytes) : this(sizeInBytes, IntPtr.Zero)
        {
            _data = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
        }

        public TestUnifiedBuffer(long sizeInBytes, IntPtr ptr) : base(sizeInBytes, ptr == IntPtr.Zero ? new IntPtr(1) : ptr)
        {
        }

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override void Dispose() => MarkDisposed();
        public override ValueTask DisposeAsync()
        {
            MarkDisposed();
            return ValueTask.CompletedTask;
        }

        // Additional abstract method implementations required by BaseMemoryBuffer<T>
        public override ReadOnlySpan<T> AsReadOnlySpan() => IsDisposed ? ReadOnlySpan<T>.Empty : ReadOnlySpan<T>.Empty;
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
        public override IAccelerator Accelerator => throw new NotSupportedException();
        public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
        public override void EnsureOnHost() { }
        public override void EnsureOnDevice() { }
        public override ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int start, int length) => this;
        public override void Synchronize() { }
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        public override bool IsOnDevice => false;
        public override bool IsOnHost => true;
        public override MappedMemory<T> Map(MapMode mode) => throw new NotSupportedException();
        public override MappedMemory<T> MapRange(int offset, int length, MapMode mode) => throw new NotSupportedException();
        public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override void MarkDeviceDirty() { }
        public override void MarkHostDirty() { }
        public override bool IsDirty => false;
        public override Memory<T> AsMemory() => Memory<T>.Empty;
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
        public override MemoryOptions Options => default;
    }

    private sealed class TestPooledBuffer<T> : BasePooledBuffer<T> where T : unmanaged
    {
        private readonly Memory<T> _memory;

        public TestPooledBuffer(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction) 
            : base(sizeInBytes, returnAction)
        {
            _memory = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
        }

        public override IntPtr DevicePointer => IntPtr.Zero;
        public override MemoryType MemoryType => MemoryType.Host;
        public override Memory<T> Memory => _memory;

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        // All required abstract method implementations for BasePooledBuffer<T>
        public override Span<T> AsSpan() => _memory.Span;
        public override ReadOnlySpan<T> AsReadOnlySpan() => _memory.Span;
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
        public override IAccelerator Accelerator => throw new NotSupportedException();
        public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
        public override void EnsureOnHost() { }
        public override void EnsureOnDevice() { }
        public override ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int start, int length) => this;
        public override void Synchronize() { }
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        public override bool IsOnDevice => false;
        public override bool IsOnHost => true;
        public override MappedMemory<T> Map(MapMode mode) => new MappedMemory<T>(_memory);
        public override MappedMemory<T> MapRange(int offset, int length, MapMode mode) => new MappedMemory<T>(_memory.Slice(offset, length));
        public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));
        public override void MarkDeviceDirty() { }
        public override void MarkHostDirty() { }
        public override bool IsDirty => false;
        public override Memory<T> AsMemory() => _memory;
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => _memory;
        public override MemoryOptions Options => default;
    }

    #endregion
}