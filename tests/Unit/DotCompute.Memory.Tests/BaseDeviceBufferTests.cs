// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Memory.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for BaseDeviceBuffer specialization.
/// </summary>
public class BaseDeviceBufferTests
{
    [Fact]
    [Trait("Category", "BufferTypes")]
    public async Task DeviceBuffer_InitializesCorrectly()
    {
        // Arrange
        await using var accelerator = new TestAccelerator();

        // Act

        using var buffer = new TestDeviceBuffer<float>(accelerator, 1024);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.MemoryType.Should().Be(MemoryType.Device);
        _ = buffer.Accelerator.Should().BeSameAs(accelerator);
        _ = buffer.SizeInBytes.Should().Be(1024);
        _ = buffer.Length.Should().Be(256); // 1024 bytes / 4 bytes per float
    }


    [Theory]
    [InlineData(MemoryType.Device)]
    [InlineData(MemoryType.Shared)]
    [InlineData(MemoryType.Pinned)]
    [Trait("Category", "BufferTypes")]
    public async Task DeviceBuffer_SupportsMultipleMemoryTypes(MemoryType memoryType)
    {
        // Arrange
        await using var accelerator = new TestAccelerator();

        // Act

        using var buffer = new TestDeviceBuffer<int>(accelerator, 512, memoryType);

        // Assert
        _ = buffer.MemoryType.Should().Be(memoryType);
        _ = buffer.State.Should().Be(BufferState.Allocated);
    }


    [Fact]
    [Trait("Category", "BufferTypes")]
    public async Task DeviceBuffer_ThrowsOnHostOperations()
    {
        // Arrange
        await using var accelerator = new TestAccelerator();
        using var buffer = new TestDeviceBuffer<double>(accelerator, 800);

        // Act & Assert

        Action spanAccess = () => buffer.AsSpan();
        Action readOnlySpanAccess = () => buffer.AsReadOnlySpan();

        _ = spanAccess.Should().Throw<NotSupportedException>("device buffers don't support direct span access");
        _ = readOnlySpanAccess.Should().Throw<NotSupportedException>("device buffers don't support direct span access");
    }


    [Fact]
    [Trait("Category", "BufferTypes")]
    public async Task DeviceBuffer_TracksDisposalState()
    {
        // Arrange
        await using var accelerator = new TestAccelerator();
        var buffer = new TestDeviceBuffer<float>(accelerator, 256);

        // Act

        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
        _ = buffer.State.Should().Be(BufferState.Disposed);
    }
}