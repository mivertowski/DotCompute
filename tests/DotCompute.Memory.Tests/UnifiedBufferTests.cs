// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Buffers;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using DotCompute.Abstractions;
using DotCompute.Memory;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for UnifiedBuffer functionality including lazy transfer optimization
/// and memory state management.
/// </summary>
public class UnifiedBufferTests
{
    private readonly IMemoryManager _memoryManager;
    private readonly MemoryPool<float> _pool;
    
    public UnifiedBufferTests()
    {
        _memoryManager = Substitute.For<IMemoryManager>();
        _pool = new MemoryPool<float>(_memoryManager);
        
        // Setup default behavior
        _memoryManager.Allocate(Arg.Any<long>())
            .Returns(x => new DeviceMemory(new IntPtr(12345), (long)x[0]));
        
        _memoryManager.AllocatePinnedHost<float>(Arg.Any<int>())
            .Returns(x => new TestMemoryOwner<float>(new float[(int)x[0]]));
    }
    
    [Fact]
    public async Task Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        
        // Assert
        buffer.Length.Should().Be(1024);
        buffer.SizeInBytes.Should().Be(1024 * sizeof(float));
        buffer.State.Should().Be(MemoryState.Uninitialized);
        buffer.HasHostMemory.Should().BeFalse();
        buffer.HasDeviceMemory.Should().BeFalse();
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidLength_ThrowsArgumentOutOfRangeException(int length)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new UnifiedBuffer<float>(_memoryManager, _pool, length));
    }
    
    [Fact]
    public void Constructor_WithNullMemoryManager_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedBuffer<float>(null!, _pool, 1024));
    }
    
    [Fact]
    public void Constructor_WithNullPool_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedBuffer<float>(_memoryManager, null!, 1024));
    }
    
    [Fact]
    public async Task AllocateHostMemoryAsync_FirstCall_AllocatesHostMemory()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        
        // Act
        await buffer.AllocateHostMemoryAsync();
        
        // Assert
        buffer.HasHostMemory.Should().BeTrue();
        buffer.State.Should().Be(MemoryState.HostOnly);
        _memoryManager.Received(1).AllocatePinnedHost<float>(1024);
    }
    
    [Fact]
    public async Task AllocateDeviceMemoryAsync_FirstCall_AllocatesDeviceMemory()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        
        // Act
        await buffer.AllocateDeviceMemoryAsync();
        
        // Assert
        buffer.HasDeviceMemory.Should().BeTrue();
        buffer.State.Should().Be(MemoryState.DeviceOnly);
        _memoryManager.Received(1).Allocate(1024 * sizeof(float));
    }
    
    [Fact]
    public async Task AllocateBothMemories_StateTransition_UpdatesStateCorrectly()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        
        // Act
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Assert
        buffer.State.Should().Be(MemoryState.HostAndDevice);
        buffer.HasHostMemory.Should().BeTrue();
        buffer.HasDeviceMemory.Should().BeTrue();
    }
    
    [Fact]
    public async Task GetHostSpanAsync_WithoutHostMemory_AllocatesAndReturnsSpan()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        
        // Act
        var span = await buffer.GetHostSpanAsync();
        
        // Assert
        span.Length.Should().Be(1024);
        buffer.HasHostMemory.Should().BeTrue();
        _memoryManager.Received(1).AllocatePinnedHost<float>(1024);
    }
    
    [Fact]
    public async Task GetDeviceMemoryAsync_WithoutDeviceMemory_AllocatesAndReturnsHandle()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        
        // Act
        var deviceMemory = await buffer.GetDeviceMemoryAsync();
        
        // Assert
        deviceMemory.IsValid.Should().BeTrue();
        buffer.HasDeviceMemory.Should().BeTrue();
        _memoryManager.Received(1).Allocate(1024 * sizeof(float));
    }
    
    [Fact]
    public async Task CopyFromAsync_WithValidData_CopiesSuccessfully()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        var sourceData = new float[1024];
        for (int i = 0; i < sourceData.Length; i++)
        {
            sourceData[i] = i * 0.5f;
        }
        
        // Act
        await buffer.CopyFromAsync(sourceData);
        
        // Assert
        var hostSpan = await buffer.GetHostSpanAsync();
        hostSpan.ToArray().Should().BeEquivalentTo(sourceData);
        buffer.HasHostMemory.Should().BeTrue();
    }
    
    [Fact]
    public async Task CopyFromAsync_WithMismatchedSize_ThrowsArgumentException()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        var sourceData = new float[512]; // Different size
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await buffer.CopyFromAsync(sourceData));
    }
    
    [Fact]
    public async Task CopyToAsync_WithValidDestination_CopiesSuccessfully()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        var sourceData = new float[1024];
        for (int i = 0; i < sourceData.Length; i++)
        {
            sourceData[i] = i * 0.5f;
        }
        
        await buffer.CopyFromAsync(sourceData);
        
        var destination = new float[1024];
        
        // Act
        await buffer.CopyToAsync(destination);
        
        // Assert
        destination.Should().BeEquivalentTo(sourceData);
    }
    
    [Fact]
    public async Task CopyToAsync_WithMismatchedSize_ThrowsArgumentException()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        var destination = new float[512]; // Different size
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await buffer.CopyToAsync(destination));
    }
    
    [Fact]
    public async Task ClearAsync_ClearsBufferData()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        var sourceData = new float[1024];
        for (int i = 0; i < sourceData.Length; i++)
        {
            sourceData[i] = i * 0.5f;
        }
        
        await buffer.CopyFromAsync(sourceData);
        
        // Act
        await buffer.ClearAsync();
        
        // Assert
        var hostSpan = await buffer.GetHostSpanAsync();
        hostSpan.ToArray().Should().AllBeEquivalentTo(0.0f);
    }
    
    [Fact]
    public async Task MarkHostDirty_ThenGetDeviceMemory_TriggersSynchronization()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Act
        buffer.MarkHostDirty();
        await buffer.GetDeviceMemoryAsync();
        
        // Assert
        _memoryManager.Received(1).CopyToDevice(Arg.Any<ReadOnlySpan<float>>(), Arg.Any<DeviceMemory>());
    }
    
    [Fact]
    public async Task MarkDeviceDirty_ThenGetHostSpan_TriggersSynchronization()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Act
        buffer.MarkDeviceDirty();
        await buffer.GetHostSpanAsync();
        
        // Assert
        _memoryManager.Received(1).CopyToHost(Arg.Any<DeviceMemory>(), Arg.Any<Span<float>>());
    }
    
    [Fact]
    public async Task SynchronizeAsync_HostToDevice_CopiesHostToDevice()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        buffer.MarkHostDirty();
        
        // Act
        await buffer.SynchronizeAsync(SyncDirection.HostToDevice);
        
        // Assert
        _memoryManager.Received(1).CopyToDevice(Arg.Any<ReadOnlySpan<float>>(), Arg.Any<DeviceMemory>());
    }
    
    [Fact]
    public async Task SynchronizeAsync_DeviceToHost_CopiesDeviceToHost()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        buffer.MarkDeviceDirty();
        
        // Act
        await buffer.SynchronizeAsync(SyncDirection.DeviceToHost);
        
        // Assert
        _memoryManager.Received(1).CopyToHost(Arg.Any<DeviceMemory>(), Arg.Any<Span<float>>());
    }
    
    [Fact]
    public async Task GetPerformanceStats_ReturnsValidStats()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Act
        var stats = buffer.GetPerformanceStats();
        
        // Assert
        stats.AccessCount.Should().BeGreaterThan(0);
        stats.SizeInBytes.Should().Be(1024 * sizeof(float));
        stats.State.Should().Be(MemoryState.HostAndDevice);
        stats.HasHostMemory.Should().BeTrue();
        stats.HasDeviceMemory.Should().BeTrue();
    }
    
    [Fact]
    public async Task Dispose_ReleasesAllResources()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Act
        buffer.Dispose();
        
        // Assert
        _memoryManager.Received(1).Free(Arg.Any<DeviceMemory>());
        
        // Subsequent operations should throw
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await buffer.GetHostSpanAsync());
    }
    
    [Fact]
    public async Task DisposeAsync_ReleasesAllResources()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Act
        await buffer.DisposeAsync();
        
        // Assert
        _memoryManager.Received(1).Free(Arg.Any<DeviceMemory>());
    }
    
    [Fact]
    public async Task LazyTransferOptimization_OnlyTransfersWhenNecessary()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Act - Access host memory multiple times
        await buffer.GetHostSpanAsync();
        await buffer.GetHostSpanAsync();
        await buffer.GetHostSpanAsync();
        
        // Assert - No unnecessary transfers
        _memoryManager.DidNotReceive().CopyToHost(Arg.Any<DeviceMemory>(), Arg.Any<Span<float>>());
        _memoryManager.DidNotReceive().CopyToDevice(Arg.Any<ReadOnlySpan<float>>(), Arg.Any<DeviceMemory>());
    }
    
    [Fact]
    public async Task ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<float>(_memoryManager, _pool, 1024);
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Act - Concurrent access from multiple threads
        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                await buffer.GetHostSpanAsync();
                await buffer.GetDeviceMemoryAsync();
                buffer.MarkHostDirty();
                buffer.MarkDeviceDirty();
            });
        }
        
        // Assert - All tasks complete without exceptions
        await Task.WhenAll(tasks);
        
        // Verify final state is consistent
        var stats = buffer.GetPerformanceStats();
        stats.AccessCount.Should().BeGreaterThan(0);
        stats.State.Should().Be(MemoryState.HostAndDevice);
    }
    
    private class TestMemoryOwner<T> : IMemoryOwner<T>
    {
        private readonly T[] _array;
        
        public TestMemoryOwner(T[] array)
        {
            _array = array;
        }
        
        public Memory<T> Memory => new(_array);
        
        public void Dispose()
        {
            // No-op for test
        }
    }
}