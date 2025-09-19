// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using FluentAssertions;
using DotCompute.Memory.Tests.TestHelpers;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for BaseMemoryBuffer covering 90%+ code coverage with all scenarios:
/// Memory Allocation, Copy Operations, Buffer Types, Error Scenarios, Performance, Memory Patterns.
/// </summary>
public class BaseMemoryBufferTests
{
    private readonly ITestOutputHelper _output;

    public BaseMemoryBufferTests(ITestOutputHelper output)
    {
        _output = output;
    }
    #region Memory Allocation Tests

    [Theory]
    [InlineData(4, 1)] // Single element
    [InlineData(1024, 256)] // Small buffer
    [InlineData(4096, 1024)] // Medium buffer
    [InlineData(1024 * 1024, 262144)] // Large buffer (1MB)
    [Trait("Category", "MemoryAllocation")]
    public void BaseMemoryBuffer_CalculatesLength_Correctly(int sizeInBytes, int expectedLength)
    {
        // Arrange & Act
        using var buffer = new TestMemoryBuffer<float>(sizeInBytes);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(sizeInBytes);
        _ = buffer.Length.Should().Be(expectedLength);
        _ = buffer.MemoryType.Should().Be(MemoryType.Host);
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [Trait("Category", "MemoryAllocation")]
    public void BaseMemoryBuffer_ThrowsForInvalidSize(int invalidSize)
    {
        // Act & Assert
        Action act = () => { var _ = new TestMemoryBuffer<float>(invalidSize); };
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*sizeInBytes*");
    }

    [Theory]
    [InlineData(1023)] // 1 byte short for float
    [InlineData(1025)] // 1 byte over for float
    [InlineData(7)]    // Non-aligned for float
    [Trait("Category", "MemoryAllocation")]
    public void BaseMemoryBuffer_ThrowsForNonAlignedSize(int nonAlignedSize)
    {
        // Act & Assert
        Action act = () => new TestMemoryBuffer<float>(nonAlignedSize);
        _ = act.Should().Throw<ArgumentException>()
            .WithMessage("*not evenly divisible*");
    }

    [Fact]
    [Trait("Category", "MemoryAllocation")]
    public void BaseMemoryBuffer_HandlesMaximumAllocation()
    {
        // Arrange - Test with a reasonably large size that won't cause OOM
        const int maxReasonableSize = 64 * 1024 * 1024; // 64MB

        // Act & Assert

        using var buffer = new TestMemoryBuffer<byte>(maxReasonableSize);
        _ = buffer.SizeInBytes.Should().Be(maxReasonableSize);
        _ = buffer.Length.Should().Be(maxReasonableSize);
    }

    [Theory]
    [InlineData(typeof(byte), 1)]
    [InlineData(typeof(int), 4)]
    [InlineData(typeof(long), 8)]
    [InlineData(typeof(double), 8)]
    [Trait("Category", "MemoryAllocation")]
    public void BaseMemoryBuffer_HandlesAlignmentRequirements(Type elementType, int expectedElementSize)
    {
        // This test verifies proper element size calculation
        var size = expectedElementSize * 100;


        if (elementType == typeof(byte))
        {
            using var buffer = new TestMemoryBuffer<byte>(size);
            _ = buffer.Length.Should().Be(size / expectedElementSize);
        }
        else if (elementType == typeof(int))
        {
            using var buffer = new TestMemoryBuffer<int>(size);
            _ = buffer.Length.Should().Be(size / expectedElementSize);
        }
        else if (elementType == typeof(long))
        {
            using var buffer = new TestMemoryBuffer<long>(size);
            _ = buffer.Length.Should().Be(size / expectedElementSize);
        }
        else if (elementType == typeof(double))
        {
            using var buffer = new TestMemoryBuffer<double>(size);
            _ = buffer.Length.Should().Be(size / expectedElementSize);
        }
    }

    #endregion

    #region Copy Operations Tests

    [Theory]
    [InlineData(-1, 0)] // Negative source offset
    [InlineData(0, -1)] // Negative destination offset
    [InlineData(-5, -3)] // Both negative
    [Trait("Category", "CopyOperations")]
    public void ValidateCopyParameters_ThrowsForInvalidOffsets(long sourceOffset, long destOffset)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<float>(1024);

        // Act & Assert

        var act = () => buffer.TestValidateCopyParameters(100, sourceOffset, 100, destOffset, 10);
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(100, 50, 100, 0, 60)] // Source overflow
    [InlineData(100, 0, 100, 50, 60)] // Destination overflow
    [InlineData(100, 90, 100, 0, 20)] // Source overflow (different scenario)
    [InlineData(100, 0, 100, 90, 20)] // Destination overflow (different scenario)
    [Trait("Category", "CopyOperations")]
    public void ValidateCopyParameters_ThrowsForOverflow(long sourceLength, long sourceOffset,

        long destLength, long destOffset, long count)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<float>(1024);

