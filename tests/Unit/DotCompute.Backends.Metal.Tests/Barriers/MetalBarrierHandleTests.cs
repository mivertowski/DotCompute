// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.Metal.Barriers;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Barriers;

/// <summary>
/// Unit tests for Metal barrier handle implementation.
/// </summary>
public sealed class MetalBarrierHandleTests
{
    [Fact]
    public void Constructor_CreatesValidHandle()
    {
        // Arrange & Act
        using var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 256,
            name: "test_barrier",
            fenceFlags: MetalMemoryFenceFlags.DeviceAndThreadgroup);

        // Assert
        Assert.Equal(1, handle.BarrierId);
        Assert.Equal(BarrierScope.ThreadBlock, handle.Scope);
        Assert.Equal(256, handle.Capacity);
        Assert.Equal("test_barrier", handle.Name);
    }

    [Fact]
    public void Constructor_NullName_Works()
    {
        // Arrange & Act
        using var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.Warp,
            capacity: 32,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.None);

        // Assert
        Assert.Null(handle.Name);
    }

    [Fact]
    public void Sync_ActivatesBarrier()
    {
        // Arrange
        using var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 256,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.Threadgroup);

        // Act
        handle.Sync();

        // Assert
        Assert.Equal(256, handle.ThreadsWaiting); // All threads waiting
    }

    [Fact]
    public void Reset_DeactivatesBarrier()
    {
        // Arrange
        using var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 256,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.Threadgroup);

        handle.Sync();
        Assert.Equal(256, handle.ThreadsWaiting);

        // Act
        handle.Reset();

        // Assert
        Assert.Equal(0, handle.ThreadsWaiting);
    }

    [Theory]
    [InlineData(MetalMemoryFenceFlags.None, "simdgroup_barrier(mem_flags::mem_none);")]
    [InlineData(MetalMemoryFenceFlags.Device, "threadgroup_barrier(mem_flags::mem_device);")]
    [InlineData(MetalMemoryFenceFlags.Threadgroup, "threadgroup_barrier(mem_flags::mem_threadgroup);")]
    [InlineData(MetalMemoryFenceFlags.Texture, "threadgroup_barrier(mem_flags::mem_texture);")]
    [InlineData(MetalMemoryFenceFlags.DeviceAndThreadgroup, "threadgroup_barrier(mem_flags::mem_device_and_threadgroup);")]
    public void GenerateMslBarrierCode_WarpScope_GeneratesCorrectCode(MetalMemoryFenceFlags flags, string expected)
    {
        // Arrange
        using var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.Warp,
            capacity: 32,
            name: null,
            fenceFlags: flags);

        // Act
        var code = handle.GenerateMslBarrierCode();

        // Assert
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(MetalMemoryFenceFlags.None, "threadgroup_barrier(mem_flags::mem_none);")]
    [InlineData(MetalMemoryFenceFlags.Device, "threadgroup_barrier(mem_flags::mem_device);")]
    [InlineData(MetalMemoryFenceFlags.Threadgroup, "threadgroup_barrier(mem_flags::mem_threadgroup);")]
    [InlineData(MetalMemoryFenceFlags.DeviceAndThreadgroup, "threadgroup_barrier(mem_flags::mem_device_and_threadgroup);")]
    public void GenerateMslBarrierCode_ThreadBlockScope_GeneratesCorrectCode(MetalMemoryFenceFlags flags, string expected)
    {
        // Arrange
        using var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 256,
            name: null,
            fenceFlags: flags);

        // Act
        var code = handle.GenerateMslBarrierCode();

        // Assert
        Assert.Equal(expected, code);
    }

    [Fact]
    public void ThreadsWaiting_BeforeSync_ReturnsZero()
    {
        // Arrange
        using var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 512,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.Device);

        // Act & Assert
        Assert.Equal(0, handle.ThreadsWaiting);
    }

    [Fact]
    public void ThreadsWaiting_AfterSync_ReturnsCapacity()
    {
        // Arrange
        using var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 512,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.Device);

        // Act
        handle.Sync();

        // Assert
        Assert.Equal(512, handle.ThreadsWaiting);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.Warp,
            capacity: 32,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.None);

        // Act & Assert
        handle.Dispose();
        handle.Dispose(); // Should not throw
    }

    [Fact]
    public void Sync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 256,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.Threadgroup);

        handle.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => handle.Sync());
    }

    [Fact]
    public void Reset_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 256,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.Threadgroup);

        handle.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => handle.Reset());
    }

    [Fact]
    public void ThreadsWaiting_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var handle = new MetalBarrierHandle(
            barrierId: 1,
            scope: BarrierScope.ThreadBlock,
            capacity: 256,
            name: null,
            fenceFlags: MetalMemoryFenceFlags.Threadgroup);

        handle.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _ = handle.ThreadsWaiting);
    }

    [Fact]
    public void Properties_ReturnCorrectValues()
    {
        // Arrange
        const int barrierId = 42;
        const BarrierScope scope = BarrierScope.ThreadBlock;
        const int capacity = 768;
        const string name = "my_barrier";

        using var handle = new MetalBarrierHandle(
            barrierId: barrierId,
            scope: scope,
            capacity: capacity,
            name: name,
            fenceFlags: MetalMemoryFenceFlags.Device);

        // Act & Assert
        Assert.Equal(barrierId, handle.BarrierId);
        Assert.Equal(scope, handle.Scope);
        Assert.Equal(capacity, handle.Capacity);
        Assert.Equal(name, handle.Name);
    }
}
