// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Comprehensive tests for CPU memory manager implementation.
/// </summary>
public class CpuMemoryManagerTests : IDisposable
{
    private readonly CpuMemoryManager _memoryManager;

    public CpuMemoryManagerTests()
    {
        _memoryManager = new CpuMemoryManager();
    }

    [Fact]
    public async Task AllocateAsyncWithValidSize_CreatesBuffer()
    {
        // Arrange
        const long size = 1024;

        // Act
        var buffer = await _memoryManager.AllocateAsync(size);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(size);
        buffer.Flags.Should().Be(MemoryFlags.None);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(16 * 1024 * 1024)]
    public async Task AllocateAsyncWithVariousSizes_CreatesCorrectBuffer(long size)
    {
        // Act
        var buffer = await _memoryManager.AllocateAsync(size);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(size);
        _memoryManager.TotalAllocatedBytes.Should().BeGreaterOrEqualTo(size);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public async Task AllocateAsyncWithInvalidSize_ThrowsArgumentOutOfRangeException(long size)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _memoryManager.AllocateAsync(size).AsTask());
    }

    [Theory]
    [InlineData(MemoryFlags.None)]
    [InlineData(MemoryFlags.ReadOnly)]
    [InlineData(MemoryFlags.WriteOnly)]
    [InlineData(MemoryFlags.HostVisible)]
    [InlineData(MemoryFlags.Cached)]
    [InlineData(MemoryFlags.Atomic)]
    public async Task AllocateAsyncWithMemoryFlags_SetsCorrectFlags(MemoryFlags flags)
    {
        // Arrange
        const long size = 1024;

        // Act
        var buffer = await _memoryManager.AllocateAsync(size, flags);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Flags.Should().Be(flags);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task AllocateAndCopyAsyncWithValidData_CopiesDataCorrectly()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var readOnlyMemory = new ReadOnlyMemory<int>(sourceData);

        // Act
        var buffer = await _memoryManager.AllocateAndCopyAsync(readOnlyMemory);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sourceData.Length * sizeof(int));

        // Verify data was copied
        var resultData = new int[sourceData.Length];
        await buffer.CopyToHostAsync<int>(resultData);
        resultData.Should().BeEquivalentTo(sourceData);

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task CreateViewWithValidRange_CreatesCorrectView()
    {
        // Arrange
        const long bufferSize = 1024;
        const long viewOffset = 256;
        const long viewLength = 512;

        var buffer = await _memoryManager.AllocateAsync(bufferSize);

        // Act
        var view = _memoryManager.CreateView(buffer, viewOffset, viewLength);

        // Assert
        view.Should().NotBeNull();
        view.SizeInBytes.Should().Be(viewLength);

        // Cleanup
        await buffer.DisposeAsync();
        await view.DisposeAsync();
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, -1)]
    [InlineData(1000, 100)]
    [InlineData(500, 600)]
    public async Task CreateViewWithInvalidRange_ThrowsArgumentOutOfRangeException(long offset, long length)
    {
        // Arrange
        const long bufferSize = 1024;
        var buffer = await _memoryManager.AllocateAsync(bufferSize);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _memoryManager.CreateView(buffer, offset, length));

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task TotalAllocatedBytesTracksAllocationCorrectly()
    {
        // Arrange
        const long size1 = 1024;
        const long size2 = 2048;
        const long size3 = 4096;

        var initialTotal = _memoryManager.TotalAllocatedBytes;

        // Act
        var buffer1 = await _memoryManager.AllocateAsync(size1);
        var buffer2 = await _memoryManager.AllocateAsync(size2);
        var buffer3 = await _memoryManager.AllocateAsync(size3);

        // Assert
        _memoryManager.TotalAllocatedBytes.Should().Be(initialTotal + size1 + size2 + size3);

        // Cleanup and verify deallocation tracking
        await buffer1.DisposeAsync();
        _memoryManager.TotalAllocatedBytes.Should().Be(initialTotal + size2 + size3);

        await buffer2.DisposeAsync();
        _memoryManager.TotalAllocatedBytes.Should().Be(initialTotal + size3);

        await buffer3.DisposeAsync();
        _memoryManager.TotalAllocatedBytes.Should().Be(initialTotal);
    }

    [Fact]
    public async Task MultipleAllocationsDifferentSizes_AllSucceed()
    {
        // Arrange
        var sizes = new long[] { 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
        var buffers = new List<IMemoryBuffer>();

        // Act
        foreach (var size in sizes)
        {
            var buffer = await _memoryManager.AllocateAsync(size);
            buffers.Add(buffer);
        }

        // Assert
        buffers.Should().HaveCount(sizes.Length);
        
        for (int i = 0; i < sizes.Length; i++)
        {
            buffers[i].SizeInBytes.Should().Be(sizes[i]);
        }

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeManagerWithActiveBuffers_DisposesAllBuffers()
    {
        // Arrange
        var buffer1 = await _memoryManager.AllocateAsync(1024);
        var buffer2 = await _memoryManager.AllocateAsync(2048);
        var buffer3 = await _memoryManager.AllocateAsync(4096);

        // Act
        _memoryManager.Dispose();

        // Assert
        // After disposal, accessing buffers should throw
        Assert.Throws<ObjectDisposedException>(() => buffer1.GetMemory());
        Assert.Throws<ObjectDisposedException>(() => buffer2.GetMemory());
        Assert.Throws<ObjectDisposedException>(() => buffer3.GetMemory());
    }

    [Fact]
    public async Task DisposedManagerThrowsObjectDisposedException()
    {
        // Arrange
        _memoryManager.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _memoryManager.AllocateAsync(1024).AsTask());
    }

    [Fact]
    public async Task CreateViewWithInvalidBuffer_ThrowsArgumentException()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);
        await buffer.DisposeAsync();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _memoryManager.CreateView(buffer, 0, 100));
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
    }
}