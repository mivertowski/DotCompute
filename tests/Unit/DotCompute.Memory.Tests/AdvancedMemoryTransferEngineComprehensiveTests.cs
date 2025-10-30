// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory.Types;
using Moq;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for AdvancedMemoryTransferEngine covering all functionality.
/// Target: 80%+ coverage for 566-line complex production class.
/// </summary>
public class AdvancedMemoryTransferEngineComprehensiveTests : IAsyncDisposable
{
    private readonly Mock<IUnifiedMemoryManager> _mockMemoryManager;
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<IUnifiedMemoryBuffer<byte>> _mockBuffer;

    public AdvancedMemoryTransferEngineComprehensiveTests()
    {
        _mockMemoryManager = new Mock<IUnifiedMemoryManager>();
        _mockAccelerator = new Mock<IAccelerator>();
        _mockBuffer = new Mock<IUnifiedMemoryBuffer<byte>>();

        // Setup default behaviors
        _ = _mockAccelerator.Setup(a => a.Type).Returns(DotCompute.Abstractions.AcceleratorType.CPU);
        _ = _mockBuffer.Setup(b => b.Length).Returns(1024);
        _ = _mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public async Task Constructor_WithValidMemoryManager_InitializesSuccessfully()
    {
        // Act
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);

        // Assert
        _ = engine.Should().NotBeNull();
        _ = engine.Statistics.Should().NotBeNull();
    }

