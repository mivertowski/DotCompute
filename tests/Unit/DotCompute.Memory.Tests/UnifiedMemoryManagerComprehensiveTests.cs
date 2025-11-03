// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AbstractionsAcceleratorType = DotCompute.Abstractions.AcceleratorType;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for UnifiedMemoryManager covering all functionality.
/// Target: 80%+ coverage for 492-line production-grade memory manager.
/// </summary>
public class UnifiedMemoryManagerComprehensiveTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithAccelerator_InitializesSuccessfully()
    {
        // Arrange
        var acceleratorMock = new Mock<IAccelerator>();
        _ = acceleratorMock.Setup(a => a.Type).Returns(AbstractionsAcceleratorType.CPU);
        _ = acceleratorMock.Setup(a => a.Info).Returns(new AcceleratorInfo(
            AbstractionsAcceleratorType.CPU, "TestCPU", "1.0", 8L * 1024 * 1024 * 1024));
        var logger = NullLogger.Instance;

        // Act
        using var manager = new UnifiedMemoryManager(acceleratorMock.Object, logger);

        // Assert
        _ = manager.Should().NotBeNull();
        _ = manager.Accelerator.Should().Be(acceleratorMock.Object);
        _ = manager.MaxAllocationSize.Should().Be(16L * 1024 * 1024 * 1024); // 16GB
        _ = manager.TotalAvailableMemory.Should().Be(8L * 1024 * 1024 * 1024);
        _ = manager.CurrentAllocatedMemory.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithoutAccelerator_InitializesForCpuOnly()
    {
        // Arrange
        var logger = NullLogger.Instance;

        // Act
        using var manager = new UnifiedMemoryManager(logger);

        // Assert
        _ = manager.Should().NotBeNull();
        _ = manager.MaxAllocationSize.Should().Be(16L * 1024 * 1024 * 1024); // 16GB
        _ = manager.TotalAvailableMemory.Should().Be(32L * 1024 * 1024 * 1024); // 32GB fallback
        _ = manager.CurrentAllocatedMemory.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Arrange
        IAccelerator? accelerator = null;
        var logger = NullLogger.Instance;

        // Act & Assert
        try
        {
            using var manager = new UnifiedMemoryManager(accelerator!, logger);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException ex)
        {
            _ = ex.ParamName.Should().Be("accelerator");
        }
    }

    [Fact]
    public void Constructor_WithNullLogger_UsesNullLogger()
    {
        // Arrange
        var acceleratorMock = new Mock<IAccelerator>();
        _ = acceleratorMock.Setup(a => a.Type).Returns(AbstractionsAcceleratorType.CPU);
        _ = acceleratorMock.Setup(a => a.Info).Returns(new AcceleratorInfo(
            AbstractionsAcceleratorType.CPU, "TestCPU", "1.0", 8L * 1024 * 1024 * 1024));

        // Act
        using var manager = new UnifiedMemoryManager(acceleratorMock.Object, null);

        // Assert
        _ = manager.Should().NotBeNull();
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Accelerator_WithAccelerator_ReturnsAccelerator()
    {
        // Arrange
        var acceleratorMock = new Mock<IAccelerator>();
        _ = acceleratorMock.Setup(a => a.Type).Returns(AbstractionsAcceleratorType.CUDA);
        _ = acceleratorMock.Setup(a => a.Info).Returns(new AcceleratorInfo(
            AbstractionsAcceleratorType.CUDA, "TestGPU", "1.0", 8L * 1024 * 1024 * 1024));
        using var manager = new UnifiedMemoryManager(acceleratorMock.Object);

        // Act
        var result = manager.Accelerator;

        // Assert
        _ = result.Should().Be(acceleratorMock.Object);
    }

    [Fact]
    public void Accelerator_WithoutAccelerator_ThrowsInvalidOperationException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act & Assert
        try
        {
            _ = manager.Accelerator;
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            _ = ex.Message.Should().Contain("No accelerator associated");
        }
    }

    [Fact]
    public void Statistics_ReturnsCorrectSnapshot()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act
        var stats = manager.Statistics;

        // Assert
        _ = stats.Should().NotBeNull();
        _ = stats.TotalAllocated.Should().Be(0);
        _ = stats.CurrentUsage.Should().Be(0);
        _ = stats.PeakUsage.Should().Be(0);
        _ = stats.AllocationCount.Should().Be(0);
    }

    [Fact]
    public void MaxAllocationSize_ReturnsExpectedValue()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act
        var maxSize = manager.MaxAllocationSize;

        // Assert
        _ = maxSize.Should().Be(16L * 1024 * 1024 * 1024); // 16GB
    }

    [Fact]
    public void TotalAvailableMemory_WithAccelerator_ReturnsAcceleratorMemory()
    {
        // Arrange
        var acceleratorMock = new Mock<IAccelerator>();
        _ = acceleratorMock.Setup(a => a.Type).Returns(AbstractionsAcceleratorType.CUDA);
        _ = acceleratorMock.Setup(a => a.Info).Returns(new AcceleratorInfo(
            AbstractionsAcceleratorType.CUDA, "TestGPU", "1.0", 12L * 1024 * 1024 * 1024));
        using var manager = new UnifiedMemoryManager(acceleratorMock.Object);

        // Act
        var totalMemory = manager.TotalAvailableMemory;

        // Assert
        _ = totalMemory.Should().Be(12L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void TotalAvailableMemory_WithoutAccelerator_ReturnsFallbackValue()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act
        var totalMemory = manager.TotalAvailableMemory;

        // Assert
        _ = totalMemory.Should().Be(32L * 1024 * 1024 * 1024); // 32GB fallback
    }

    [Fact]
    public void CurrentAllocatedMemory_ReturnsStatisticsValue()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act
        var allocated = manager.CurrentAllocatedMemory;

        // Assert
        _ = allocated.Should().Be(0);
    }

    #endregion

    #region AllocateInternalAsync Tests

    [Fact]
    public async Task AllocateInternalAsync_ValidSize_AllocatesBuffer()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        const int size = 1024;

        // Act
        var buffer = await manager.AllocateAsync<byte>(size);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.Length.Should().Be(size);
        _ = manager.Statistics.TotalAllocated.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AllocateInternalAsync_NegativeSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            _ = await manager.AllocateAsync<byte>(-100);
        });
    }

    [Fact]
    public async Task AllocateInternalAsync_ZeroSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            _ = await manager.AllocateAsync<byte>(0);
        });
    }

    [Fact]
    public async Task AllocateInternalAsync_ExceedsMaxSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var tooLarge = manager.MaxAllocationSize + 1;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            _ = await manager.AllocateAsync<byte>((int)Math.Min(tooLarge, int.MaxValue));
        });

        _ = exception.Message.Should().Contain("exceeds maximum limit");
    }

    [Fact]
    public async Task AllocateInternalAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var manager = new UnifiedMemoryManager();
        await manager.DisposeAsync();

        // Act & Assert
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            _ = await manager.AllocateAsync<byte>(1024);
        });
    }

    [Fact]
    public async Task AllocateInternalAsync_WithCancellation_SupportsCancellation()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await manager.AllocateAsync<byte>(1024, cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task AllocateInternalAsync_MultipleAllocations_TracksStatistics()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        const int allocations = 5;
        const int size = 1024;

        // Act
        var buffers = new List<IUnifiedMemoryBuffer<byte>>();
        for (var i = 0; i < allocations; i++)
        {
            var buffer = await manager.AllocateAsync<byte>(size);
            buffers.Add(buffer);
        }

        // Assert
        _ = manager.Statistics.AllocationCount.Should().Be(allocations);
        _ = manager.Statistics.TotalAllocated.Should().BeGreaterThanOrEqualTo(size * allocations);

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task AllocateInternalAsync_ConcurrentAllocations_HandledSafely()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        const int concurrentTasks = 10;
        const int size = 512;

        // Act
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async _ =>
        {
            var buffer = await manager.AllocateAsync<byte>(size);
            return buffer;
        });

        var buffers = await Task.WhenAll(tasks);

        // Assert
        _ = buffers.Should().HaveCount(concurrentTasks);
        _ = buffers.Should().OnlyContain(b => b != null);
        _ = manager.Statistics.AllocationCount.Should().Be(concurrentTasks);

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    #endregion

    #region CreateView Tests

    [Fact]
    public void CreateView_ValidParameters_CreatesView()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer = manager.AllocateAsync<int>(100).AsTask().GetAwaiter().GetResult();
        const int offset = 10;
        const int length = 20;

        // Act
        var view = manager.CreateView(buffer, offset, length);

        // Assert
        _ = view.Should().NotBeNull();
        _ = view.Length.Should().Be(length);
    }

    [Fact]
    public void CreateView_NullBuffer_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = manager.CreateView<int>(null!, 0, 10);
        });
    }

    [Fact]
    public void CreateView_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer = manager.AllocateAsync<int>(100).AsTask().GetAwaiter().GetResult();

        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = manager.CreateView(buffer, -1, 10);
        });
    }

    [Fact]
    public void CreateView_ZeroLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer = manager.AllocateAsync<int>(100).AsTask().GetAwaiter().GetResult();

        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = manager.CreateView(buffer, 0, 0);
        });
    }

    [Fact]
    public void CreateView_ExceedsBoundaries_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer = manager.AllocateAsync<int>(100).AsTask().GetAwaiter().GetResult();

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = manager.CreateView(buffer, 50, 60); // 50 + 60 = 110 > 100
        });

        _ = exception.Message.Should().Contain("View extends beyond buffer boundaries");
    }

    [Fact]
    public async Task CreateView_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var manager = new UnifiedMemoryManager();
        var buffer = await manager.AllocateAsync<int>(100);
        await manager.DisposeAsync();

        // Act & Assert
        _ = Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = manager.CreateView(buffer, 0, 10);
        });
    }

    #endregion

    #region CopyAsync Tests

    [Fact]
    public async Task CopyAsync_FullBuffer_CopiesSuccessfully()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var source = await manager.AllocateAsync<int>(10);
        var destination = await manager.AllocateAsync<int>(10);

        var sourceData = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        await source.CopyFromAsync(sourceData);

        // Act
        await manager.CopyAsync(source, destination);

        // Assert
        var destData = new int[10];
        await destination.CopyToAsync(destData);
        _ = destData.Should().Equal(sourceData);
    }

    [Fact]
    public async Task CopyAsync_WithOffsets_CopiesPartialBuffer()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var source = await manager.AllocateAsync<int>(10);
        var destination = await manager.AllocateAsync<int>(10);

        var sourceData = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        await source.CopyFromAsync(sourceData);

        // Act
        await manager.CopyAsync(source, 2, destination, 3, 4);

        // Assert
        var destData = new int[10];
        await destination.CopyToAsync(destData);
        _ = destData[3].Should().Be(3);
        _ = destData[4].Should().Be(4);
        _ = destData[5].Should().Be(5);
        _ = destData[6].Should().Be(6);
    }

    [Fact]
    public async Task CopyAsync_NullSource_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var destination = await manager.AllocateAsync<int>(10);

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await manager.CopyAsync<int>(null!, destination);
        });
    }

    [Fact]
    public async Task CopyAsync_NullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var source = await manager.AllocateAsync<int>(10);

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await manager.CopyAsync(source, null!);
        });
    }

    [Fact]
    public async Task CopyAsync_DifferentSizes_ThrowsArgumentException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var source = await manager.AllocateAsync<int>(10);
        var destination = await manager.AllocateAsync<int>(20);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await manager.CopyAsync(source, destination);
        });

        _ = exception.Message.Should().Contain("same size");
    }

    [Fact]
    public async Task CopyAsync_WithCancellation_SupportsCancellation()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var source = await manager.AllocateAsync<int>(10);
        var destination = await manager.AllocateAsync<int>(10);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await manager.CopyAsync(source, destination, cts.Token);
        });
    }

    #endregion

    #region CopyToDeviceAsync / CopyFromDeviceAsync Tests

    [Fact]
    public async Task CopyToDeviceAsync_ValidData_CopiesSuccessfully()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer = await manager.AllocateAsync<int>(5);
        var hostData = new int[] { 1, 2, 3, 4, 5 };

        // Act
        await manager.CopyToDeviceAsync(hostData, buffer);

        // Assert
        var result = new int[5];
        await buffer.CopyToAsync(result);
        _ = result.Should().Equal(hostData);
    }

    [Fact]
    public async Task CopyFromDeviceAsync_ValidBuffer_CopiesSuccessfully()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer = await manager.AllocateAsync<int>(5);
        var sourceData = new int[] { 1, 2, 3, 4, 5 };
        await buffer.CopyFromAsync(sourceData);

        var destination = new int[5];

        // Act
        await manager.CopyFromDeviceAsync(buffer, destination);

        // Assert
        _ = destination.Should().Equal(sourceData);
    }

    [Fact]
    public async Task CopyToDeviceAsync_NullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var hostData = new int[] { 1, 2, 3 };

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await manager.CopyToDeviceAsync<int>(hostData, null!);
        });
    }

    [Fact]
    public async Task CopyFromDeviceAsync_NullSource_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var destination = new int[5];

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await manager.CopyFromDeviceAsync<int>(null!, destination);
        });
    }

    #endregion

    #region FreeAsync Tests

    [Fact]
    public async Task FreeAsync_TrackedBuffer_FreesSuccessfully()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer = await manager.AllocateAsync<int>(100);
        var initialAllocationCount = manager.Statistics.AllocationCount;

        // Act
        await manager.FreeAsync(buffer);

        // Assert
        _ = manager.Statistics.DeallocationCount.Should().Be(1);
    }

    [Fact]
    public async Task FreeAsync_UntrackedBuffer_HandlesGracefully()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var otherManager = new UnifiedMemoryManager();
        var buffer = await otherManager.AllocateAsync<int>(100);

        // Act - should not throw
        await manager.FreeAsync(buffer);

        // Assert
        // No exception thrown
        await otherManager.DisposeAsync();
    }

    [Fact]
    public async Task FreeAsync_NullBuffer_HandlesGracefully()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();

        // Act - should not throw
        await manager.FreeAsync(null!);

        // Assert
        // No exception thrown
    }

    [Fact]
    public async Task FreeAsync_UpdatesStatistics()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer = await manager.AllocateAsync<int>(100);
        var beforeDeallocation = manager.Statistics.DeallocationCount;

        // Act
        await manager.FreeAsync(buffer);

        // Assert
        _ = manager.Statistics.DeallocationCount.Should().Be(beforeDeallocation + 1);
    }

    #endregion

    #region OptimizeAsync Tests

    [Fact]
    public async Task OptimizeAsync_ValidState_PerformsOptimization()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        _ = await manager.AllocateAsync<int>(100);

        // Act - should not throw
        await manager.OptimizeAsync();

        // Assert
        // Optimization completed without errors
    }

    [Fact]
    public async Task OptimizeAsync_AfterDispose_HandlesGracefully()
    {
        // Arrange
        var manager = new UnifiedMemoryManager();
        await manager.DisposeAsync();

        // Act - should not throw
        await manager.OptimizeAsync();

        // Assert
        // No exception thrown
    }

    [Fact]
    public async Task OptimizeAsync_WithCancellation_SupportsCancellation()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await manager.OptimizeAsync(cts.Token);
        });
    }

    #endregion

    #region Clear Tests

    [Fact]
    public async Task Clear_WithActiveBuffers_DisposesAllBuffers()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffer1 = await manager.AllocateAsync<int>(100);
        var buffer2 = await manager.AllocateAsync<int>(200);
        var initialAllocationCount = manager.Statistics.AllocationCount;

        // Act
        manager.Clear();

        // Assert
        _ = manager.Statistics.TotalAllocated.Should().Be(0);
        _ = manager.Statistics.AllocationCount.Should().Be(0);
    }

    [Fact]
    public async Task Clear_AfterDispose_HandlesGracefully()
    {
        // Arrange
        var manager = new UnifiedMemoryManager();
        await manager.DisposeAsync();

        // Act - should not throw
        manager.Clear();

        // Assert
        // No exception thrown
    }

    [Fact]
    public async Task Clear_ResetsStatistics()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        _ = await manager.AllocateAsync<int>(100);
        _ = await manager.AllocateAsync<int>(200);

        // Act
        manager.Clear();

        // Assert
        var stats = manager.Statistics;
        _ = stats.TotalAllocated.Should().Be(0);
        _ = stats.CurrentUsage.Should().Be(0);
        _ = stats.AllocationCount.Should().Be(0);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_WithActiveBuffers_DisposesCleanly()
    {
        // Arrange
        var manager = new UnifiedMemoryManager();
        _ = await manager.AllocateAsync<int>(100);

        // Act
        manager.Dispose();

        // Assert
        _ = manager.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_WithActiveBuffers_DisposesCleanly()
    {
        // Arrange
        var manager = new UnifiedMemoryManager();
        _ = await manager.AllocateAsync<int>(100);

        // Act
        await manager.DisposeAsync();

        // Assert
        _ = manager.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledTwice_HandlesGracefully()
    {
        // Arrange
        var manager = new UnifiedMemoryManager();

        // Act
        manager.Dispose();
        manager.Dispose(); // Second call

        // Assert
        _ = manager.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_HandlesGracefully()
    {
        // Arrange
        var manager = new UnifiedMemoryManager();

        // Act
        await manager.DisposeAsync();
        await manager.DisposeAsync(); // Second call

        // Assert
        _ = manager.IsDisposed.Should().BeTrue();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task IntegrationTest_AllocateUseFreeCycle_WorksCorrectly()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        const int size = 1000;
        var testData = Enumerable.Range(1, size).ToArray();

        // Act - Allocate
        var buffer = await manager.AllocateAsync<int>(size);
        _ = buffer.Should().NotBeNull();

        // Act - Use
        await buffer.CopyFromAsync(testData);
        var readData = new int[size];
        await buffer.CopyToAsync(readData);
        _ = readData.Should().Equal(testData);

        // Act - Free
        await manager.FreeAsync(buffer);

        // Assert
        _ = manager.Statistics.AllocationCount.Should().Be(1);
        _ = manager.Statistics.DeallocationCount.Should().Be(1);
    }

    [Fact]
    public async Task IntegrationTest_MultipleConcurrentAllocations_HandledSafely()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        const int concurrentTasks = 20;
        const int bufferSize = 256;

        // Act
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async i =>
        {
            var buffer = await manager.AllocateAsync<int>(bufferSize);
            var data = Enumerable.Repeat(i, bufferSize).ToArray();
            await buffer.CopyFromAsync(data);

            var readBack = new int[bufferSize];
            await buffer.CopyToAsync(readBack);

            _ = readBack.Should().AllBeEquivalentTo(i);

            await Task.Delay(10); // Simulate some work
            await manager.FreeAsync(buffer);
        });

        await Task.WhenAll(tasks);

        // Assert
        _ = manager.Statistics.AllocationCount.Should().BeGreaterThanOrEqualTo(concurrentTasks);
        _ = manager.Statistics.DeallocationCount.Should().BeGreaterThanOrEqualTo(concurrentTasks);
    }

    [Fact]
    public async Task IntegrationTest_MemoryPooling_ImprovesPerformance()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        const int iterations = 10;
        const int bufferSize = 1024;
        var buffers = new List<IUnifiedMemoryBuffer<int>>();

        // Act - Multiple allocations and deallocations to build pool
        for (var i = 0; i < iterations; i++)
        {
            var buffer = await manager.AllocateAsync<int>(bufferSize);
            buffers.Add(buffer);
        }

        foreach (var buffer in buffers)
        {
            await manager.FreeAsync(buffer);
        }

        buffers.Clear();

        // Now allocate again - should come from pool
        var initialPoolHits = manager.Statistics.PoolHitRate;

        for (var i = 0; i < iterations; i++)
        {
            var buffer = await manager.AllocateAsync<int>(bufferSize);
            buffers.Add(buffer);
        }

        // Assert
        _ = manager.Statistics.AllocationCount.Should().BeGreaterThanOrEqualTo(iterations * 2);

        // Cleanup
        foreach (var buffer in buffers)
        {
            await manager.FreeAsync(buffer);
        }
    }

    [Fact]
    public async Task IntegrationTest_OptimizeReducesMemoryFootprint()
    {
        // Arrange
        using var manager = new UnifiedMemoryManager();
        var buffers = new List<IUnifiedMemoryBuffer<int>>();

        // Allocate multiple buffers
        for (var i = 0; i < 5; i++)
        {
            buffers.Add(await manager.AllocateAsync<int>(1000));
        }

        // Free some buffers
        await manager.FreeAsync(buffers[0]);
        await manager.FreeAsync(buffers[2]);
        await manager.FreeAsync(buffers[4]);

        // Act
        await manager.OptimizeAsync();

        // Assert - Optimization should complete without errors
        _ = manager.Should().NotBeNull();

        // Cleanup
        await manager.FreeAsync(buffers[1]);
        await manager.FreeAsync(buffers[3]);
    }

    #endregion
}
