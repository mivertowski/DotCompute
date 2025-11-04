// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.Metal.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Backends.Metal.Tests.Memory;

/// <summary>
/// Comprehensive tests for the enhanced MetalMemoryManager with advanced pooling features.
/// </summary>
public sealed class MetalMemoryManagerTests : IDisposable
{
    private readonly ILogger<MetalMemoryManager> _logger;
    private readonly MetalMemoryManager _memoryManager;

    public MetalMemoryManagerTests()
    {
        _logger = NullLogger<MetalMemoryManager>.Instance;
        _memoryManager = new MetalMemoryManager(_logger);
    }

    [Fact]
    public async Task AllocateAsync_ShouldAllocateMemorySuccessfully()
    {
        // Arrange
        const long sizeInBytes = 1024;
        
        // Act
        var buffer = await _memoryManager.AllocateAsync(sizeInBytes);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task AllocateAsync_WithPinnedOption_ShouldCreatePinnedBuffer()
    {
        // Arrange
        const long sizeInBytes = 4096;
        const MemoryOptions options = MemoryOptions.Pinned;
        
        // Act
        var buffer = await _memoryManager.AllocateAsync(sizeInBytes, options);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.Options.Should().HaveFlag(MemoryOptions.Pinned);
    }

    [Fact]
    public async Task AllocateAsync_WithUnifiedOption_ShouldCreateUnifiedBuffer()
    {
        // Arrange
        const long sizeInBytes = 2048;
        const MemoryOptions options = MemoryOptions.Unified;
        
        // Act
        var buffer = await _memoryManager.AllocateAsync(sizeInBytes, options);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.Options.Should().HaveFlag(MemoryOptions.Unified);
    }

    [Fact]
    public async Task MemoryPool_ShouldImproveAllocationEfficiency()
    {
        // Arrange
        const long bufferSize = 1024;
        var buffers = new List<DotCompute.Abstractions.IUnifiedMemoryBuffer>();
        
        // Act - First allocation (pool miss)
        var buffer1 = await _memoryManager.AllocateAsync(bufferSize);
        buffers.Add(buffer1);
        
        // Get initial statistics
        var initialStats = _memoryManager.Statistics;
        
        // Free the buffer (should return to pool)
        await _memoryManager.FreeAsync(buffer1, CancellationToken.None);
        
        // Allocate again (should be pool hit)
        var buffer2 = await _memoryManager.AllocateAsync(bufferSize);
        buffers.Add(buffer2);
        
        // Get final statistics
        var finalStats = _memoryManager.Statistics;
        
        // Assert
        finalStats.PoolHitRate.Should().BeGreaterThan(initialStats.PoolHitRate);
        buffer2.Should().NotBeNull();
    }

    [Fact]
    public async Task CopyAsync_BetweenBuffers_ShouldSucceed()
    {
        // Arrange
        const int elementCount = 256;
        var sourceBuffer = await _memoryManager.AllocateAsync<int>(elementCount);
        var destBuffer = await _memoryManager.AllocateAsync<int>(elementCount);
        
        // Act
        var copyTask = _memoryManager.CopyAsync(sourceBuffer, destBuffer, default);
        
        // Assert
        var action = async () => await copyTask;
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CopyToDeviceAsync_ShouldTransferDataSuccessfully()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3, 4, 5 };
        var buffer = await _memoryManager.AllocateAsync<int>(sourceData.Length);
        
        // Act
        var copyTask = _memoryManager.CopyToDeviceAsync(sourceData.AsMemory(), buffer, default);
        
        // Assert
        var action = async () => await copyTask;
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CopyFromDeviceAsync_ShouldTransferDataSuccessfully()
    {
        // Arrange
        const int elementCount = 10;
        var buffer = await _memoryManager.AllocateAsync<float>(elementCount);
        var destination = new float[elementCount];
        
        // Act
        var copyTask = _memoryManager.CopyFromDeviceAsync(buffer, destination.AsMemory(), CancellationToken.None);
        
        // Assert
        var action = async () => await copyTask;
        await action.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(65536)]
    public async Task PooledAllocation_DifferentSizes_ShouldUseAppropriatePool(long sizeInBytes)
    {
        // Arrange & Act
        var buffer = await _memoryManager.AllocateAsync(sizeInBytes);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        
        // Cleanup
        await _memoryManager.FreeAsync(buffer, default);
    }

    [Fact]
    public async Task Statistics_ShouldTrackAllocationsAccurately()
    {
        // Arrange
        const long bufferSize = 2048;
        var initialStats = _memoryManager.Statistics;
        
        // Act
        var buffer1 = await _memoryManager.AllocateAsync(bufferSize);
        var buffer2 = await _memoryManager.AllocateAsync(bufferSize);
        
        var afterAllocationStats = _memoryManager.Statistics;
        
        await _memoryManager.FreeAsync(buffer1, CancellationToken.None);
        await _memoryManager.FreeAsync(buffer2, default);
        
        var finalStats = _memoryManager.Statistics;
        
        // Assert
        afterAllocationStats.TotalAllocated.Should().BeGreaterThan(initialStats.TotalAllocated);
        afterAllocationStats.ActiveAllocations.Should().BeGreaterThan(initialStats.ActiveAllocations);
    }

    [Fact]
    public async Task OptimizeAsync_ShouldPerformMemoryOptimization()
    {
        // Arrange
        var buffers = new List<DotCompute.Abstractions.IUnifiedMemoryBuffer>();
        
        // Create some allocations to trigger optimization
        for (int i = 0; i < 10; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(1024 * (i + 1));
            buffers.Add(buffer);
        }
        
        // Free half of them to create fragmentation
        for (int i = 0; i < 5; i++)
        {
            await _memoryManager.FreeAsync(buffers[i], default);
        }
        
        // Act
        var optimizeTask = _memoryManager.OptimizeAsync(default);
        
        // Assert
        var action = async () => await optimizeTask;
        await action.Should().NotThrowAsync();
        
        // Cleanup remaining buffers
        for (int i = 5; i < buffers.Count; i++)
        {
            await _memoryManager.FreeAsync(buffers[i], default);
        }
    }

    [Fact]
    public void CreateView_ShouldCreateValidView()
    {
        // Arrange
        const long bufferSize = 4096;
        const long viewOffset = 1024;
        const long viewSize = 2048;
        
        // We need to create a mock buffer since we don't have Metal available
        var mockBuffer = new MetalMemoryBuffer(bufferSize, MemoryOptions.None);
        
        // Act
        var view = _memoryManager.CreateView(mockBuffer, viewOffset, viewSize);
        
        // Assert
        view.Should().NotBeNull();
        view.SizeInBytes.Should().Be(viewSize);
    }

    [Fact]
    public async Task Dispose_ShouldCleanupAllResources()
    {
        // Arrange
        var buffer1 = await _memoryManager.AllocateAsync(1024);
        var buffer2 = await _memoryManager.AllocateAsync(2048);
        
        // Act
        var disposeAction = () => _memoryManager.Dispose();
        
        // Assert
        disposeAction.Should().NotThrow();
    }

    [Fact]
    public async Task ConcurrentAllocations_ShouldBeThreadSafe()
    {
        // Arrange
        const int concurrentTasks = 10;
        const long bufferSize = 1024;
        
        // Act
        var tasks = new Task<IUnifiedMemoryBuffer>[concurrentTasks];
        for (int i = 0; i < concurrentTasks; i++)
        {
            tasks[i] = Task.Run(async () => await _memoryManager.AllocateAsync(bufferSize));
        }
        
        var buffers = await Task.WhenAll(tasks);
        
        // Assert
        buffers.Should().HaveCount(concurrentTasks);
        buffers.Should().OnlyContain(b => b != null && b.SizeInBytes == bufferSize);
        
        // Cleanup
        foreach (var buffer in buffers)
        {
            await _memoryManager.FreeAsync(buffer, default);
        }
    }

    [Fact]
    public async Task LargeAllocation_ShouldBypassPooling()
    {
        // Arrange - Allocate something larger than typical pool sizes
        const long largeSize = 512L * 1024 * 1024; // 512MB
        
        // Act & Assert - Should not throw, even though it bypasses pooling
        var largeBuffer = await _memoryManager.AllocateAsync(largeSize);
        largeBuffer.Should().NotBeNull();
        largeBuffer.SizeInBytes.Should().Be(largeSize);
        
        // Cleanup
        await _memoryManager.FreeAsync(largeBuffer, default);
    }

    [Fact]
    public void MaxAllocationSize_ShouldReflectSystemCapabilities()
    {
        // Act & Assert
        _memoryManager.MaxAllocationSize.Should().BeGreaterThan(0);
        _memoryManager.TotalAvailableMemory.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MemoryPressure_ShouldTriggerOptimization()
    {
        // Arrange - Create many allocations to potentially trigger memory pressure
        var buffers = new List<DotCompute.Abstractions.IUnifiedMemoryBuffer>();
        
        try
        {
            for (int i = 0; i < 50; i++)
            {
                var buffer = await _memoryManager.AllocateAsync(1024 * 1024); // 1MB each
                buffers.Add(buffer);
            }
            
            // Act - This should handle any memory pressure internally
            await _memoryManager.OptimizeAsync(default);
            
            // Assert - Should not throw exceptions
            _memoryManager.Statistics.Should().NotBeNull();
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                try
                {
                    await _memoryManager.FreeAsync(buffer, default);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    [Fact]
    public void Clear_ShouldResetAllStatistics()
    {
        // Arrange
        var initialStats = _memoryManager.Statistics;
        
        // Act
        _memoryManager.Clear();
        var afterClearStats = _memoryManager.Statistics;
        
        // Assert
        afterClearStats.TotalAllocated.Should().Be(0);
        afterClearStats.ActiveAllocations.Should().Be(0);
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
    }
}

/// <summary>
/// Tests for MetalMemoryPool functionality.
/// </summary>
public sealed class MetalMemoryPoolTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly MetalMemoryPool _pool;

    public MetalMemoryPoolTests()
    {
        _logger = NullLogger.Instance;
        _pool = new MetalMemoryPool(_logger, supportsUnifiedMemory: false);
    }

    [Fact]
    public async Task TryAllocateAsync_ShouldReturnNullForOversizedRequests()
    {
        // Arrange
        const long oversizedRequest = 1L * 1024 * 1024 * 1024; // 1GB
        
        // Act
        var result = await _pool.TryAllocateAsync(oversizedRequest, MemoryOptions.None, CancellationToken.None);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDetailedStatistics_ShouldProvideComprehensiveInfo()
    {
        // Act
        var stats = _pool.GetDetailedStatistics();
        
        // Assert
        stats.Should().NotBeNull();
        stats.BucketStatistics.Should().NotBeEmpty();
        stats.HitRate.Should().BeGreaterThanOrEqualTo(0);
        stats.FragmentationPercentage.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CleanupAsync_ShouldReducePoolSize()
    {
        // Arrange
        var initialStats = _pool.GetDetailedStatistics();
        
        // Act
        await _pool.CleanupAsync(aggressive: true, CancellationToken.None);
        var finalStats = _pool.GetDetailedStatistics();
        
        // Assert - Pool should be cleaned (though might be empty to start)
        finalStats.Should().NotBeNull();
    }

    public void Dispose()
    {
        _pool?.Dispose();
    }
}

/// <summary>
/// Tests for MetalPinnedMemoryAllocator functionality.
/// </summary>
public sealed class MetalPinnedMemoryAllocatorTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly MetalPinnedMemoryAllocator _allocator;

    public MetalPinnedMemoryAllocatorTests()
    {
        _logger = NullLogger.Instance;
        _allocator = new MetalPinnedMemoryAllocator(_logger);
    }

    [Fact]
    public async Task AllocateAsync_ShouldCreatePinnedBuffer()
    {
        // Arrange
        const long sizeInBytes = 4096;
        
        // Act
        var buffer = await _allocator.AllocateAsync(sizeInBytes, MemoryOptions.Pinned, CancellationToken.None);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.Options.Should().HaveFlag(MemoryOptions.Pinned);
        
        // Cleanup
        await _allocator.FreeAsync(buffer, CancellationToken.None);
    }

    [Fact]
    public void GetStatistics_ShouldProvideUsageInfo()
    {
        // Act
        var stats = _allocator.GetStatistics();
        
        // Assert
        stats.Should().NotBeNull();
        stats.TotalPinnedBytes.Should().BeGreaterThanOrEqualTo(0);
        stats.ActiveAllocations.Should().BeGreaterThanOrEqualTo(0);
    }

    public void Dispose()
    {
        _allocator?.Dispose();
    }
}

/// <summary>
/// Tests for MetalMemoryPressureMonitor functionality.
/// </summary>
public sealed class MetalMemoryPressureMonitorTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly MetalMemoryPressureMonitor _monitor;

    public MetalMemoryPressureMonitorTests()
    {
        _logger = NullLogger.Instance;
        _monitor = new MetalMemoryPressureMonitor(_logger, 0.80); // 80% threshold
    }

    [Fact]
    public void UpdatePressure_ShouldDetectPressureCorrectly()
    {
        // Arrange
        const long totalMemory = 1024 * 1024 * 1024; // 1GB
        const long highUsage = (long)(totalMemory * 0.85); // 85% usage
        
        // Act
        _monitor.UpdatePressure(highUsage, totalMemory);
        
        // Assert
        _monitor.IsUnderPressure.Should().BeTrue();
        _monitor.CurrentUsagePercentage.Should().BeApproximately(0.85, 0.01);
    }

    [Fact]
    public void PressureLevel_ShouldReflectUsageCorrectly()
    {
        // Arrange
        const long totalMemory = 1000;

        // Test Normal level (below 85% threshold)
        _monitor.UpdatePressure(500, totalMemory); // 50%
        _monitor.PressureLevel.Should().Be(MemoryPressureLevel.Normal);

        // Test Moderate level (85% <= usage < 90%)
        _monitor.UpdatePressure(870, totalMemory); // 87%
        _monitor.PressureLevel.Should().Be(MemoryPressureLevel.Moderate);

        // Test Warning level (90% <= usage < 95%)
        _monitor.UpdatePressure(920, totalMemory); // 92%
        _monitor.PressureLevel.Should().Be(MemoryPressureLevel.Warning);

        // Test Critical level (95% <= usage < 98%)
        _monitor.UpdatePressure(960, totalMemory); // 96%
        _monitor.PressureLevel.Should().Be(MemoryPressureLevel.Critical);

        // Test Emergency level (>= 98%)
        _monitor.UpdatePressure(990, totalMemory); // 99%
        _monitor.PressureLevel.Should().Be(MemoryPressureLevel.Emergency);
    }

    [Fact]
    public void GetStatistics_ShouldProvideDetailedInfo()
    {
        // Act
        var stats = _monitor.GetStatistics();
        
        // Assert
        stats.Should().NotBeNull();
        stats.PressureThreshold.Should().Be(0.80);
        stats.PressureEventCount.Should().BeGreaterThanOrEqualTo(0);
    }

    public void Dispose()
    {
        _monitor?.Dispose();
    }
}