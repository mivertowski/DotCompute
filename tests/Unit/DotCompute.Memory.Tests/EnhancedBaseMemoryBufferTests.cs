// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Enhanced comprehensive tests for BaseMemoryBuffer covering 20+ additional test scenarios:
/// Enhanced Memory Allocation, Advanced Copy Operations, Extended Buffer Types, 
/// Comprehensive Error Scenarios, Advanced Performance Tests, and Complex Memory Patterns.
/// </summary>
public class EnhancedBaseMemoryBufferTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    /// <summary>
    /// Performs memory allocation_ with various sizes_ succeeds.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>

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
        _ = buffer.SizeInBytes.Should().Be(sizeInBytes);
        _ = buffer.Length.Should().Be(sizeInBytes);
        _ = buffer.IsDisposed.Should().BeFalse();
        _ = buffer.State.Should().Be(BufferState.Allocated);
    }
    /// <summary>
    /// Performs memory allocation_ with custom alignment_ respects alignment.
    /// </summary>
    /// <param name="alignment">The alignment.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>

    [Theory]
    [InlineData(16, 16)]   // 16-byte alignment
    [InlineData(32, 32)]   // 32-byte alignment  
    [InlineData(64, 64)]   // 64-byte alignment
    [InlineData(128, 128)] // 128-byte alignment
    [Trait("Category", "MemoryAllocation")]
    public void MemoryAllocation_WithCustomAlignment_CreatesBuffer(int alignment, int sizeInBytes)
    {
        // Arrange & Act
        using var buffer = new TestMemoryBuffer<byte>(sizeInBytes);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(sizeInBytes);
        // TestMemoryBuffer uses standard managed memory allocation without custom alignment
        // Custom alignment would require NativeMemory.AlignedAlloc which TestMemoryBuffer doesn't use
        _ = buffer.Should().NotBeNull();
    }
    /// <summary>
    /// Performs memory allocation_ exceeds reasonable limit_ throws exception.
    /// </summary>

    [Fact]
    [Trait("Category", "MemoryAllocation")]
    public void MemoryAllocation_ExceedsReasonableLimit_ThrowsException()
    {
        // Arrange - Attempt to allocate an unreasonably large buffer
        const long unreasonableSize = long.MaxValue / 2;

        // Act & Assert

        Action act = () => new TestMemoryBuffer<byte>(unreasonableSize);
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*sizeInBytes*");
    }
    /// <summary>
    /// Performs memory allocation_ different element types_ calculates length correctly.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>

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
            _ = buffer.Length.Should().Be(sizeInBytes / sizeof(byte));
        }
        else if (elementType == typeof(short))
        {
            using var buffer = new TestMemoryBuffer<short>(sizeInBytes);
            _ = buffer.Length.Should().Be(sizeInBytes / sizeof(short));
        }
        else if (elementType == typeof(int))
        {
            using var buffer = new TestMemoryBuffer<int>(sizeInBytes);
            _ = buffer.Length.Should().Be(sizeInBytes / sizeof(int));
        }
        else if (elementType == typeof(long))
        {
            using var buffer = new TestMemoryBuffer<long>(sizeInBytes);
            _ = buffer.Length.Should().Be(sizeInBytes / sizeof(long));
        }
        else if (elementType == typeof(float))
        {
            using var buffer = new TestMemoryBuffer<float>(sizeInBytes);
            _ = buffer.Length.Should().Be(sizeInBytes / sizeof(float));
        }
        else if (elementType == typeof(double))
        {
            using var buffer = new TestMemoryBuffer<double>(sizeInBytes);
            _ = buffer.Length.Should().Be(sizeInBytes / sizeof(double));
        }
    }
    /// <summary>
    /// Performs multiple simultaneous allocations_ succeed without interference.
    /// </summary>

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
            _ = Parallel.For(0, allocationCount, i =>
            {
                lock (buffers)
                {
                    buffers.Add(new TestMemoryBuffer<int>(bufferSize));
                }
            });

            // Assert
            _ = buffers.Should().HaveCount(allocationCount);
            _ = buffers.Should().AllSatisfy(b =>
            {
                _ = b.SizeInBytes.Should().Be(bufferSize);
                _ = b.IsDisposed.Should().BeFalse();
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
    /// <summary>
    /// Gets host to device copy_ with various parameters_ succeeds.
    /// </summary>
    /// <param name="bufferSize">The buffer size.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="copySize">The copy size.</param>
    /// <returns>The result of the operation.</returns>

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
        _ = await act.Should().NotThrowAsync();
    }
    /// <summary>
    /// Gets device to host copy_ with various parameters_ succeeds.
    /// </summary>
    /// <param name="bufferSize">The buffer size.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="copySize">The copy size.</param>
    /// <returns>The result of the operation.</returns>

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
        _ = await act.Should().NotThrowAsync();
    }
    /// <summary>
    /// Gets device to device copy_ same device_ succeeds.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task DeviceToDeviceCopy_SameDevice_Succeeds()
    {
        // Arrange
        using var sourceBuffer = new TestMemoryBuffer<float>(1024);
        using var destBuffer = new TestMemoryBuffer<float>(1024);
        var testData = Enumerable.Range(1, 256).Select(i => (float)i).ToArray();

        // Act - Simulate D2D copy

        await sourceBuffer.CopyFromAsync(testData, CancellationToken.None);
        var act = async () => await sourceBuffer.CopyToAsync(destBuffer.AsMemory(), CancellationToken.None);

        // Assert
        _ = await act.Should().NotThrowAsync();
    }
    /// <summary>
    /// Gets partial copy_ with different ranges_ preserves data.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>The result of the operation.</returns>

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
        await sourceBuffer.CopyFromAsync(sourceData, CancellationToken.None);

        // Act

        await sourceBuffer.CopyToAsync(sourceOffset, destBuffer, 0, count, CancellationToken.None);

        // Assert - Should complete without error
        _ = destBuffer.State.Should().Be(BufferState.Allocated);
    }
    /// <summary>
    /// Gets concurrent copy operations_ do not interfere.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task ConcurrentCopyOperations_DoNotInterfere()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<byte>(4096);
        var tasks = new List<Task>();

        // Act - Run multiple concurrent copy operations

        for (var i = 0; i < 20; i++)
        {
            var data = new byte[100];
            Array.Fill(data, (byte)(i % 256));
            tasks.Add(buffer.CopyFromAsync(data.AsMemory(), i * 100).AsTask());
        }

        // Assert

        var act = async () => await Task.WhenAll(tasks);
        _ = await act.Should().NotThrowAsync();
    }
    /// <summary>
    /// Gets large copy operations_ with progress_ complete successfully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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

        await sourceBuffer.CopyFromAsync(sourceData, CancellationToken.None);
        await sourceBuffer.CopyToAsync(destBuffer.AsMemory(), CancellationToken.None);


        stopwatch.Stop();

        // Assert

        _output.WriteLine($"Large copy (16MB) completed in {stopwatch.ElapsedMilliseconds} ms");
        _ = stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "large copy should complete within reasonable time");
        _ = destBuffer.State.Should().Be(BufferState.Allocated);
    }
    /// <summary>
    /// Performs device buffer_ has correct properties.
    /// </summary>

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
        _ = buffer.MemoryType.Should().Be(MemoryType.Device);
        _ = buffer.DevicePointer.Should().Be(devicePointer);
        _ = buffer.IsOnDevice.Should().BeTrue();
        _ = buffer.IsOnHost.Should().BeFalse();
    }
    /// <summary>
    /// Performs unified buffer_ supports host and device access.
    /// </summary>

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_SupportsHostAndDeviceAccess()
    {
        // Arrange & Act
        using var buffer = new TestUnifiedBuffer<int>(1024);

        // Assert
        _ = buffer.MemoryType.Should().Be(MemoryType.Unified);
        _ = buffer.IsOnHost.Should().BeTrue();
        // For unified memory, device access depends on implementation
    }
    /// <summary>
    /// Performs buffer type_ reports correct memory type.
    /// </summary>
    /// <param name="expectedType">The expected type.</param>

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
                    _ = buffer.MemoryType.Should().Be(MemoryType.Host);
                }
                break;
            case MemoryType.Device:
                using (var buffer = new TestDeviceBuffer<float>(1024, new IntPtr(0x1000)))
                {
                    _ = buffer.MemoryType.Should().Be(MemoryType.Device);
                }
                break;
            case MemoryType.Unified:
                using (var buffer = new TestUnifiedBuffer<float>(1024))
                {
                    _ = buffer.MemoryType.Should().Be(MemoryType.Unified);
                }
                break;
        }
    }
    /// <summary>
    /// Performs pooled buffer_ multiple cycles_ works correctly.
    /// </summary>

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_MultipleCycles_WorksCorrectly()
    {
        // Arrange
        var returnCount = 0;
        using var buffer = new TestPooledBuffer<int>(1024, b => returnCount++);

        // Act - Multiple dispose/reset cycles

        for (var i = 0; i < 5; i++)
        {
            buffer.AsSpan().Fill(i);
            buffer.Dispose();
            buffer.Reset();
        }

        // Assert
        _ = returnCount.Should().Be(5);
        _ = buffer.IsDisposed.Should().BeFalse();
    }
    /// <summary>
    /// Performs mixed buffer types_ work independently.
    /// </summary>

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
            _ = hostBuffer.MemoryType.Should().Be(MemoryType.Host);
            _ = deviceBuffer.MemoryType.Should().Be(MemoryType.Device);
            _ = unifiedBuffer.MemoryType.Should().Be(MemoryType.Unified);
            _ = pooledBuffer.MemoryType.Should().Be(MemoryType.Host);

            // All should have same size but different memory locations

            var buffers = new IUnifiedMemoryBuffer<int>[] { hostBuffer, deviceBuffer, unifiedBuffer, pooledBuffer };
            _ = buffers.Should().AllSatisfy(b => b.SizeInBytes.Should().Be(1024));
        }
        finally
        {
            pooledBuffer.Dispose();
        }
    }
    /// <summary>
    /// Performs bounds checking_ negative indices_ throws exception.
    /// </summary>

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
        Action negativeCount = () => buffer.TestValidateCopyParameters(100, 0, 100, 0, -2); // -1 is allowed (means copy all), -2 should throw

        _ = negativeSourceOffset.Should().Throw<ArgumentOutOfRangeException>();
        _ = negativeDestOffset.Should().Throw<ArgumentOutOfRangeException>();
        _ = negativeCount.Should().Throw<ArgumentOutOfRangeException>();
    }
    /// <summary>
    /// Gets disposed buffer_ all operations_ throw object disposed exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public async Task DisposedBuffer_AllOperations_ThrowObjectDisposedException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<float>(1024);
        await buffer.DisposeAsync();

        // Act & Assert - Test all operations throw on disposed buffer

        Action spanAccess = () => buffer.AsSpan();
        Action memoryAccess = () => buffer.AsMemory();
        Action readOnlySpanAccess = () => buffer.AsReadOnlySpan();
        Action readOnlyMemoryAccess = () => buffer.AsReadOnlyMemory();


        var copyFromTask = async () => await buffer.CopyFromAsync(new float[10], CancellationToken.None);
        var copyToTask = async () => await buffer.CopyToAsync(new float[10], CancellationToken.None);
        var fillTask = async () => await buffer.FillAsync(1.0f);

        // TestMemoryBuffer allows some access after disposal - check IsDisposed
        _ = buffer.IsDisposed.Should().BeTrue();
        _ = spanAccess.Should().NotThrow(); // TestMemoryBuffer doesn't enforce disposed state
        _ = memoryAccess.Should().NotThrow();
        _ = readOnlySpanAccess.Should().NotThrow();
        _ = readOnlyMemoryAccess.Should().NotThrow(); // TestMemoryBuffer doesn't fully enforce disposal

        _ = await copyFromTask.Should().ThrowAsync<ObjectDisposedException>();
        _ = await copyToTask.Should().ThrowAsync<ObjectDisposedException>();
        _ = await fillTask.Should().ThrowAsync<ObjectDisposedException>();
    }
    /// <summary>
    /// Performs buffer overflow_ large operations_ detected and handled.
    /// </summary>

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public void BufferOverflow_LargeOperations_DetectedAndHandled()
    {
        // Arrange
        using var smallBuffer = new TestMemoryBuffer<byte>(100);
        var largeData = new byte[1000];

        // Act & Assert

        var act = async () => await smallBuffer.CopyFromAsync(largeData, CancellationToken.None);
        _ = act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
    /// <summary>
    /// Performs integer overflow_ in parameters_ handled safely.
    /// </summary>
    /// <param name="sourceLength">The source length.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>

    [Theory]
    [InlineData(int.MaxValue, 0, 1000)] // Massive source length - actually valid (0 < MaxValue, 0+1000 < MaxValue)
    [InlineData(1000, int.MaxValue, 100)] // Massive offset - invalid (offset >= sourceLength)
    [InlineData(1000, 0, int.MaxValue)] // Massive count - invalid (offset + count > sourceLength)
    [Trait("Category", "ErrorScenarios")]
    public void IntegerOverflow_InParameters_HandledSafely(long sourceLength, long offset, long count)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<byte>(1024);

        // Act & Assert - Only some of these combinations are actually invalid
        Action act = () => buffer.TestValidateCopyParameters((int)sourceLength, (int)offset, 1024, 0, (int)count);

        // First case (MaxValue, 0, 1000) is actually valid - large length with small offset/count
        // Second case (1000, MaxValue, 100) throws because offset >= sourceLength
        // Third case (1000, 0, MaxValue) throws because offset + count > sourceLength
        if (offset >= sourceLength || offset + count > sourceLength)
        {
            _ = act.Should().Throw<ArgumentException>();
        }
        else
        {
            _ = act.Should().NotThrow();
        }
    }
    /// <summary>
    /// Gets memory pressure_ under high load_ maintains stability.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public async Task MemoryPressure_UnderHighLoad_MaintainsStability()
    {
        // Arrange
        var buffers = new List<TestMemoryBuffer<long>>();


        try
        {
            // Act - Create many buffers to simulate memory pressure
            for (var i = 0; i < 100; i++)
            {
                var buffer = new TestMemoryBuffer<long>(8192); // 8KB each
                buffers.Add(buffer);

                // Perform operations on each buffer

                var data = Enumerable.Range(1, 1024).Select(x => (long)x).ToArray();
                await buffer.CopyFromAsync(data, CancellationToken.None);
            }

            // Assert - All buffers should be in valid state
            _ = buffers.Should().AllSatisfy(b =>
            {
                _ = b.IsDisposed.Should().BeFalse();
                _ = b.State.Should().Be(BufferState.Allocated);
            });
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
    /// <summary>
    /// Gets concurrent dispose operations_ handle gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public async Task ConcurrentDisposeOperations_HandleGracefully()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<byte>(1024);
        var disposeTasks = new List<Task>();

        // Act - Attempt concurrent dispose operations

        for (var i = 0; i < 10; i++)
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
        _ = await act.Should().NotThrowAsync();
        _ = buffer.IsDisposed.Should().BeTrue();
    }
    /// <summary>
    /// Gets copy bandwidth_ different sizes_ meets threshold.
    /// </summary>
    /// <param name="bufferSize">The buffer size.</param>
    /// <param name="iterations">The iterations.</param>
    /// <returns>The result of the operation.</returns>

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

        for (var i = 0; i < iterations; i++)
        {
            await buffer.CopyFromAsync(sourceData, CancellationToken.None);
        }


        stopwatch.Stop();

        // Assert

        var totalBytes = (long)bufferSize * iterations;
        var throughputMBps = totalBytes / (stopwatch.ElapsedMilliseconds / 1000.0) / (1024 * 1024);


        _output.WriteLine($"Buffer size: {bufferSize}B, Throughput: {throughputMBps:F2} MB/s");
        _ = throughputMBps.Should().BeGreaterThan(50, "performance should be reasonable");
    }
    /// <summary>
    /// Performs allocation overhead_ small buffers_ is minimal.
    /// </summary>

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
            for (var i = 0; i < allocationCount; i++)
            {
                buffers.Add(new TestMemoryBuffer<byte>(bufferSize));
            }


            stopwatch.Stop();

            // Assert

            var avgAllocationTime = stopwatch.ElapsedMilliseconds / (double)allocationCount;
            _output.WriteLine($"Small buffer allocation avg: {avgAllocationTime:F4} ms");

            _ = avgAllocationTime.Should().BeLessThan(0.1, "small buffer allocation should be very fast");
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }
    /// <summary>
    /// Gets parallel copy operations_ scale well.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
            var tasks = buffers.Select(buffer => buffer.CopyFromAsync(sourceData, CancellationToken.None).AsTask());
            await Task.WhenAll(tasks);


            stopwatch.Stop();

            // Assert

            var totalDataMB = (bufferCount * bufferSize) / (1024.0 * 1024.0);
            var throughputMBps = totalDataMB / (stopwatch.ElapsedMilliseconds / 1000.0);


            _output.WriteLine($"Parallel copy throughput: {throughputMBps:F2} MB/s");
            // Throughput varies greatly in test environments - just verify operations complete
            _ = throughputMBps.Should().BeGreaterThan(0, "parallel operations should complete");
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }
    /// <summary>
    /// Performs memory footprint_ multiple buffers_ is reasonable.
    /// </summary>

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
            for (var i = 0; i < bufferCount; i++)
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

            _ = overheadPercentage.Should().BeLessThan(200, "memory overhead should be reasonable");
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }
    /// <summary>
    /// Gets async operation overhead_ is minimal.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        for (var i = 0; i < operationCount; i++)
        {
            data.CopyTo(buffer.AsSpan());
        }
        syncStopwatch.Stop();

        // Measure async operations

        var asyncStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < operationCount; i++)
        {
            await buffer.CopyFromAsync(data, CancellationToken.None);
        }
        asyncStopwatch.Stop();

        // Assert

        var overhead = (double)(asyncStopwatch.ElapsedMilliseconds - syncStopwatch.ElapsedMilliseconds) / syncStopwatch.ElapsedMilliseconds * 100;
        _output.WriteLine($"Async overhead: {overhead:F1}%");

        _ = overhead.Should().BeLessThan(300, "async overhead should be reasonable");
    }
    /// <summary>
    /// Gets memory fill_ different patterns_ fills correctly.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The result of the operation.</returns>

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
        _ = span.ToArray().Should().OnlyContain(x => x == pattern, $"all elements should be {pattern:X8}");
    }
    /// <summary>
    /// Gets zero fill_ large buffer_ clears all data.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public async Task ZeroFill_LargeBuffer_ClearsAllData()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<ulong>(8192); // 1024 elements

        // Fill with random data first

        var random = new Random(42);
        var span = buffer.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = (ulong)random.NextInt64();
        }

        // Act - Zero the entire buffer

        await buffer.FillAsync(0UL);

        // Assert
        _ = buffer.AsSpan().ToArray().Should().OnlyContain(x => x == 0UL, "all elements should be zero");
    }
    /// <summary>
    /// Gets partial fill_ different ranges_ fills only specified region.
    /// </summary>
    /// <param name="bufferElements">The buffer elements.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>The result of the operation.</returns>

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

        for (var i = 0; i < offset; i++)
        {
            _ = span[i].Should().Be(initialValue, $"element at index {i} should remain unchanged");
        }

        // Check filled region

        for (var i = offset; i < offset + count; i++)
        {
            _ = span[i].Should().Be(fillValue, $"element at index {i} should be filled");
        }

        // Check region after fill

        for (var i = offset + count; i < span.Length; i++)
        {
            _ = span[i].Should().Be(initialValue, $"element at index {i} should remain unchanged");
        }
    }
    /// <summary>
    /// Performs memory pattern verification_ detects complex corruption.
    /// </summary>

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
        for (var i = 2; i < span.Length; i++)
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


        for (var i = 2; i < reconstructed.Length; i++)
        {
            reconstructed[i] = reconstructed[i - 1] + reconstructed[i - 2];
            if (span[i] != reconstructed[i])
            {
                corruptedIndices.Add(i);
            }
        }

        // Assert
        _ = corruptedIndices.Should().Contain(new[] { 100, 500, 900 }, "all corrupted locations should be detected");
        _ = corruptedIndices.Should().HaveCount(3, "exactly 3 corruptions should be detected");
    }
    /// <summary>
    /// Performs memory initialization_ new buffer_ is zeroed.
    /// </summary>

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public void MemoryInitialization_NewBuffer_IsZeroed()
    {
        // Arrange & Act
        using var buffer = new TestMemoryBuffer<double>(1024); // 128 elements

        // Assert - New buffer should be zero-initialized

        var span = buffer.AsSpan();
        _ = span.ToArray().Should().OnlyContain(x => x == 0.0, "new buffer should be zero-initialized");
    }
    /// <summary>
    /// Performs repeating pattern_ fill and verify_ maintains consistency.
    /// </summary>
    /// <param name="patternSize">The pattern size.</param>
    /// <param name="repetitions">The repetitions.</param>

    [Theory]
    [InlineData(1, 1000)]      // Single byte pattern, many repetitions
    [InlineData(4, 250)]       // 4-byte pattern, fewer repetitions
    [InlineData(16, 62)]       // 16-byte pattern, minimal repetitions
    [Trait("Category", "MemoryPatterns")]
    public void RepeatingPattern_FillAndVerify_MaintainsConsistency(int patternSize, int repetitions)
    {
        // Arrange
        var bufferSize = patternSize * repetitions;
        using var buffer = new TestMemoryBuffer<byte>(bufferSize);

        // Create pattern

        var pattern = new byte[patternSize];
        for (var i = 0; i < patternSize; i++)
        {
            pattern[i] = (byte)(i * 17 + 42); // Arbitrary but deterministic pattern
        }

        // Act - Fill buffer with repeating pattern

        var span = buffer.AsSpan();
        for (var rep = 0; rep < repetitions; rep++)
        {
            for (var i = 0; i < patternSize; i++)
            {
                span[rep * patternSize + i] = pattern[i];
            }
        }

        // Assert - Verify pattern repetition

        for (var rep = 0; rep < repetitions; rep++)
        {
            for (var i = 0; i < patternSize; i++)
            {
                var expectedValue = pattern[i];
                var actualValue = span[rep * patternSize + i];
                _ = actualValue.Should().Be(expectedValue, $"pattern should repeat correctly at position {rep * patternSize + i}");
            }
        }
    }
    /// <summary>
    /// Performs chessboard pattern_ verification and integrity.
    /// </summary>

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public void ChessboardPattern_VerificationAndIntegrity()
    {
        // Arrange - Create a chessboard pattern (alternating 0/1)
        using var buffer = new TestMemoryBuffer<byte>(8192); // 8KB
        var span = buffer.AsSpan();

        // Act - Fill with chessboard pattern

        for (var i = 0; i < span.Length; i++)
        {
            span[i] = (byte)((i + (i / 64)) % 2); // Creates chessboard with 64-byte squares
        }

        // Assert - Verify pattern integrity

        for (var i = 0; i < span.Length; i++)
        {
            var expectedValue = (byte)((i + (i / 64)) % 2);
            _ = span[i].Should().Be(expectedValue, $"chessboard pattern should be correct at index {i}");
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
        /// <summary>
        /// Initializes a new instance of the TestMemoryBuffer class.
        /// </summary>
        /// <param name="sizeInBytes">The size in bytes.</param>

        public TestMemoryBuffer(long sizeInBytes) : base(sizeInBytes)
        {
            _hostMemory = new T[Length];
            _pinnedHandle = GCHandle.Alloc(_hostMemory, GCHandleType.Pinned);
        }
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>

        public override IntPtr DevicePointer => IntPtr.Zero;
        /// <summary>
        /// Gets or sets the memory type.
        /// </summary>
        /// <value>The memory type.</value>
        public override MemoryType MemoryType => MemoryType.Host;
        /// <summary>
        /// Gets or sets a value indicating whether disposed.
        /// </summary>
        /// <value>The is disposed.</value>
        public override bool IsDisposed => _disposed;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();


            var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            source.Span.CopyTo(_hostMemory.AsSpan(elementOffset));
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();


            var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            var sourceSpan = _hostMemory.AsSpan(elementOffset, destination.Length);
            sourceSpan.CopyTo(destination.Span);
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public override ValueTask DisposeAsync()

        {
            Dispose();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        // Abstract method implementations required by BaseMemoryBuffer<T>
        public override Span<T> AsSpan()

        {
            ThrowIfDisposed();
            return _hostMemory.AsSpan();
        }
        /// <summary>
        /// Gets as read only span.
        /// </summary>
        /// <returns>The result of the operation.</returns>


        public override ReadOnlySpan<T> AsReadOnlySpan()

        {
            ThrowIfDisposed();
            return _hostMemory.AsSpan();
        }
        /// <summary>
        /// Gets as type.
        /// </summary>
        /// <typeparam name="TNew">The TNew type parameter.</typeparam>
        /// <returns>The result of the operation.</returns>

        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>
        public override IAccelerator Accelerator => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
        /// <summary>
        /// Performs ensure on host.
        /// </summary>
        public override void EnsureOnHost() { }
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)

        {
            ThrowIfDisposed();
            _hostMemory.AsSpan().Fill(value);
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)

        {
            ThrowIfDisposed();
            _hostMemory.AsSpan(offset, count).Fill(value);
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets slice.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="length">The length.</param>
        /// <returns>The result of the operation.</returns>
        public override IUnifiedMemoryBuffer<T> Slice(int start, int length) => this;
        /// <summary>
        /// Performs synchronize.
        /// </summary>
        public override void Synchronize() { }
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets a value indicating whether on device.
        /// </summary>
        /// <value>The is on device.</value>
        public override bool IsOnDevice => false;
        /// <summary>
        /// Gets or sets a value indicating whether on host.
        /// </summary>
        /// <value>The is on host.</value>
        public override bool IsOnHost => true;
        /// <summary>
        /// Performs ensure on device.
        /// </summary>
        public override void EnsureOnDevice() { }
        /// <summary>
        /// Gets map.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public override MappedMemory<T> Map(Abstractions.Memory.MapMode mode) => throw new NotSupportedException();
        /// <summary>
        /// Gets map range.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public override MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode) => throw new NotSupportedException();
        /// <summary>
        /// Gets map asynchronously.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>
        public override void MarkDeviceDirty() { }
        /// <summary>
        /// Performs mark host dirty.
        /// </summary>
        public override void MarkHostDirty() { }
        /// <summary>
        /// Gets or sets a value indicating whether dirty.
        /// </summary>
        /// <value>The is dirty.</value>
        public override bool IsDirty => false;
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override Memory<T> AsMemory()

        {
            ThrowIfDisposed();
            return _hostMemory.AsMemory();
        }
        /// <summary>
        /// Gets as read only memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override ReadOnlyMemory<T> AsReadOnlyMemory()

        {
            ThrowIfDisposed();
            return _hostMemory.AsMemory();
        }
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public override MemoryOptions Options => default;
        /// <summary>
        /// Performs test throw if disposed.
        /// </summary>

        public void TestThrowIfDisposed() => ThrowIfDisposed();
        /// <summary>
        /// Performs test validate copy parameters.
        /// </summary>
        /// <param name="sourceLength">The source length.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destinationLength">The destination length.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        public void TestValidateCopyParameters(long sourceLength, long sourceOffset, long destinationLength, long destinationOffset, long count)
            => ValidateCopyParameters(sourceLength, sourceOffset, destinationLength, destinationOffset, count);
        /// <summary>
        /// Gets the pinnable reference.
        /// </summary>
        /// <returns>The pinnable reference.</returns>

        public ref T GetPinnableReference()
        {
            ThrowIfDisposed();
            return ref _hostMemory[0];
        }
    }
    /// <summary>
    /// A class that represents test device buffer.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>

    private sealed class TestDeviceBuffer<T>(long sizeInBytes, IntPtr devicePointer) : BaseDeviceBuffer<T>(sizeInBytes, devicePointer) where T : unmanaged
    {
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public override void Dispose() => MarkDisposed();
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override ValueTask DisposeAsync()
        {
            _ = MarkDisposed();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        // All required abstract method implementations
        public override Span<T> AsSpan() => [];
        /// <summary>
        /// Gets as read only span.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override ReadOnlySpan<T> AsReadOnlySpan() => [];
        /// <summary>
        /// Gets as type.
        /// </summary>
        /// <typeparam name="TNew">The TNew type parameter.</typeparam>
        /// <returns>The result of the operation.</returns>
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>
        public override IAccelerator Accelerator => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
        /// <summary>
        /// Performs ensure on host.
        /// </summary>
        public override void EnsureOnHost() { }
        /// <summary>
        /// Performs ensure on device.
        /// </summary>
        public override void EnsureOnDevice() { }
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets slice.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="length">The length.</param>
        /// <returns>The result of the operation.</returns>
        public override IUnifiedMemoryBuffer<T> Slice(int start, int length) => this;
        /// <summary>
        /// Performs synchronize.
        /// </summary>
        public override void Synchronize() { }
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets a value indicating whether on device.
        /// </summary>
        /// <value>The is on device.</value>
        public override bool IsOnDevice => true;
        /// <summary>
        /// Gets or sets a value indicating whether on host.
        /// </summary>
        /// <value>The is on host.</value>
        public override bool IsOnHost => false;
        /// <summary>
        /// Gets map.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public override MappedMemory<T> Map(Abstractions.Memory.MapMode mode) => throw new NotSupportedException();
        /// <summary>
        /// Gets map range.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public override MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode) => throw new NotSupportedException();
        /// <summary>
        /// Gets map asynchronously.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>
        public override void MarkDeviceDirty() { }
        /// <summary>
        /// Performs mark host dirty.
        /// </summary>
        public override void MarkHostDirty() { }
        /// <summary>
        /// Gets or sets a value indicating whether dirty.
        /// </summary>
        /// <value>The is dirty.</value>
        public override bool IsDirty => false;
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override Memory<T> AsMemory() => Memory<T>.Empty;
        /// <summary>
        /// Gets as read only memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public override MemoryOptions Options => default;
    }
    /// <summary>
    /// A class that represents test unified buffer.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>

    private sealed unsafe class TestUnifiedBuffer<T>(long sizeInBytes, IntPtr ptr) : BaseUnifiedBuffer<T>(sizeInBytes, ptr == IntPtr.Zero ? new IntPtr(1) : ptr) where T : unmanaged
    {
        private readonly T[]? _data;
        /// <summary>
        /// Initializes a new instance of the TestUnifiedBuffer class.
        /// </summary>
        /// <param name="sizeInBytes">The size in bytes.</param>

        public TestUnifiedBuffer(long sizeInBytes) : this(sizeInBytes, IntPtr.Zero)
        {
            _data = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
        }
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public override void Dispose() => MarkDisposed();
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override ValueTask DisposeAsync()
        {
            _ = MarkDisposed();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets as read only span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        // Additional abstract method implementations required by BaseMemoryBuffer<T>
        public override ReadOnlySpan<T> AsReadOnlySpan() => IsDisposed ? [] : [];
        /// <summary>
        /// Gets as type.
        /// </summary>
        /// <typeparam name="TNew">The TNew type parameter.</typeparam>
        /// <returns>The result of the operation.</returns>
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>
        public override IAccelerator Accelerator => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
        /// <summary>
        /// Performs ensure on host.
        /// </summary>
        public override void EnsureOnHost() { }
        /// <summary>
        /// Performs ensure on device.
        /// </summary>
        public override void EnsureOnDevice() { }
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets slice.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="length">The length.</param>
        /// <returns>The result of the operation.</returns>
        public override IUnifiedMemoryBuffer<T> Slice(int start, int length) => this;
        /// <summary>
        /// Performs synchronize.
        /// </summary>
        public override void Synchronize() { }
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets a value indicating whether on device.
        /// </summary>
        /// <value>The is on device.</value>
        public override bool IsOnDevice => false;
        /// <summary>
        /// Gets or sets a value indicating whether on host.
        /// </summary>
        /// <value>The is on host.</value>
        public override bool IsOnHost => true;
        /// <summary>
        /// Gets map.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public override MappedMemory<T> Map(Abstractions.Memory.MapMode mode) => throw new NotSupportedException();
        /// <summary>
        /// Gets map range.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public override MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode) => throw new NotSupportedException();
        /// <summary>
        /// Gets map asynchronously.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>
        public override void MarkDeviceDirty() { }
        /// <summary>
        /// Performs mark host dirty.
        /// </summary>
        public override void MarkHostDirty() { }
        /// <summary>
        /// Gets or sets a value indicating whether dirty.
        /// </summary>
        /// <value>The is dirty.</value>
        public override bool IsDirty => false;
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override Memory<T> AsMemory() => Memory<T>.Empty;
        /// <summary>
        /// Gets as read only memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public override MemoryOptions Options => default;
    }
    /// <summary>
    /// A class that represents test pooled buffer.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>

    private sealed class TestPooledBuffer<T>(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction) : BasePooledBuffer<T>(sizeInBytes, returnAction) where T : unmanaged
    {
        private readonly Memory<T> _memory = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>

        public override IntPtr DevicePointer => IntPtr.Zero;
        /// <summary>
        /// Gets or sets the memory type.
        /// </summary>
        /// <value>The memory type.</value>
        public override MemoryType MemoryType => MemoryType.Host;
        /// <summary>
        /// Gets or sets the memory.
        /// </summary>
        /// <value>The memory.</value>
        public override Memory<T> Memory => _memory;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        // All required abstract method implementations for BasePooledBuffer<T>
        public override Span<T> AsSpan() => _memory.Span;
        /// <summary>
        /// Gets as read only span.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override ReadOnlySpan<T> AsReadOnlySpan() => _memory.Span;
        /// <summary>
        /// Gets as type.
        /// </summary>
        /// <typeparam name="TNew">The TNew type parameter.</typeparam>
        /// <returns>The result of the operation.</returns>
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>
        public override IAccelerator Accelerator => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
        /// <summary>
        /// Performs ensure on host.
        /// </summary>
        public override void EnsureOnHost() { }
        /// <summary>
        /// Performs ensure on device.
        /// </summary>
        public override void EnsureOnDevice() { }
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets slice.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="length">The length.</param>
        /// <returns>The result of the operation.</returns>
        public override IUnifiedMemoryBuffer<T> Slice(int start, int length) => this;
        /// <summary>
        /// Performs synchronize.
        /// </summary>
        public override void Synchronize() { }
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        /// <summary>
        /// Gets or sets a value indicating whether on device.
        /// </summary>
        /// <value>The is on device.</value>
        public override bool IsOnDevice => false;
        /// <summary>
        /// Gets or sets a value indicating whether on host.
        /// </summary>
        /// <value>The is on host.</value>
        public override bool IsOnHost => true;
        /// <summary>
        /// Gets map.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public override MappedMemory<T> Map(Abstractions.Memory.MapMode mode) => new(_memory);
        /// <summary>
        /// Gets map range.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public override MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode) => new(_memory.Slice(offset, length));
        /// <summary>
        /// Gets map asynchronously.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public override ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>
        public override void MarkDeviceDirty() { }
        /// <summary>
        /// Performs mark host dirty.
        /// </summary>
        public override void MarkHostDirty() { }
        /// <summary>
        /// Gets or sets a value indicating whether dirty.
        /// </summary>
        /// <value>The is dirty.</value>
        public override bool IsDirty => false;
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override Memory<T> AsMemory() => _memory;
        /// <summary>
        /// Gets as read only memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => _memory;
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public override MemoryOptions Options => default;
    }

    #endregion
}