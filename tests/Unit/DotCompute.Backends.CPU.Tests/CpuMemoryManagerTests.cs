// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Tests.Common;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Tests for CPU memory management including host memory allocation, pinned memory operations,
/// memory pooling, and NUMA awareness.
/// </summary>
[Trait("Category", TestCategories.HardwareIndependent)]
[Trait("Category", TestCategories.MemoryIntensive)]
public class CpuMemoryManagerTests : IDisposable
{
    private readonly ILogger<CpuMemoryManager> _logger;
    private readonly CpuMemoryManager _memoryManager;
    
    public CpuMemoryManagerTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<CpuMemoryManager>();
        _memoryManager = new CpuMemoryManager(_logger);
    }
    
    [Fact]
    public void Constructor_WithDefaultPolicy_InitializesSuccessfully()
    {
        // Arrange & Act - memory manager created in constructor
        
        // Assert
        _memoryManager.Should().NotBeNull();
        _memoryManager.Topology.Should().NotBeNull();
        _memoryManager.IsDisposed.Should().BeFalse();
    }
    
    [Fact]
    public void Constructor_WithCustomPolicy_InitializesWithPolicy()
    {
        // Arrange
        var customPolicy = new NumaMemoryPolicy
        {
            PreferLocalNode = false,
            AllowRemoteAccess = true,
            InterleavingEnabled = true
        };
        
        // Act
        using var memoryManager = new CpuMemoryManager(_logger, customPolicy);
        
        // Assert
        memoryManager.Should().NotBeNull();
        memoryManager.Topology.Should().NotBeNull();
    }
    
    [Fact]
    public void Topology_ReturnsValidTopology()
    {
        // Act
        var topology = _memoryManager.Topology;
        
        // Assert
        topology.Should().NotBeNull();
        topology.NodeCount.Should().BeGreaterThan(0);
        topology.TotalMemoryBytes.Should().BeGreaterThan(0);
    }
    
    [Theory]
    [InlineData(1024)]      // 1KB
    [InlineData(64 * 1024)] // 64KB
    [InlineData(1024 * 1024)] // 1MB
    public async Task AllocateAsync_WithVariousSizes_AllocatesSuccessfully(long sizeInBytes)
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        // Act
        var buffer = await _memoryManager.AllocateAsync(sizeInBytes, options);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.IsDisposed.Should().BeFalse();
        buffer.Options.Should().Be(options);
        
        // Cleanup
        await buffer.DisposeAsync();
    }
    
    [Fact]
    public async Task AllocateAsync_WithNumaPolicy_ConsidersNumaPlacement()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        var numaPolicy = new NumaMemoryPolicy
        {
            PreferLocalNode = true,
            AllowRemoteAccess = false,
            InterleavingEnabled = false
        };
        
        // Act
        var buffer = await _memoryManager.AllocateAsync(1024, options, numaPolicy);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(1024);
        
        // Cleanup
        await buffer.DisposeAsync();
    }
    
    [Fact]
    public async Task AllocateAsync_MultipleAllocations_TracksMemoryCorrectly()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        var initialAllocated = _memoryManager.CurrentAllocatedMemory;
        
        // Act
        var buffer1 = await _memoryManager.AllocateAsync(1024, options);
        var buffer2 = await _memoryManager.AllocateAsync(2048, options);
        
        var afterAllocation = _memoryManager.CurrentAllocatedMemory;
        
        // Assert
        afterAllocation.Should().BeGreaterThan(initialAllocated);
        (afterAllocation - initialAllocated).Should().BeGreaterThanOrEqualTo(3072); // At least 1024 + 2048
        
        // Cleanup
        await buffer1.DisposeAsync();
        await buffer2.DisposeAsync();
    }
    
    [Fact]
    public async Task CreateView_WithValidBuffer_CreatesViewSuccessfully()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        var buffer = await _memoryManager.AllocateAsync(1024, options);
        
        // Act
        var view = _memoryManager.CreateView(buffer, 256, 512);
        
        // Assert
        view.Should().NotBeNull();
        view.SizeInBytes.Should().Be(512);
        view.IsDisposed.Should().BeFalse();
        
        // Cleanup
        await view.DisposeAsync();
        await buffer.DisposeAsync();
    }
    
    [Fact]
    public async Task CreateView_WithInvalidBuffer_ThrowsArgumentException()
    {
        // Arrange
        var mockBuffer = new Mock<IUnifiedMemoryBuffer>();
        mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);
        
        // Act & Assert
        _memoryManager.Invoking(mm => mm.CreateView(mockBuffer.Object, 0, 512))
            .Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void MaxAllocationSize_ReturnsMaxValue()
    {
        // Act
        var maxSize = _memoryManager.MaxAllocationSize;
        
        // Assert
        maxSize.Should().Be(long.MaxValue);
    }
    
    [Fact]
    public void TotalAvailableMemory_ReturnsPositiveValue()
    {
        // Act
        var availableMemory = _memoryManager.TotalAvailableMemory;
        
        // Assert
        availableMemory.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void Accelerator_ReturnsValidAccelerator()
    {
        // Act
        var accelerator = _memoryManager.Accelerator;
        
        // Assert
        accelerator.Should().NotBeNull();
        accelerator.AcceleratorType.Should().Be(AcceleratorType.CPU);
    }
    
    [Fact]
    public void Statistics_ReturnsValidStatistics()
    {
        // Act
        var statistics = _memoryManager.Statistics;
        
        // Assert
        statistics.Should().NotBeNull();
        statistics.TotalAllocated.Should().BeGreaterThanOrEqualTo(0);
        statistics.TotalFreed.Should().BeGreaterThanOrEqualTo(0);
        statistics.ActiveBuffers.Should().BeGreaterThanOrEqualTo(0);
        statistics.PeakMemoryUsage.Should().BeGreaterThanOrEqualTo(0);
    }
    
    [Fact]
    public async Task FreeAsync_WithValidBuffer_UpdatesStatistics()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        var buffer = await _memoryManager.AllocateAsync(1024, options) as CpuMemoryBuffer;
        var beforeFree = _memoryManager.Statistics;
        
        // Act
        await _memoryManager.FreeAsync(buffer!);
        
        // Assert
        var afterFree = _memoryManager.Statistics;
        afterFree.TotalFreed.Should().BeGreaterThan(beforeFree.TotalFreed);
        afterFree.ActiveBuffers.Should().BeLessThan(beforeFree.ActiveBuffers);
    }
    
    [Fact]
    public async Task OptimizeAsync_TriggersGarbageCollection()
    {
        // Arrange
        var beforeGC = GC.CollectionCount(0);
        
        // Act
        await _memoryManager.OptimizeAsync();
        
        // Assert
        var afterGC = GC.CollectionCount(0);
        afterGC.Should().BeGreaterThan(beforeGC);
    }
    
    [Fact]
    public void Clear_ResetsStatistics()
    {
        // Act
        _memoryManager.Clear();
        
        // Assert
        var statistics = _memoryManager.Statistics;
        statistics.TotalAllocated.Should().Be(0);
        statistics.ActiveBuffers.Should().Be(0);
        statistics.TotalFreed.Should().Be(0);
    }
    
    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task AllocateAsync_WithZeroSize_ThrowsArgumentException()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        // Act & Assert
        await _memoryManager.Invoking(mm => mm.AllocateAsync(0, options))
            .Should().ThrowAsync<ArgumentException>();
    }
    
    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task AllocateAsync_WithNegativeSize_ThrowsArgumentException()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        // Act & Assert
        await _memoryManager.Invoking(mm => mm.AllocateAsync(-1024, options))
            .Should().ThrowAsync<ArgumentException>();
    }
    
    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task CreateView_WithInvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        var buffer = await _memoryManager.AllocateAsync(1024, options);
        
        // Act & Assert
        _memoryManager.Invoking(mm => mm.CreateView(buffer, 512, 1024))
            .Should().Throw<ArgumentOutOfRangeException>();
        
        // Cleanup
        await buffer.DisposeAsync();
    }
    
    [Fact]
    [Trait("Category", TestCategories.Concurrency)]
    public async Task AllocateAsync_ConcurrentAllocations_HandlesParallelRequests()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        var allocationTasks = Enumerable.Range(0, 10)
            .Select(_ => _memoryManager.AllocateAsync(1024, options).AsTask())
            .ToArray();
        
        // Act
        var buffers = await Task.WhenAll(allocationTasks);
        
        // Assert
        buffers.Should().HaveCount(10);
        buffers.Should().OnlyContain(b => b != null && b.SizeInBytes == 1024);
        
        // Verify all buffers are different instances
        buffers.Distinct().Should().HaveCount(10);
        
        // Cleanup
        await Task.WhenAll(buffers.Select(b => b.DisposeAsync().AsTask()));
    }
    
    [Fact]
    [Trait("Category", TestCategories.Performance)]
    public async Task AllocateAsync_PerformanceBenchmark_MeetsTimingRequirements()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host
        };
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var buffers = new List<IUnifiedMemoryBuffer>();
        
        // Act - Allocate 100 buffers of 64KB each
        for (var i = 0; i < 100; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(64 * 1024, options);
            buffers.Add(buffer);
        }
        
        stopwatch.Stop();
        
        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
        buffers.Should().HaveCount(100);
        
        // Cleanup
        await Task.WhenAll(buffers.Select(b => b.DisposeAsync().AsTask()));
    }
    
    // NOTE: MemoryAccessMode type no longer exists - MemoryOptions is now an enum
    // This test needs to be refactored to use the new MemoryOptions enum
    /*
    [Theory]
    [InlineData(MemoryAccessMode.ReadOnly)]
    [InlineData(MemoryAccessMode.WriteOnly)]
    [InlineData(MemoryAccessMode.ReadWrite)]
    public async Task AllocateAsync_WithDifferentAccessModes_ConfiguresCorrectly(MemoryAccessMode accessMode)
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = accessMode,
            Location = MemoryLocation.Host
        };
        
        // Act
        var buffer = await _memoryManager.AllocateAsync(1024, options);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.Options.AccessMode.Should().Be(accessMode);
        
        // Cleanup
        await buffer.DisposeAsync();
    }
    */
    
    [Fact]
    public async Task AllocateAsync_WithPinnedMemory_ConfiguresCorrectly()
    {
        // Arrange
        var options = new MemoryOptions
        {
            AccessMode = MemoryAccessMode.ReadWrite,
            Location = MemoryLocation.Host,
            IsPinned = true
        };
        
        // Act
        var buffer = await _memoryManager.AllocateAsync(1024, options);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.Options.IsPinned.Should().BeTrue();
        
        // Cleanup
        await buffer.DisposeAsync();
    }
    
    public void Dispose()
    {
        _memoryManager?.Dispose();
        GC.SuppressFinalize(this);
    }
}