    [Fact]
    public async Task Constructor_WithNullMemoryManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            await using var engine = new AdvancedMemoryTransferEngine(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException ex)
        {
            _ = ex.ParamName.Should().Be("memoryManager");
        }
    }

    [Fact]
    public async Task Constructor_InitializesStatistics()
    {
        // Act
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);

        // Assert
        var stats = engine.Statistics;
        _ = stats.TotalBytesTransferred.Should().Be(0);
        _ = stats.TotalTransferCount.Should().Be(0);
        _ = stats.AverageTransferSize.Should().Be(0);
    }

    #endregion

    #region TransferLargeDatasetAsync - Small Dataset Tests

    [Fact]
    public async Task TransferLargeDatasetAsync_SmallDataset_UsesStandardTransfer()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[100]; // Small dataset

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Success.Should().BeTrue();
        _ = result.UsedStreaming.Should().BeFalse();
        _ = result.UsedMemoryMapping.Should().BeFalse();
        _ = result.ChunkCount.Should().Be(1);
    }

    [Fact]
    public async Task TransferLargeDatasetAsync_WithNullOptions_UsesDefaults()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[100];

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object, options: null);

        // Assert
        _ = result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TransferLargeDatasetAsync_EmptyDataset_HandlesCorrectly()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = Array.Empty<int>();

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.TotalBytes.Should().Be(0);
    }

    #endregion

    #region TransferLargeDatasetAsync - Error Handling Tests

    [Fact]
    public async Task TransferLargeDatasetAsync_AllocationFails_ReturnsFailureResult()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[100];

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Task.FromException<IUnifiedMemoryBuffer<int>>(new OutOfMemoryException("Test exception"))));

        // Act
        var result = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("Test exception");
    }

    [Fact]
    public async Task TransferLargeDatasetAsync_WithCancellation_SupportsCancellation()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[100];
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Task.FromException<IUnifiedMemoryBuffer<int>>(new OperationCanceledException())));

        // Act
        var result = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object, cancellationToken: cts.Token);

        // Assert
        _ = result.Success.Should().BeFalse();
    }

    #endregion

    #region TransferLargeDatasetAsync - Performance Metrics Tests

    [Fact]
    public async Task TransferLargeDatasetAsync_CalculatesPerformanceMetrics()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[1000];

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object);

        // Assert
        _ = result.ThroughputMBps.Should().BeGreaterThan(0);
        _ = result.EfficiencyRatio.Should().BeGreaterThanOrEqualTo(0);
        _ = result.EfficiencyRatio.Should().BeLessThanOrEqualTo(1.0);
        _ = result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task TransferLargeDatasetAsync_UpdatesStatistics()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[1000];

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        var initialStats = engine.Statistics;
        var initialCount = initialStats.TotalTransferCount;
        var initialBytes = initialStats.TotalBytesTransferred;

        // Act
        _ = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object);

        // Assert
        var finalStats = engine.Statistics;
        _ = finalStats.TotalTransferCount.Should().Be(initialCount + 1);
        _ = finalStats.TotalBytesTransferred.Should().BeGreaterThan(initialBytes);
    }

    #endregion

    #region TransferLargeDatasetAsync - Options Tests

    [Fact]
    public async Task TransferLargeDatasetAsync_WithCompression_SetsCompressionFlag()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[100];
        var options = new TransferOptions { EnableCompression = true };

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object, options);

        // Assert
        _ = result.UsedCompression.Should().BeTrue();
    }

    [Fact]
    public async Task TransferLargeDatasetAsync_WithoutCompression_ClearsCompressionFlag()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[100];
        var options = new TransferOptions { EnableCompression = false };

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object, options);

        // Assert
        _ = result.UsedCompression.Should().BeFalse();
    }

    #endregion

    #region ExecuteConcurrentTransfersAsync Tests

    [Fact]
    public async Task ExecuteConcurrentTransfersAsync_MultipleDatasets_TransfersAllSuccessfully()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var dataSets = new[]
        {
            new int[100],
            new int[200],
            new int[300]
        };

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.ExecuteConcurrentTransfersAsync(dataSets, _mockAccelerator.Object);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Success.Should().BeTrue();
        _ = result.TransferCount.Should().Be(3);
        _ = result.SuccessfulTransfers.Should().Be(3);
        _ = result.FailedTransfers.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteConcurrentTransfersAsync_WithNullOptions_UsesDefaults()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var dataSets = new[] { new int[100] };

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.ExecuteConcurrentTransfersAsync(dataSets, _mockAccelerator.Object, options: null);

        // Assert
        _ = result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteConcurrentTransfersAsync_CalculatesConcurrencyBenefit()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var dataSets = new[]
        {
            new int[100],
            new int[100]
        };

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.ExecuteConcurrentTransfersAsync(dataSets, _mockAccelerator.Object);

        // Assert
        _ = result.ConcurrencyBenefit.Should().BeGreaterThanOrEqualTo(0);
        _ = result.ConcurrencyBenefit.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task ExecuteConcurrentTransfersAsync_CalculatesAverageThroughput()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var dataSets = new[]
        {
            new int[1000],
            new int[1000]
        };

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result = await engine.ExecuteConcurrentTransfersAsync(dataSets, _mockAccelerator.Object);

        // Assert
        _ = result.AverageThroughputMBps.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteConcurrentTransfersAsync_WithPartialFailure_ReportsCorrectly()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var dataSets = new[]
        {
            new int[100],
            new int[200],
            new int[300]
        };

        var callCount = 0;
        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 2)
                {
                    return new ValueTask<IUnifiedMemoryBuffer<int>>(Task.FromException<IUnifiedMemoryBuffer<int>>(new InvalidOperationException("Test failure")));
                }
                return new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>());
            });

        // Act
        var result = await engine.ExecuteConcurrentTransfersAsync(dataSets, _mockAccelerator.Object);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Success.Should().BeFalse();
        _ = result.SuccessfulTransfers.Should().Be(2);
        _ = result.FailedTransfers.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteConcurrentTransfersAsync_WithCancellation_SupportsCancellation()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var dataSets = new[] { new int[100] };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Task.FromException<IUnifiedMemoryBuffer<int>>(new OperationCanceledException())));

        // Act & Assert
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await engine.ExecuteConcurrentTransfersAsync(dataSets, _mockAccelerator.Object, cancellationToken: cts.Token);
        });
    }

    #endregion

    #region Statistics Property Tests

    [Fact]
    public async Task Statistics_InitialState_ReturnsZeroValues()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);

        // Act
        var stats = engine.Statistics;

        // Assert
        _ = stats.TotalBytesTransferred.Should().Be(0);
        _ = stats.TotalTransferCount.Should().Be(0);
        _ = stats.AverageTransferSize.Should().Be(0);
        _ = stats.ActiveTransfers.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Statistics_AfterTransfer_ReflectsActivity()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[1000];

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        _ = await engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object);
        var stats = engine.Statistics;

        // Assert
        _ = stats.TotalTransferCount.Should().BeGreaterThan(0);
        _ = stats.TotalBytesTransferred.Should().BeGreaterThan(0);
        _ = stats.AverageTransferSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Statistics_MultipleReads_ReturnsConsistentData()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);

        // Act
        var stats1 = engine.Statistics;
        var stats2 = engine.Statistics;

        // Assert
        _ = stats1.TotalBytesTransferred.Should().Be(stats2.TotalBytesTransferred);
        _ = stats1.TotalTransferCount.Should().Be(stats2.TotalTransferCount);
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_CalledOnce_DisposesCleanly()
    {
        // Arrange
        var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);

        // Act
        await engine.DisposeAsync();

        // Assert
        // Should complete without exceptions
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_HandlesGracefully()
    {
        // Arrange
        var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);

        // Act
        await engine.DisposeAsync();
        await engine.DisposeAsync(); // Second call

        // Assert
        // Should complete without exceptions
    }

    [Fact]
    public async Task DisposeAsync_WithPendingTransfers_CompletesGracefully()
    {
        // Arrange
        var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);
        var data = new int[100];

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Start a transfer but don't await it
        var transferTask = engine.TransferLargeDatasetAsync(data, _mockAccelerator.Object);

        // Act
        await engine.DisposeAsync();

        // Assert
        // Disposal should complete even with pending transfer
        _ = await transferTask; // Complete the transfer
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task IntegrationTest_MultipleSequentialTransfers_WorkCorrectly()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        // Act
        var result1 = await engine.TransferLargeDatasetAsync(new int[100], _mockAccelerator.Object);
        var result2 = await engine.TransferLargeDatasetAsync(new int[200], _mockAccelerator.Object);
        var result3 = await engine.TransferLargeDatasetAsync(new int[300], _mockAccelerator.Object);

        // Assert
        _ = result1.Success.Should().BeTrue();
        _ = result2.Success.Should().BeTrue();
        _ = result3.Success.Should().BeTrue();

        var stats = engine.Statistics;
        _ = stats.TotalTransferCount.Should().Be(3);
    }

    [Fact]
    public async Task IntegrationTest_MixedTransferSizes_HandlesCorrectly()
    {
        // Arrange
        await using var engine = new AdvancedMemoryTransferEngine(_mockMemoryManager.Object);

        _ = _mockMemoryManager
            .Setup(m => m.AllocateAndCopyAsync<int>(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IUnifiedMemoryBuffer<int>>(Mock.Of<IUnifiedMemoryBuffer<int>>()));

        var sizes = new[] { 10, 100, 1000, 10000 };

        // Act & Assert
        foreach (var size in sizes)
        {
            var result = await engine.TransferLargeDatasetAsync(new int[size], _mockAccelerator.Object);
            _ = result.Success.Should().BeTrue();
            _ = result.TotalBytes.Should().Be(size * sizeof(int));
        }
    }

    #endregion
}
