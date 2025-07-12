// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Comprehensive tests for CPU memory buffer implementation.
/// </summary>
public class CpuMemoryBufferTests : IDisposable
{
    private readonly CpuMemoryManager _memoryManager;

    public CpuMemoryBufferTests()
    {
        _memoryManager = new CpuMemoryManager();
    }

    [Fact]
    public async Task GetMemory_WithValidBuffer_ReturnsCorrectMemory()
    {
        // Arrange
        const long size = 1024;
        var buffer = await _memoryManager.AllocateAsync(size);

        // Act
        var memory = buffer.GetMemory();

        // Assert
        memory.Should().NotBeNull();
        memory.Length.Should().Be((int)size);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task CopyFromHostAsync_WithValidData_CopiesCorrectly()
    {
        // Arrange
        var sourceData = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var buffer = await _memoryManager.AllocateAsync(sourceData.Length);

        // Act
        await buffer.CopyFromHostAsync<byte>(sourceData);

        // Assert
        var memory = buffer.GetMemory();
        var span = memory.Span;
        
        for (int i = 0; i < sourceData.Length; i++)
        {
            span[i].Should().Be(sourceData[i]);
        }

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task CopyToHostAsync_WithValidBuffer_CopiesCorrectly()
    {
        // Arrange
        var sourceData = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var buffer = await _memoryManager.AllocateAsync(sourceData.Length);
        await buffer.CopyFromHostAsync<byte>(sourceData);

        // Act
        var destinationData = new byte[sourceData.Length];
        await buffer.CopyToHostAsync<byte>(destinationData);

        // Assert
        destinationData.Should().BeEquivalentTo(sourceData);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Theory]
    [InlineData(typeof(byte), 1)]
    [InlineData(typeof(short), 2)]
    [InlineData(typeof(int), 4)]
    [InlineData(typeof(long), 8)]
    [InlineData(typeof(float), 4)]
    [InlineData(typeof(double), 8)]
    public async Task CopyOperations_WithDifferentTypes_WorkCorrectly(Type elementType, int elementSize)
    {
        // Arrange
        const int elementCount = 100;
        var bufferSize = elementCount * elementSize;
        var buffer = await _memoryManager.AllocateAsync(bufferSize);

        // Test different primitive types
        if (elementType == typeof(int))
        {
            var sourceData = Enumerable.Range(0, elementCount).ToArray();
            await buffer.CopyFromHostAsync<int>(sourceData);
            
            var destinationData = new int[elementCount];
            await buffer.CopyToHostAsync<int>(destinationData);
            
            destinationData.Should().BeEquivalentTo(sourceData);
        }
        else if (elementType == typeof(float))
        {
            var sourceData = Enumerable.Range(0, elementCount).Select(i => (float)i * 0.5f).ToArray();
            await buffer.CopyFromHostAsync<float>(sourceData);
            
            var destinationData = new float[elementCount];
            await buffer.CopyToHostAsync<float>(destinationData);
            
            destinationData.Should().BeEquivalentTo(sourceData);
        }
        else if (elementType == typeof(double))
        {
            var sourceData = Enumerable.Range(0, elementCount).Select(i => (double)i * 0.33).ToArray();
            await buffer.CopyFromHostAsync<double>(sourceData);
            
            var destinationData = new double[elementCount];
            await buffer.CopyToHostAsync<double>(destinationData);
            
            destinationData.Should().BeEquivalentTo(sourceData);
        }

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task CopyFromHostAsync_WithOffset_CopiesAtCorrectPosition()
    {
        // Arrange
        const int bufferSize = 1024;
        const int offset = 256;
        var sourceData = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var buffer = await _memoryManager.AllocateAsync(bufferSize);

        // Act
        await buffer.CopyFromHostAsync<byte>(sourceData, offset);

        // Assert
        var memory = buffer.GetMemory();
        var span = memory.Span;

        // Check that data before offset is still zero
        for (int i = 0; i < offset; i++)
        {
            span[i].Should().Be(0);
        }

        // Check that data at offset matches source
        for (int i = 0; i < sourceData.Length; i++)
        {
            span[offset + i].Should().Be(sourceData[i]);
        }

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task CopyToHostAsync_WithOffset_CopiesFromCorrectPosition()
    {
        // Arrange
        const int bufferSize = 1024;
        const int offset = 256;
        var sourceData = Enumerable.Range(0, bufferSize).Select(i => (byte)i).ToArray();
        var buffer = await _memoryManager.AllocateAsync(bufferSize);
        await buffer.CopyFromHostAsync<byte>(sourceData);

        // Act
        var destinationData = new byte[256];
        await buffer.CopyToHostAsync<byte>(destinationData, offset);

        // Assert
        var expectedData = sourceData.Skip(offset).Take(256).ToArray();
        destinationData.Should().BeEquivalentTo(expectedData);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task CopyFromHostAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const int bufferSize = 1024;
        var sourceData = new byte[512];
        var buffer = await _memoryManager.AllocateAsync(bufferSize);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            buffer.CopyFromHostAsync<byte>(sourceData, bufferSize));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            buffer.CopyFromHostAsync<byte>(sourceData, -1));

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task CopyToHostAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const int bufferSize = 1024;
        var destinationData = new byte[512];
        var buffer = await _memoryManager.AllocateAsync(bufferSize);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            buffer.CopyToHostAsync<byte>(destinationData, bufferSize));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            buffer.CopyToHostAsync<byte>(destinationData, -1));

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task BufferView_WithValidRange_CreatesCorrectView()
    {
        // Arrange
        const int bufferSize = 1024;
        const int viewOffset = 256;
        const int viewLength = 512;
        var sourceData = Enumerable.Range(0, bufferSize).Select(i => (byte)i).ToArray();
        
        var buffer = await _memoryManager.AllocateAsync(bufferSize);
        await buffer.CopyFromHostAsync<byte>(sourceData);

        // Act
        var view = _memoryManager.CreateView(buffer, viewOffset, viewLength);

        // Assert
        view.SizeInBytes.Should().Be(viewLength);

        // Verify view shows correct data
        var viewData = new byte[viewLength];
        await view.CopyToHostAsync<byte>(viewData);
        
        var expectedData = sourceData.Skip(viewOffset).Take(viewLength).ToArray();
        viewData.Should().BeEquivalentTo(expectedData);

        // Cleanup
        await buffer.DisposeAsync();
        await view.DisposeAsync();
    }

    [Fact]
    public async Task BufferView_ModifyingView_AffectsOriginalBuffer()
    {
        // Arrange
        const int bufferSize = 1024;
        const int viewOffset = 256;
        const int viewLength = 512;
        var originalData = Enumerable.Range(0, bufferSize).Select(i => (byte)i).ToArray();
        var newData = Enumerable.Range(0, viewLength).Select(i => (byte)(255 - i)).ToArray();
        
        var buffer = await _memoryManager.AllocateAsync(bufferSize);
        await buffer.CopyFromHostAsync<byte>(originalData);

        var view = _memoryManager.CreateView(buffer, viewOffset, viewLength);

        // Act
        await view.CopyFromHostAsync<byte>(newData);

        // Assert
        var resultData = new byte[bufferSize];
        await buffer.CopyToHostAsync<byte>(resultData);

        // Data before view should be unchanged
        for (int i = 0; i < viewOffset; i++)
        {
            resultData[i].Should().Be(originalData[i]);
        }

        // Data in view range should be modified
        for (int i = 0; i < viewLength; i++)
        {
            resultData[viewOffset + i].Should().Be(newData[i]);
        }

        // Data after view should be unchanged
        for (int i = viewOffset + viewLength; i < bufferSize; i++)
        {
            resultData[i].Should().Be(originalData[i]);
        }

        // Cleanup
        await buffer.DisposeAsync();
        await view.DisposeAsync();
    }

    [Fact]
    public async Task DisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);
        await buffer.DisposeAsync();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => buffer.GetMemory());
        
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            buffer.CopyFromHostAsync<byte>(new byte[10]));
            
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            buffer.CopyToHostAsync<byte>(new byte[10]));
    }

    [Fact]
    public async Task ReadOnlyBuffer_ThrowsInvalidOperationException()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024, MemoryFlags.ReadOnly);
        var sourceData = new byte[100];

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            buffer.CopyFromHostAsync<byte>(sourceData));

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task WriteOnlyBuffer_ThrowsInvalidOperationException()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024, MemoryFlags.WriteOnly);
        var destinationData = new byte[100];

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            buffer.CopyToHostAsync<byte>(destinationData));

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task LargeBuffer_AboveArrayPoolLimit_ThrowsNotSupportedException()
    {
        // Arrange
        var largeSize = (long)int.MaxValue + 1;

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _memoryManager.AllocateAsync(largeSize).AsTask());
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
    }
}