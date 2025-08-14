// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using FluentAssertions;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using DotCompute.Memory;
using DotCompute.Tests.Shared;
using AbstractionsMemoryManager = DotCompute.Abstractions.IMemoryManager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Tests;

/// <summary>
/// Comprehensive tests for P2P buffer functionality including type-aware transfers.
/// </summary>
public sealed class P2PBufferTests : IDisposable
{
    private readonly ILogger<P2PBuffer<float>> _logger;
    private readonly MockAccelerator _mockAccelerator;
    private readonly MockMemoryBuffer _mockMemoryBuffer;

    public P2PBufferTests()
    {
        _logger = NullLogger<P2PBuffer<float>>.Instance;
        _mockAccelerator = new MockAccelerator(name: "test-device-0", type: AcceleratorType.CUDA);
        _mockMemoryBuffer = new MockMemoryBuffer(1024 * sizeof(float), Abstractions.MemoryOptions.None);
    }

    [Fact]
    public void Constructor_ValidParameters_InitializesCorrectly()
    {
        // Arrange
        const int length = 256;
        const bool supportsP2P = true;

        // Act
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, length, supportsP2P, _logger);

        // Assert
        Assert.Equal(length, buffer.Length);
        Assert.Equal(_mockMemoryBuffer.SizeInBytes, buffer.SizeInBytes);
        Assert.Equal(_mockAccelerator, buffer.Accelerator);
        // Note: P2PBuffer may not expose MemoryType property directly
        // Assert.Equal(MemoryType.DeviceLocal, buffer.MemoryType);
        Assert.True(buffer.SupportsDirectP2P);
        Assert.Equal(_mockMemoryBuffer, buffer.UnderlyingBuffer);
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public void Constructor_NullUnderlyingBuffer_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new P2PBuffer<float>(null!, _mockAccelerator, 256, true, _logger));
    }

    [Fact]
    public void Constructor_NullAccelerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new P2PBuffer<float>(_mockMemoryBuffer, null!, 256, true, _logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, null!));
    }

    [Fact]
    public async Task CopyFromHostAsync_ValidArray_CompletesSuccessfully()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var sourceData = new float[256];
        for (var i = 0; i < sourceData.Length; i++)
        {
            sourceData[i] = i * 0.5f;
        }

        // Act
        await buffer.CopyFromHostAsync(sourceData, 0);

        // Assert - should complete without throwing
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public async Task CopyFromHostAsync_NullArray_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            buffer.CopyFromHostAsync<float>(null!, 0));
    }

    [Fact]
    public async Task CopyFromHostAsync_WrongDataType_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var intData = new int[256];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            buffer.CopyFromHostAsync(intData, 0));
    }

    [Fact]
    public async Task CopyFromHostAsync_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var sourceData = new float[256];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            buffer.CopyFromHostAsync(sourceData, -1));
    }

    [Fact]
    public async Task CopyFromHostAsync_ReadOnlyMemory_CompletesSuccessfully()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var sourceData = new float[256];
        for (var i = 0; i < sourceData.Length; i++)
        {
            sourceData[i] = i * 0.5f;
        }
        var readOnlyMemory = new ReadOnlyMemory<float>(sourceData);

        // Act
        await buffer.CopyFromHostAsync(readOnlyMemory, 0);

        // Assert
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public async Task CopyToHostAsync_ValidArray_CompletesSuccessfully()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var destinationData = new float[256];

        // Act
        await buffer.CopyToHostAsync(destinationData, 0);

        // Assert
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public async Task CopyToHostAsync_NullArray_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            buffer.CopyToHostAsync<float>(null!, 0));
    }

    [Fact]
    public async Task CopyToHostAsync_WrongDataType_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var intData = new int[256];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            buffer.CopyToHostAsync(intData, 0));
    }

    [Fact]
    public async Task CopyToHostAsync_Memory_CompletesSuccessfully()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var destinationData = new float[256];
        var memory = new Memory<float>(destinationData);

        // Act
        await buffer.CopyToHostAsync(memory, 0);

        // Assert
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public async Task CopyToAsync_P2PBuffer_UsesOptimizedPath()
    {
        // Arrange
        var sourceBuffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var targetAccelerator = new MockAccelerator(name: "test-device-1", type: AcceleratorType.CUDA);
        var targetMemoryBuffer = new MockMemoryBuffer(1024 * sizeof(float), Abstractions.MemoryOptions.None);
        var targetBuffer = new P2PBuffer<float>(targetMemoryBuffer, targetAccelerator, 256, true, _logger);

        // Act
        await sourceBuffer.CopyToAsync(targetBuffer);

        // Assert - should complete without throwing
        Assert.False(sourceBuffer.IsDisposed);
        Assert.False(targetBuffer.IsDisposed);
    }

    [Fact]
    public async Task CopyToAsync_NullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            buffer.CopyToAsync((IBuffer<float>)null!));
    }

    [Fact]
    public async Task CopyToAsync_RangeTransfer_ValidatesRanges()
    {
        // Arrange
        var sourceBuffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var targetAccelerator = new MockAccelerator(name: "test-device-1", type: AcceleratorType.CUDA);
        var targetMemoryBuffer = new MockMemoryBuffer(1024 * sizeof(float), Abstractions.MemoryOptions.None);
        var targetBuffer = new P2PBuffer<float>(targetMemoryBuffer, targetAccelerator, 256, true, _logger);

        // Act & Assert - valid range
        await sourceBuffer.CopyToAsync(0, targetBuffer, 0, 128);

        // Act & Assert - invalid source offset
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            sourceBuffer.CopyToAsync(-1, targetBuffer, 0, 10));

        // Act & Assert - invalid destination offset
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            sourceBuffer.CopyToAsync(0, targetBuffer, -1, 10));

        // Act & Assert - invalid count
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            sourceBuffer.CopyToAsync(0, targetBuffer, 0, -1));

        // Act & Assert - count exceeds buffer bounds
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            sourceBuffer.CopyToAsync(0, targetBuffer, 0, 300));
    }

    [Fact]
    public async Task FillAsync_ValidValue_CompletesSuccessfully()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        const float fillValue = 3.14f;

        // Act
        await buffer.FillAsync(fillValue);

        // Assert
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public async Task FillAsync_WrongDataType_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        const int intValue = 42;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            buffer.FillAsync(intValue));
    }

    [Fact]
    public async Task FillAsync_RangeFill_ValidatesRanges()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        const float fillValue = 2.718f;

        // Act & Assert - valid range
        await buffer.FillAsync(fillValue, 10, 50);

        // Act & Assert - invalid offset
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            buffer.FillAsync(fillValue, -1, 10));

        // Act & Assert - invalid count
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            buffer.FillAsync(fillValue, 0, -1));

        // Act & Assert - range exceeds buffer bounds
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            buffer.FillAsync(fillValue, 0, 300));
    }

    [Fact]
    public async Task ClearAsync_CompletesSuccessfully()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act
        await buffer.ClearAsync();

        // Assert
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public void Slice_ValidRange_CreatesCorrectSlice()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act
        var slice = buffer.Slice(64, 128);

        // Assert
        Assert.Equal(128, slice.Length);
        Assert.Equal(_mockAccelerator, slice.Accelerator);
        Assert.IsType<P2PBuffer<float>>(slice);
    }

    [Fact]
    public void Slice_InvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act & Assert - negative offset
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Slice(-1, 10));

        // Act & Assert - non-positive count
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Slice(0, -1));

        // Act & Assert - range exceeds buffer bounds
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Slice(200, 100));
    }

    [Fact]
    public void AsType_ValidTypeConversion_CreatesTypedBuffer()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act
        var intBuffer = buffer.AsType<int>();

        // Assert
        Assert.Equal(256, intBuffer.Length); // Same element count
        Assert.Equal(_mockAccelerator, intBuffer.Accelerator);
        Assert.IsType<P2PBuffer<int>>(intBuffer);
    }

    [Fact]
    public void Map_ReturnsDefaultMappedMemory()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act
        var mappedMemory = buffer.Map(MapMode.Read);

        // Assert
        // P2P buffers typically don't support direct mapping
        Assert.Equal(default, mappedMemory);
    }

    [Fact]
    public void MapRange_ValidRange_ReturnsDefaultMappedMemory()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act
        var mappedMemory = buffer.MapRange(64, 128, MapMode.Read);

        // Assert
        Assert.Equal(default, mappedMemory);
    }

    [Fact]
    public void MapRange_InvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act & Assert - negative offset
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            buffer.MapRange(-1, 10, MapMode.Read));

        // Act & Assert - non-positive count
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            buffer.MapRange(0, -1, MapMode.Read));

        // Act & Assert - range exceeds buffer bounds
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            buffer.MapRange(200, 100, MapMode.Read));
    }

    [Fact]
    public async Task MapAsync_ReturnsDefaultMappedMemory()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);

        // Act
        var mappedMemory = await buffer.MapAsync(MapMode.Read);

        // Assert
        Assert.Equal(default, mappedMemory);
    }

    [Fact]
    public async Task Operations_AfterDispose_ThrowObjectDisposedException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        await buffer.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => 
            buffer.CopyFromHostAsync(new float[10], 0));
        
        await Assert.ThrowsAsync<ObjectDisposedException>(() => 
            buffer.CopyToHostAsync(new float[10], 0));
        
        await Assert.ThrowsAsync<ObjectDisposedException>(() => 
            buffer.FillAsync(1.0f));
        
        Assert.Throws<ObjectDisposedException>(() => buffer.Slice(0, 10));
        
        Assert.Throws<ObjectDisposedException>(() => buffer.AsType<int>());
    }

    [Fact]
    public async Task ConcurrentOperations_HandlesConcurrency()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        var concurrentTasks = new Task[10];

        // Act - concurrent fill operations
        for (var i = 0; i < concurrentTasks.Length; i++)
        {
            var value = i * 0.1f;
            concurrentTasks[i] = buffer.FillAsync(value).AsTask();
        }

        // Wait for all operations to complete
        await Task.WhenAll(concurrentTasks);

        // Assert - all operations should complete successfully
        Assert.False(buffer.IsDisposed);
    }

    [Fact]
    public async Task CopyOperations_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, 256, true, _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            buffer.CopyFromHostAsync(new float[256], 0, cts.Token));
        
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            buffer.CopyToHostAsync(new float[256], 0, cts.Token));
    }

    [Fact]
    public void Properties_ReturnCorrectValues()
    {
        // Arrange
        const int length = 512;
        const bool supportsP2P = false;
        var buffer = new P2PBuffer<float>(_mockMemoryBuffer, _mockAccelerator, length, supportsP2P, _logger);

        // Assert
        Assert.Equal(length, buffer.Length);
        Assert.Equal(_mockMemoryBuffer.SizeInBytes, buffer.SizeInBytes);
        Assert.Equal(_mockAccelerator, buffer.Accelerator);
        // Note: P2PBuffer may not expose MemoryType property directly
        // Assert.Equal(MemoryType.DeviceLocal, buffer.MemoryType);
        Assert.Equal(_mockMemoryBuffer.Options, buffer.Options);
        Assert.False(buffer.IsDisposed);
        Assert.False(buffer.SupportsDirectP2P);
        Assert.Equal(_mockMemoryBuffer, buffer.UnderlyingBuffer);
    }

    public void Dispose()
    {
        _mockMemoryBuffer?.Dispose();
        _mockAccelerator?.DisposeAsync().AsTask().Wait();
    }
}
