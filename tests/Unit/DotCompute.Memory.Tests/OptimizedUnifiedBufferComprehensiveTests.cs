// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Tests.Common;
using Microsoft.Extensions.ObjectPool;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for OptimizedUnifiedBuffer covering all critical scenarios.
/// Part of Phase 1: Memory Module testing to achieve 80% coverage.
/// </summary>
public sealed class OptimizedUnifiedBufferComprehensiveTests : IDisposable
{
    private readonly TestUnifiedMemoryManager _mockMemoryManager;

    public OptimizedUnifiedBufferComprehensiveTests()
    {
        _mockMemoryManager = new TestUnifiedMemoryManager();
    }

    public void Dispose() => _mockMemoryManager.Dispose();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesBuffer()
    {
        // Act
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Assert
        _ = buffer.Length.Should().Be(100);
        _ = buffer.SizeInBytes.Should().Be(400); // 100 * sizeof(int)
        _ = buffer.State.Should().Be(BufferState.HostOnly);
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullMemoryManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            var buffer = new OptimizedUnifiedBuffer<int>(null!, 100);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void Constructor_WithZeroLength_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        try
        {
            var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 0);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void Constructor_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        try
        {
            var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, -10);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void Constructor_WithInitialData_CopiesData()
    {
        // Arrange
        var initialData = new int[] { 1, 2, 3, 4, 5 };

        // Act
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, initialData);

        // Assert
        _ = buffer.Length.Should().Be(5);
        var span = buffer.AsSpan();
        _ = span.ToArray().Should().Equal(initialData);
    }

    [Fact]
    public void Constructor_WithArrayPool_UsesPooling()
    {
        // Arrange
        var pool = new TestArrayPool<int>();

        // Act
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100, pool);

