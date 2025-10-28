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
    /// <summary>
    /// Initializes a new instance of the CpuMemoryManagerTests class.
    /// </summary>


    public CpuMemoryManagerTests()
    {
        using var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<CpuMemoryManager>();
        _memoryManager = new CpuMemoryManager(_logger);
    }
    /// <summary>
    /// Performs constructor_ with default policy_ initializes successfully.
    /// </summary>


    [Fact]
    public void Constructor_WithDefaultPolicy_InitializesSuccessfully()
    {
        // Arrange & Act - memory manager created in constructor

        // Assert
        _ = _memoryManager.Should().NotBeNull();
        _ = _memoryManager.Topology.Should().NotBeNull();
        _ = _memoryManager.IsDisposed.Should().BeFalse();
    }
    /// <summary>
    /// Performs constructor_ with custom policy_ initializes with policy.
    /// </summary>


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
        _ = memoryManager.Should().NotBeNull();
        _ = memoryManager.Topology.Should().NotBeNull();
    }
    /// <summary>
    /// Performs topology_ returns valid topology.
    /// </summary>


    [Fact]
    public void Topology_ReturnsValidTopology()
    {
        // Act
        var topology = _memoryManager.Topology;

        // Assert
        _ = topology.Should().NotBeNull();
        _ = topology.NodeCount.Should().BeGreaterThan(0);
        _ = topology.TotalMemoryBytes.Should().BeGreaterThan(0);
    }
    /// <summary>
    /// Gets allocate async_ with various sizes_ allocates successfully.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <returns>The result of the operation.</returns>


    [Theory]
    [InlineData(1024)]      // 1KB
    [InlineData(64 * 1024)] // 64KB
    [InlineData(1024 * 1024)] // 1MB
    public async Task AllocateAsync_WithVariousSizes_AllocatesSuccessfully(long sizeInBytes)
    {
        // Arrange
        var options = MemoryOptions.HostVisible;

        // Act

        var buffer = await _memoryManager.AllocateAsync(sizeInBytes, options);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.SizeInBytes.Should().Be(sizeInBytes);
        _ = buffer.IsDisposed.Should().BeFalse();
        _ = buffer.Options.Should().Be(options);

        // Cleanup

        await buffer.DisposeAsync();
    }
    /// <summary>
    /// Gets allocate async_ with numa policy_ considers numa placement.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task AllocateAsync_WithNumaPolicy_ConsidersNumaPlacement()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;


        var numaPolicy = new NumaMemoryPolicy
        {
            PreferLocalNode = true,
            AllowRemoteAccess = false,
            InterleavingEnabled = false
        };

        // Act

        var buffer = await _memoryManager.AllocateAsync(1024, options, numaPolicy);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.SizeInBytes.Should().Be(1024);

        // Cleanup

        await buffer.DisposeAsync();
    }
    /// <summary>
    /// Gets allocate async_ multiple allocations_ tracks memory correctly.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task AllocateAsync_MultipleAllocations_TracksMemoryCorrectly()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;


        var initialAllocated = _memoryManager.CurrentAllocatedMemory;

        // Act

        var buffer1 = await _memoryManager.AllocateAsync(1024, options);
        var buffer2 = await _memoryManager.AllocateAsync(2048, options);


        var afterAllocation = _memoryManager.CurrentAllocatedMemory;

        // Assert
        _ = afterAllocation.Should().BeGreaterThan(initialAllocated);
        _ = (afterAllocation - initialAllocated).Should().BeGreaterThanOrEqualTo(3072); // At least 1024 + 2048

        // Cleanup

        await buffer1.DisposeAsync();
        await buffer2.DisposeAsync();
    }
    /// <summary>
    /// Creates a new view_ with valid buffer_ creates view successfully.
    /// </summary>
    /// <returns>The created view_ with valid buffer_ creates view successfully.</returns>


    [Fact]
    public async Task CreateView_WithValidBuffer_CreatesViewSuccessfully()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;


        var buffer = await _memoryManager.AllocateAsync(1024, options);

        // Act

        var view = _memoryManager.CreateView(buffer, 256, 512);

        // Assert
        _ = view.Should().NotBeNull();
        _ = view.SizeInBytes.Should().Be(512);
        _ = view.IsDisposed.Should().BeFalse();

        // Cleanup

        await view.DisposeAsync();
        await buffer.DisposeAsync();
    }
    /// <summary>
    /// Creates a new view_ with invalid buffer_ throws argument exception.
    /// </summary>


    [Fact]
    public void CreateView_WithInvalidBuffer_ThrowsArgumentException()
    {
        // Arrange
        var mockBuffer = new Mock<IUnifiedMemoryBuffer>();
        _ = mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);

        // Act & Assert
        _ = _memoryManager.Invoking(mm => mm.CreateView(mockBuffer.Object, 0, 512))
            .Should().Throw<ArgumentException>();
    }
    /// <summary>
    /// Performs max allocation size_ returns max value.
    /// </summary>


    [Fact]
    public void MaxAllocationSize_ReturnsMaxValue()
    {
        // Act
        var maxSize = _memoryManager.MaxAllocationSize;

        // Assert
        _ = maxSize.Should().Be(long.MaxValue);
    }
    /// <summary>
    /// Performs total available memory_ returns positive value.
    /// </summary>


    [Fact]
    public void TotalAvailableMemory_ReturnsPositiveValue()
    {
        // Act
        var availableMemory = _memoryManager.TotalAvailableMemory;

        // Assert
        _ = availableMemory.Should().BeGreaterThan(0);
    }
    /// <summary>
    /// Performs accelerator_ returns valid accelerator.
    /// </summary>


    [Fact]
    public void Accelerator_ReturnsValidAccelerator()
    {
        // Act
        var accelerator = _memoryManager.Accelerator;

        // Assert
        _ = accelerator.Should().NotBeNull();
        _ = accelerator.Type.Should().Be(AcceleratorType.CPU);
    }
    /// <summary>
    /// Performs statistics_ returns valid statistics.
    /// </summary>


    [Fact]
    public void Statistics_ReturnsValidStatistics()
    {
        // Act
        var statistics = _memoryManager.Statistics;

        // Assert
        _ = statistics.Should().NotBeNull();
        _ = statistics.TotalAllocated.Should().BeGreaterThanOrEqualTo(0);
        _ = statistics.TotalFreed.Should().BeGreaterThanOrEqualTo(0);
        _ = statistics.ActiveBuffers.Should().BeGreaterThanOrEqualTo(0);
        _ = statistics.PeakMemoryUsage.Should().BeGreaterThanOrEqualTo(0);
    }
    /// <summary>
    /// Gets free async_ with valid buffer_ updates statistics.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task FreeAsync_WithValidBuffer_UpdatesStatistics()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;


        var buffer = await _memoryManager.AllocateAsync(1024, options) as CpuMemoryBuffer;
        var beforeFree = _memoryManager.Statistics;

        // Act

        await _memoryManager.FreeAsync(buffer!, CancellationToken.None);

        // Assert

        var afterFree = _memoryManager.Statistics;
        _ = afterFree.TotalFreed.Should().BeGreaterThan(beforeFree.TotalFreed);
        _ = afterFree.ActiveBuffers.Should().BeLessThan(beforeFree.ActiveBuffers);
    }
    /// <summary>
    /// Gets optimize async_ triggers garbage collection.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    public async Task OptimizeAsync_TriggersGarbageCollection()
    {
        // Arrange
        var beforeGC = GC.CollectionCount(0);

        // Act

        await _memoryManager.OptimizeAsync(CancellationToken.None);

        // Assert

        var afterGC = GC.CollectionCount(0);
        _ = afterGC.Should().BeGreaterThan(beforeGC);
    }
    /// <summary>
    /// Performs clear_ resets statistics.
    /// </summary>


    [Fact]
    public void Clear_ResetsStatistics()
    {
        // Act
        _memoryManager.Clear();

        // Assert

        var statistics = _memoryManager.Statistics;
        _ = statistics.TotalAllocated.Should().Be(0);
        _ = statistics.ActiveBuffers.Should().Be(0);
        _ = statistics.TotalFreed.Should().Be(0);
    }
    /// <summary>
    /// Gets allocate async_ with zero size_ throws argument exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task AllocateAsync_WithZeroSize_ThrowsArgumentException()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;

        // Act & Assert

        Func<Task> act = async () => await _memoryManager.AllocateAsync(0, options);
        _ = await act.Should().ThrowExactlyAsync<ArgumentOutOfRangeException>();
    }
    /// <summary>
    /// Gets allocate async_ with negative size_ throws argument exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task AllocateAsync_WithNegativeSize_ThrowsArgumentException()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;

        // Act & Assert

        Func<Task> act = async () => await _memoryManager.AllocateAsync(-1024, options);
        _ = await act.Should().ThrowExactlyAsync<ArgumentOutOfRangeException>();
    }
    /// <summary>
    /// Creates a new view_ with invalid range_ throws argument out of range exception.
    /// </summary>
    /// <returns>The created view_ with invalid range_ throws argument out of range exception.</returns>


    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task CreateView_WithInvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;


        var buffer = await _memoryManager.AllocateAsync(1024, options);

        // Act & Assert
        _ = _memoryManager.Invoking(mm => mm.CreateView(buffer, 512, 1024))
            .Should().Throw<ArgumentOutOfRangeException>();

        // Cleanup

        await buffer.DisposeAsync();
    }
    /// <summary>
    /// Gets allocate async_ concurrent allocations_ handles parallel requests.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", TestCategories.Concurrency)]
    public async Task AllocateAsync_ConcurrentAllocations_HandlesParallelRequests()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;


        var allocationTasks = Enumerable.Range(0, 10)
            .Select(_ => _memoryManager.AllocateAsync(1024, options).AsTask())
            .ToArray();

        // Act

        var buffers = await Task.WhenAll(allocationTasks);

        // Assert
        _ = buffers.Should().HaveCount(10);
        _ = buffers.Should().OnlyContain(b => b != null && b.SizeInBytes == 1024);

        // Verify all buffers are different instances
        _ = buffers.Distinct().Should().HaveCount(10);

        // Cleanup

        await Task.WhenAll(buffers.Select(b => b.DisposeAsync().AsTask()));
    }
    /// <summary>
    /// Gets allocate async_ performance benchmark_ meets timing requirements.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", TestCategories.Performance)]
    public async Task AllocateAsync_PerformanceBenchmark_MeetsTimingRequirements()
    {
        // Arrange
        var options = MemoryOptions.HostVisible;


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
        _ = stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
        _ = buffers.Should().HaveCount(100);

        // Cleanup

        await Task.WhenAll(buffers.Select(b => b.DisposeAsync().AsTask()));
    }
    /// <summary>
    /// Gets allocate async_ with pinned memory_ configures correctly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        var options = MemoryOptions.HostVisible;
        
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
        var options = MemoryOptions.Pinned | MemoryOptions.HostVisible;

        // Act

        var buffer = await _memoryManager.AllocateAsync(1024, options);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.Options.IsPinned().Should().BeTrue();

        // Cleanup

        await buffer.DisposeAsync();
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>


    public void Dispose()
    {
        _memoryManager?.Dispose();
        GC.SuppressFinalize(this);
    }
}