        // Act & Assert

        var act = () => buffer.TestValidateCopyParameters(sourceLength, sourceOffset, destLength, destOffset, count);
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.Message.Should().Contain("overflow");
    }

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task CopyFromAsync_ValidatesSourceSize()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<float>(16); // 4 elements
        var oversizedData = new float[10]; // More than buffer capacity

        // Act & Assert

        var act = async () => await buffer.CopyFromAsync(oversizedData, CancellationToken.None);
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task CopyToAsync_ValidatesDestinationSize()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<float>(40); // 10 elements
        var smallDestination = new float[5]; // Smaller than buffer

        // Act & Assert

        var act = async () => await buffer.CopyToAsync(smallDestination, CancellationToken.None);
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task CopyOperations_WorkWithValidData()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<float>(16); // 4 elements
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var destination = new float[4];

        // Act

        await buffer.CopyFromAsync(sourceData, CancellationToken.None);
        await buffer.CopyToAsync(destination, CancellationToken.None);

        // Assert
        _ = destination.Should().Equal(sourceData);
    }

    [Theory]
    [InlineData(0, 2)] // Copy first 2 elements
    [InlineData(2, 2)] // Copy last 2 elements 
    [InlineData(1, 3)] // Copy middle 3 elements
    [Trait("Category", "CopyOperations")]
    public async Task CopyOperations_HandlesPartialCopies(int offset, int count)
    {
        // Arrange
        using var sourceBuffer = new TestMemoryBuffer<int>(20); // 5 elements
        using var destBuffer = new TestMemoryBuffer<int>(20); // 5 elements

        // Act - This should not throw for valid ranges

        Func<Task> act = async () => await sourceBuffer.CopyToAsync(offset, destBuffer, 0, count, CancellationToken.None);

        // Assert
        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", "CopyOperations")]
    public async Task AsyncCopyOperations_SupportCancellation()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<float>(1024);
        var sourceData = new float[256];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert

        var act = async () => await buffer.CopyFromAsync(sourceData, cts.Token);
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Buffer Types Tests

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void HostBuffer_HasCorrectProperties()
    {
        // Arrange & Act
        using var buffer = new TestMemoryBuffer<int>(1024);

        // Assert
        _ = buffer.MemoryType.Should().Be(MemoryType.Host);
        _ = buffer.DevicePointer.Should().Be(IntPtr.Zero);
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeFalse();
        _ = buffer.IsDirty.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_HandlesSlicing()
    {
        // Arrange
        using var buffer = new TestUnifiedBuffer<float>(64); // 16 elements

        // Act

        var slice = buffer.Slice(4, 8); // Get middle 8 elements

        // Assert
        _ = slice.Should().NotBeNull();
        _ = slice.Should().BeSameAs(buffer); // Test implementation returns self
    }

    [Theory]
    [InlineData(-1, 4)] // Negative start
    [InlineData(0, -1)] // Negative length
    [InlineData(10, 20)] // Beyond buffer bounds
    [InlineData(15, 5)] // Start + length > buffer length
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_SliceValidatesParameters(int start, int length)
    {
        // Arrange
        using var buffer = new TestUnifiedBuffer<float>(64); // 16 elements

        // Act & Assert

        Action act = () => buffer.Slice(start, length);
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_ReturnsToPoolOnDispose()
    {
        // Arrange
        var returnCalled = false;
        var pooledBuffer = new TestPooledBuffer<float>(1024, b => returnCalled = true);

        // Act

        pooledBuffer.Dispose();

        // Assert
        _ = returnCalled.Should().BeTrue();
        _ = pooledBuffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_CanBeReset()
    {
        // Arrange
        var buffer = new TestPooledBuffer<float>(1024, null);
        buffer.Dispose();

        // Act

        buffer.Reset();

        // Assert
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    #endregion

    #region Error Scenarios Tests

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public void ThrowIfDisposed_ThrowsWhenDisposed()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<float>(1024);
        buffer.Dispose();

        // Act & Assert

        var act = buffer.TestThrowIfDisposed;
        _ = act.Should().Throw<ObjectDisposedException>()
            .Which.ObjectName.Should().Contain("TestMemoryBuffer");
    }

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public async Task OperationsOnDisposedBuffer_ThrowObjectDisposed()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<float>(1024);
        await buffer.DisposeAsync();

        // Act & Assert - Test multiple operations throw when disposed

        Action spanAccess = () => buffer.AsSpan();
        Action memoryAccess = () => buffer.AsMemory();
        var copyFromAsync = async () => await buffer.CopyFromAsync(new float[10], CancellationToken.None);
        var copyToAsync = async () => await buffer.CopyToAsync(new float[10], CancellationToken.None);

        _ = spanAccess.Should().Throw<ObjectDisposedException>();
        _ = memoryAccess.Should().Throw<ObjectDisposedException>();
        _ = await copyFromAsync.Should().ThrowAsync<ObjectDisposedException>();
        _ = await copyToAsync.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", "ErrorScenarios")]
    public async Task ConcurrentAccess_HandledSafely()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<int>(4096);
        var tasks = new List<Task>();
        var data = Enumerable.Range(1, 1024).ToArray();

        // Act - Multiple concurrent operations

        for (var i = 0; i < 10; i++)
        {
            tasks.Add(buffer.CopyFromAsync(data, CancellationToken.None).AsTask());
            tasks.Add(buffer.CopyToAsync(new int[1024], CancellationToken.None).AsTask());
        }

        // Assert - Should not throw

        var act = async () => await Task.WhenAll(tasks);
        _ = await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(1000, 500, 600)] // Copy beyond source bounds
    [InlineData(500, 1000, 100)] // Destination offset beyond bounds
    [Trait("Category", "ErrorScenarios")]
    public void OutOfBoundsAccess_ThrowsException(int bufferSize, int offset, int count)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<byte>(bufferSize);

        // Act & Assert

        var act = () => buffer.TestValidateCopyParameters(bufferSize, offset, bufferSize, 0, count);
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Performance Tests

    [Fact]
    [Trait("Category", "Performance")]
    public async Task CopyBandwidthMeasurement_MeetsMinimumThreshold()
    {
        // Arrange
        const int bufferSize = 1024 * 1024; // 1MB
        const int iterations = 10;
        using var buffer = new TestMemoryBuffer<byte>(bufferSize);
        var sourceData = new byte[bufferSize];
        var random = new Random(42);
        random.NextBytes(sourceData);


        var stopwatch = Stopwatch.StartNew();

        // Act - Measure copy performance

        for (var i = 0; i < iterations; i++)
        {
            await buffer.CopyFromAsync(sourceData, CancellationToken.None);
        }


        stopwatch.Stop();

        // Assert - Log performance metrics

        var totalBytes = (long)bufferSize * iterations;
        var throughputMBps = totalBytes / (stopwatch.ElapsedMilliseconds / 1000.0) / (1024 * 1024);


        _output.WriteLine($"Copy throughput: {throughputMBps:F2} MB/s");
        _output.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average per copy: {stopwatch.ElapsedMilliseconds / (double)iterations:F2} ms");

        // Should be able to copy at least 100 MB/s (very conservative)
        _ = throughputMBps.Should().BeGreaterThan(100, "copy operations should have reasonable performance");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void AllocationOverhead_IsMinimal()
    {
        // Arrange
        const int allocationCount = 1000;
        const int bufferSize = 4096;
        var stopwatch = Stopwatch.StartNew();
        var buffers = new List<TestMemoryBuffer<float>>();

        // Act - Measure allocation time

        for (var i = 0; i < allocationCount; i++)
        {
            buffers.Add(new TestMemoryBuffer<float>(bufferSize));
        }


        stopwatch.Stop();

        // Assert

        var avgAllocationTime = stopwatch.ElapsedMilliseconds / (double)allocationCount;
        _output.WriteLine($"Average allocation time: {avgAllocationTime:F3} ms");
        _output.WriteLine($"Total allocation time: {stopwatch.ElapsedMilliseconds} ms");

        _ = avgAllocationTime.Should().BeLessThan(1.0, "allocation should be fast");

        // Cleanup

        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void PoolEfficiency_ShowsReuse()
    {
        // Arrange
        var returnCount = 0;
        var pool = new Queue<TestPooledBuffer<int>>();


        TestPooledBuffer<int> CreateOrRent()
        {
            if (pool.Count > 0)
            {
                var buffer = pool.Dequeue();
                buffer.Reset();
                return buffer;
            }
            return new TestPooledBuffer<int>(1024, b =>
            {
                returnCount++;
                pool.Enqueue((TestPooledBuffer<int>)b);
            });
        }

        // Act - Simulate pool usage

        const int operationCount = 100;
        for (var i = 0; i < operationCount; i++)
        {
            using var buffer = CreateOrRent();
            // Simulate work
            buffer.AsSpan().Fill(i);
        }

        // Assert

        _output.WriteLine($"Total returns to pool: {returnCount}");
        _output.WriteLine($"Pool size: {pool.Count}");

        _ = returnCount.Should().BeGreaterThan(operationCount - 10, "most buffers should be returned to pool");
        _ = pool.Count.Should().BeGreaterThan(0, "pool should contain reusable buffers");
    }

    #endregion

    #region Memory Patterns Tests

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [Trait("Category", "MemoryPatterns")]
    public async Task FillOperations_SetsCorrectPattern(int fillValue)
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<int>(40); // 10 elements

        // Act

        await buffer.FillAsync(fillValue);

        // Assert

        var span = buffer.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            _ = span[i].Should().Be(fillValue, $"element at index {i} should match fill value");
        }
    }

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public async Task PartialFill_WorksCorrectly()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<byte>(100);
        await buffer.FillAsync(0); // Initialize to zeros

        // Act - Fill middle section with 0xFF

        await buffer.FillAsync(0xFF, 20, 40);

        // Assert

        var span = buffer.AsSpan();

        // Check initial section is still zero

        for (var i = 0; i < 20; i++)
        {
            _ = span[i].Should().Be(0, $"element at index {i} should still be zero");
        }

        // Check filled section

        for (var i = 20; i < 60; i++)
        {
            _ = span[i].Should().Be(0xFF, $"element at index {i} should be filled");
        }

        // Check final section is still zero

        for (var i = 60; i < 100; i++)
        {
            _ = span[i].Should().Be(0, $"element at index {i} should still be zero");
        }
    }

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public void PatternVerification_DetectsCorruption()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<uint>(1000 * 4); // 1000 elements
        const uint pattern = 0xDEADBEEF;

        // Fill with pattern

        buffer.AsSpan().Fill(pattern);

        // Corrupt one element

        buffer.AsSpan()[500] = 0xBADC0DE;

        // Act - Verify pattern (should detect corruption)

        var isCorrupted = false;
        var span = buffer.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] != pattern)
            {
                isCorrupted = true;
                break;
            }
        }

        // Assert
        _ = isCorrupted.Should().BeTrue("pattern verification should detect corruption");
    }

    [Fact]
    [Trait("Category", "MemoryPatterns")]
    public async Task MemoryZeroing_ClearsAllData()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<long>(64); // 8 elements

        // Fill with non-zero data first

        buffer.AsSpan().Fill(0x123456789ABCDEF0L);

        // Act - Zero the memory

        await buffer.FillAsync(0L);

        // Assert

        var span = buffer.AsSpan();
        _ = span.ToArray().Should().OnlyContain(x => x == 0L, "all elements should be zero");
    }

    [Theory]
    [InlineData(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD })]
    [InlineData(new byte[] { 0x00, 0xFF, 0x00, 0xFF })]
    [InlineData(new byte[] { 0x12, 0x34, 0x56, 0x78 })]
    [Trait("Category", "MemoryPatterns")]
    public async Task PatternCopyAndVerify_MaintainsIntegrity(byte[] pattern)
    {
        // Arrange
        using var sourceBuffer = new TestMemoryBuffer<byte>(pattern.Length);
        using var destBuffer = new TestMemoryBuffer<byte>(pattern.Length);

        // Act - Copy pattern and verify

        await sourceBuffer.CopyFromAsync(pattern, CancellationToken.None);
        await sourceBuffer.CopyToAsync(destBuffer.AsMemory(), CancellationToken.None);

        // Assert
        _ = sourceBuffer.AsSpan().ToArray().Should().Equal(pattern, "source should match original pattern");
        _ = destBuffer.AsSpan().ToArray().Should().Equal(pattern, "destination should match original pattern");
        _ = destBuffer.AsSpan().ToArray().Should().Equal(sourceBuffer.AsSpan().ToArray(), "both buffers should match");
    }

    #endregion
}