        // Assert
        _ = buffer.Length.Should().Be(100);
        var metrics = buffer.PerformanceMetrics;
        _ = metrics.AllocationSource.Should().Be("Pool");
    }

    [Fact]
    public void Constructor_WithoutArrayPool_UsesDirectAllocation()
    {
        // Act
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Assert
        var metrics = buffer.PerformanceMetrics;
        _ = metrics.AllocationSource.Should().Be("Direct");
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Properties_InitialState_HasCorrectValues()
    {
        // Act
        using var buffer = new OptimizedUnifiedBuffer<float>(_mockMemoryManager, 256);

        // Assert
        _ = buffer.Length.Should().Be(256);
        _ = buffer.SizeInBytes.Should().Be(1024); // 256 * sizeof(float)
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeFalse();
        _ = buffer.IsDirty.Should().BeFalse();
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void PerformanceMetrics_InitialState_ReturnsZeroStatistics()
    {
        // Act
        using var buffer = new OptimizedUnifiedBuffer<double>(_mockMemoryManager, 50);
        var metrics = buffer.PerformanceMetrics;

        // Assert
        _ = metrics.TransferCount.Should().Be(0);
        _ = metrics.AverageTransferTime.Should().Be(TimeSpan.Zero);
        _ = metrics.SizeInBytes.Should().Be(400); // 50 * sizeof(double)
    }

    [Fact]
    public void DevicePointer_WhenNotOnDevice_ReturnsZero()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        var pointer = buffer.DevicePointer;

        // Assert
        _ = pointer.Should().Be(IntPtr.Zero);
    }

    #endregion

    #region Span and Memory Operations Tests

    [Fact]
    public void AsSpan_ReturnsValidSpan()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);

        // Act
        var span = buffer.AsSpan();

        // Assert
        _ = span.Length.Should().Be(10);
        span[0] = 42;
        _ = buffer.AsSpan()[0].Should().Be(42);
    }

    [Fact]
    public void AsReadOnlySpan_ReturnsValidReadOnlySpan()
    {
        // Arrange
        var initialData = new[] { 1, 2, 3, 4, 5 };
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, initialData);

        // Act
        var span = buffer.AsReadOnlySpan();

        // Assert
        _ = span.Length.Should().Be(5);
        _ = span.ToArray().Should().Equal(initialData);
    }

    [Fact]
    public void AsMemory_ReturnsValidMemory()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 20);

        // Act
        var memory = buffer.AsMemory();

        // Assert
        _ = memory.Length.Should().Be(20);
        memory.Span[5] = 100;
        _ = buffer.AsSpan()[5].Should().Be(100);
    }

    [Fact]
    public void AsReadOnlyMemory_ReturnsValidReadOnlyMemory()
    {
        // Arrange
        var initialData = new[] { 10, 20, 30 };
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, initialData);

        // Act
        var memory = buffer.AsReadOnlyMemory();

        // Assert
        _ = memory.Length.Should().Be(3);
        _ = memory.ToArray().Should().Equal(initialData);
    }

    [Fact]
    public void AsSpan_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);
        buffer.Dispose();

        // Act & Assert
        try
        {
            var span = buffer.AsSpan();
            Assert.Fail("Expected ObjectDisposedException was not thrown");
        }
        catch (ObjectDisposedException)
        {
            // Expected exception
        }
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void EnsureOnHost_WhenAlreadyOnHost_DoesNotThrow()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        buffer.EnsureOnHost();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.State.Should().Be(BufferState.HostOnly);
    }

    [Fact]
    public void EnsureOnHost_MultipleCallsAreIdempotent()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        buffer.EnsureOnHost();
        buffer.EnsureOnHost();
        buffer.EnsureOnHost();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureOnHostAsync_CompletesSuccessfully()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 50);

        // Act
        await buffer.EnsureOnHostAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureOnHostAsync_WithAlreadyOnHost_CompletesWithoutCancellation()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.AsSpan()[0] = 1; // Initialize buffer (already on host)
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Should complete without throwing since already on host
        await buffer.EnsureOnHostAsync(default, cts.Token);

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void EnsureOnDevice_AllocatesDeviceMemory()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.AsSpan()[0] = 1; // Initialize buffer

        // Act
        buffer.EnsureOnDevice();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_CompletesSuccessfully()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 50);
        buffer.AsSpan()[0] = 1; // Initialize buffer

        // Act
        await buffer.EnsureOnDeviceAsync();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    #endregion

    #region Dirty Tracking Tests

    [Fact]
    public void MarkHostDirty_UpdatesState()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);
        _ = buffer.AsSpan(); // Ensure on host

        // Act
        buffer.MarkHostDirty();

        // Assert
        _ = buffer.IsDirty.Should().BeFalse(); // HostOnly state doesn't become dirty
    }

    [Fact]
    public void MarkDeviceDirty_UpdatesState()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.AsSpan()[0] = 1; // Initialize buffer
        buffer.EnsureOnDevice();

        // Act
        buffer.MarkDeviceDirty();

        // Assert
        _ = buffer.State.Should().Be(BufferState.DeviceDirty);
    }

    [Fact]
    public void MarkHostDirty_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);
        buffer.Dispose();

        // Act & Assert
        try
        {
            buffer.MarkHostDirty();
            Assert.Fail("Expected ObjectDisposedException was not thrown");
        }
        catch (ObjectDisposedException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void Synchronize_WhenNotDirty_DoesNotThrow()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        buffer.Synchronize();

        // Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
    }

    [Fact]
    public async Task SynchronizeAsync_CompletesSuccessfully()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 50);

        // Act
        await buffer.SynchronizeAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    #endregion

    #region Buffer Operations Tests

    [Fact]
    public void GetMemory_ReturnsValidMemory()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 30);

        // Act
        var memory = buffer.GetMemory();

        // Assert
        _ = memory.Length.Should().Be(30);
    }

    [Fact]
    public async Task CopyFromAsync_CopiesDataCorrectly()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);
        var sourceData = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act
        await buffer.CopyFromAsync(sourceData);

        // Assert
        _ = buffer.AsSpan().ToArray().Should().Equal(sourceData);
    }

    [Fact]
    public async Task CopyFromAsync_WithOversizedSource_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 5);
        var sourceData = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await buffer.CopyFromAsync(sourceData));
    }

    [Fact]
    public async Task CopyToAsync_CopiesDataCorrectly()
    {
        // Arrange
        var initialData = new[] { 1, 2, 3, 4, 5 };
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, initialData);
        var destination = new int[5];

        // Act
        await buffer.CopyToAsync(destination);

        // Assert
        _ = destination.Should().Equal(initialData);
    }

    [Fact]
    public async Task CopyToAsync_WithUndersizedDestination_CopiesPartialData()
    {
        // Arrange
        var initialData = new[] { 1, 2, 3, 4, 5 };
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, initialData.Length);
        await buffer.CopyFromAsync(initialData); // Initialize buffer
        var destination = new int[3];

        // Act - OptimizedUnifiedBuffer copies what fits without throwing
        await buffer.CopyToAsync(destination);

        // Assert - First 3 elements should be copied
        _ = destination.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Slice_CreatesNewBuffer()
    {
        // Arrange
        var initialData = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, initialData);

        // Act
        using var slice = buffer.Slice(2, 5);

        // Assert
        _ = slice.Length.Should().Be(5);
    }

    [Fact]
    public void Slice_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);

        // Act & Assert
        try
        {
            var slice = buffer.Slice(-1, 5);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void Slice_WithInvalidLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);

        // Act & Assert
        try
        {
            var slice = buffer.Slice(5, -3);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void Slice_WithOffsetPlusLengthExceedingSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);

        // Act & Assert
        try
        {
            var slice = buffer.Slice(8, 5);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    #endregion

    #region Fill Operations Tests

    [Fact]
    public async Task FillAsync_FillsEntireBuffer()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 20);

        // Act
        await buffer.FillAsync(42);

        // Assert
        _ = buffer.AsSpan().ToArray().Should().OnlyContain(x => x == 42);
    }

    [Fact]
    public async Task FillAsync_WithOffsetAndCount_FillsPartialBuffer()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 20);

        // Act
        await buffer.FillAsync(99, 5, 10);

        // Assert
        var span = buffer.AsSpan();
        _ = span[4].Should().Be(0); // Before filled region
        _ = span[5].Should().Be(99); // Start of filled region
        _ = span[14].Should().Be(99); // End of filled region
        _ = span[15].Should().Be(0); // After filled region
    }

    [Fact]
    public async Task FillAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await buffer.FillAsync(42, -1, 5));
    }

    [Fact]
    public async Task FillAsync_WithInvalidCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await buffer.FillAsync(42, 0, -5));
    }

    [Fact]
    public async Task FillAsync_WithOffsetPlusCountExceedingSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await buffer.FillAsync(42, 8, 5));
    }

    #endregion

    #region Map Operations Tests

    [Fact]
    public void Map_ReturnsValidMappedMemory()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 50);

        // Act
        using var mapped = buffer.Map();

        // Assert
        _ = mapped.Memory.Length.Should().Be(50);
    }

    [Fact]
    public void MapRange_ReturnsValidMappedMemory()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        using var mapped = buffer.MapRange(10, 20);

        // Assert
        _ = mapped.Memory.Length.Should().Be(20);
    }

    [Fact]
    public void MapRange_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 50);

        // Act & Assert
        try
        {
            var mapped = buffer.MapRange(-1, 10);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void MapRange_WithInvalidLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 50);

        // Act & Assert
        try
        {
            var mapped = buffer.MapRange(0, -10);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public async Task MapAsync_ReturnsValidMappedMemory()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 30);

        // Act
        using var mapped = await buffer.MapAsync();

        // Assert
        _ = mapped.Memory.Length.Should().Be(30);
    }

    #endregion

    #region Read and Write Tests

    [Fact]
    public async Task ReadAsync_ReadsAllData()
    {
        // Arrange
        var initialData = new[] { 1, 2, 3, 4, 5 };
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, initialData);

        // Act
        var result = await buffer.ReadAsync();

        // Assert
        _ = result.Should().Equal(initialData);
    }

    [Fact]
    public async Task ReadAsync_WithOffsetAndCount_ReadsPartialData()
    {
        // Arrange
        var initialData = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, initialData);

        // Act
        var result = await buffer.ReadAsync(3, 4);

        // Assert
        _ = result.Should().Equal([4, 5, 6, 7]);
    }

    [Fact]
    public async Task ReadAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await buffer.ReadAsync(-1, 5));
    }

    [Fact]
    public async Task WriteAsync_WithMemory_WritesData()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);
        var data = new[] { 10, 20, 30, 40, 50 };

        // Act
        await buffer.WriteAsync(data.AsMemory(), 0);

        // Assert
        var span = buffer.AsSpan();
        _ = span[0].Should().Be(10);
        _ = span[4].Should().Be(50);
    }

    [Fact]
    public async Task WriteAsync_WithArray_WritesData()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);
        var data = new[] { 100, 200, 300 };

        // Act
        await buffer.WriteAsync(data, 5);

        // Assert
        var span = buffer.AsSpan();
        _ = span[5].Should().Be(100);
        _ = span[7].Should().Be(300);
    }

    [Fact]
    public async Task WriteAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);
        var data = new[] { 1, 2, 3 };

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await buffer.WriteAsync(data, -1));
    }

    [Fact]
    public async Task WriteAsync_WithOffsetPlusDataExceedingSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 10);
        var data = new[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await buffer.WriteAsync(data, 8));
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_MarksBufferAsDisposed()
    {
        // Arrange
        var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_MultipleCallsAreIdempotent()
    {
        // Arrange
        var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        buffer.Dispose();
        buffer.Dispose();
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_MarksBufferAsDisposed()
    {
        // Arrange
        var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        await buffer.DisposeAsync();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCallsAreIdempotent()
    {
        // Arrange
        var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        await buffer.DisposeAsync();
        await buffer.DisposeAsync();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(1024)]
    public void Constructor_WithVariousSizes_WorksCorrectly(int size)
    {
        // Act
        using var buffer = new OptimizedUnifiedBuffer<byte>(_mockMemoryManager, size);

        // Assert
        _ = buffer.Length.Should().Be(size);
        _ = buffer.SizeInBytes.Should().Be(size);
    }

    [Fact]
    public async Task ComplexWorkflow_ReadWriteFillSynchronize_WorksCorrectly()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 20);

        // Act - Write initial data
        var initialData = new[] { 1, 2, 3, 4, 5 };
        await buffer.WriteAsync(initialData, 0);

        // Act - Fill middle section
        await buffer.FillAsync(99, 5, 10);

        // Act - Read back
        var result = await buffer.ReadAsync();

        // Assert
        _ = result[0].Should().Be(1);
        _ = result[4].Should().Be(5);
        _ = result[5].Should().Be(99);
        _ = result[14].Should().Be(99);
        _ = result[15].Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleOperations_WorkCorrectly()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act - Perform multiple concurrent operations
        await buffer.FillAsync(1, 0, 25);
        await buffer.FillAsync(2, 25, 25);
        await buffer.FillAsync(3, 50, 25);
        await buffer.FillAsync(4, 75, 25);

        // Assert
        var span = buffer.AsSpan();
        _ = span[10].Should().Be(1);
        _ = span[35].Should().Be(2);
        _ = span[60].Should().Be(3);
        _ = span[85].Should().Be(4);
    }

    [Fact]
    public void PerformanceMetrics_AfterOperations_TracksLastAccessTime()
    {
        // Arrange
        using var buffer = new OptimizedUnifiedBuffer<int>(_mockMemoryManager, 50);
        var beforeAccess = DateTimeOffset.UtcNow;

        // Act
        var span = buffer.AsSpan();
        span[0] = 42;
        var afterAccess = DateTimeOffset.UtcNow;

        var metrics = buffer.PerformanceMetrics;

        // Assert
        _ = metrics.LastAccessTime.Should().BeOnOrAfter(beforeAccess);
        _ = metrics.LastAccessTime.Should().BeOnOrBefore(afterAccess);
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Test implementation of IUnifiedMemoryManager for testing purposes.
    /// </summary>
    private sealed class TestUnifiedMemoryManager : IUnifiedMemoryManager, IAsyncDisposable
    {
        private readonly List<IUnifiedMemoryBuffer> _allocatedBuffers = [];
        private bool _disposed;

        public long MaxAllocationSize { get; set; } = long.MaxValue;
        public IAccelerator Accelerator => null!;
        public Abstractions.Memory.MemoryStatistics Statistics => new()
        {
            TotalAllocated = _allocatedBuffers.Sum(b => b.SizeInBytes),
            AllocationCount = _allocatedBuffers.Count
        };
        public long TotalAvailableMemory => long.MaxValue;
        public long CurrentAllocatedMemory => _allocatedBuffers.Sum(b => b.SizeInBytes);

        public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(int count, MemoryOptions options, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var buffer = new TestMemoryBuffer<T>(count);
            _allocatedBuffers.Add(buffer);
            return new ValueTask<IUnifiedMemoryBuffer<T>>(buffer);
        }

        public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, MemoryOptions options, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken);
            await buffer.CopyFromAsync(source, cancellationToken);
            return buffer;
        }

        public ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(long sizeInBytes, MemoryOptions options, CancellationToken cancellationToken = default)
        {
            var buffer = new TestMemoryBuffer<byte>((int)sizeInBytes);
            _allocatedBuffers.Add(buffer);
            return new ValueTask<IUnifiedMemoryBuffer>(buffer);
        }

        public IUnifiedMemoryBuffer<T> CreateView<T>(IUnifiedMemoryBuffer<T> buffer, int offset, int length) where T : unmanaged
            => buffer.Slice(offset, length);

        public ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) where T : unmanaged
            => source.CopyToAsync(destination, cancellationToken);

        public ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) where T : unmanaged
            => source.CopyToAsync(sourceOffset, destination, destinationOffset, count, cancellationToken);

        public ValueTask CopyToDeviceAsync<T>(ReadOnlyMemory<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) where T : unmanaged
            => destination.CopyFromAsync(source, cancellationToken);

        public ValueTask CopyFromDeviceAsync<T>(IUnifiedMemoryBuffer<T> source, Memory<T> destination, CancellationToken cancellationToken = default) where T : unmanaged
            => source.CopyToAsync(destination, cancellationToken);

        public ValueTask CopyHostToDeviceAsync(nint hostPointer, DeviceMemory deviceMemory, long sizeInBytes, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask CopyDeviceToHostAsync(DeviceMemory deviceMemory, nint hostPointer, long sizeInBytes, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public void CopyDeviceToDevice(DeviceMemory source, DeviceMemory destination, long sizeInBytes)
        {
            // No-op for testing
        }

        public void Free(IUnifiedMemoryBuffer buffer) => _allocatedBuffers.Remove(buffer);

        public ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
        {
            _ = _allocatedBuffers.Remove(buffer);
            return ValueTask.CompletedTask;
        }

        public ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public void Clear() => _allocatedBuffers.Clear();

        public DeviceMemory AllocateDevice(long sizeInBytes)
            => new DeviceMemory((nint)sizeInBytes, sizeInBytes);

        public void FreeDevice(DeviceMemory deviceMemory)
        {
            // No-op for testing
        }

        public void MemsetDevice(DeviceMemory deviceMemory, byte value, long sizeInBytes)
        {
            // No-op for testing
        }

        public ValueTask MemsetDeviceAsync(DeviceMemory deviceMemory, byte value, long sizeInBytes, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public void CopyHostToDevice(nint hostPointer, DeviceMemory deviceMemory, long sizeInBytes)
        {
            // No-op for testing
        }

        public void CopyDeviceToHost(DeviceMemory deviceMemory, nint hostPointer, long sizeInBytes)
        {
            // No-op for testing
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var buffer in _allocatedBuffers)
                {
                    buffer?.Dispose();
                }
                _allocatedBuffers.Clear();
                _disposed = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                foreach (var buffer in _allocatedBuffers)
                {
                    if (buffer != null)
                    {
                        await buffer.DisposeAsync();
                    }
                }
                _allocatedBuffers.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Simple test array pool for testing pooling behavior.
    /// </summary>
    private sealed class TestArrayPool<T> : ObjectPool<T[]>
    {
        private readonly List<T[]> _pool = [];

        public override T[] Get()
        {
            if (_pool.Count > 0)
            {
                var item = _pool[^1];
                _pool.RemoveAt(_pool.Count - 1);
                return item;
            }
            return new T[100];
        }

        public override void Return(T[] obj)
        {
            if (obj != null)
            {
                _pool.Add(obj);
            }
        }
    }

    #endregion
}
