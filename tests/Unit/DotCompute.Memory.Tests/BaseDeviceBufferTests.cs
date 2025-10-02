// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Tests.Common;
using DotCompute.Tests.Common.Mocks;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for BaseDeviceBuffer specialization.
/// </summary>
public class BaseDeviceBufferTests
{
    /// <summary>
    /// Gets device buffer_ initializes correctly.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    [Fact]
    [Trait("Category", "BufferTypes")]
    public async Task DeviceBuffer_InitializesCorrectly()
    {
        // Arrange
        await using var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();

        // Act

        using var buffer = new TestMemoryBuffer<float>(256);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.MemoryType.Should().Be(MemoryType.Device);
        _ = buffer.Accelerator.Should().BeSameAs(accelerator);
        _ = buffer.SizeInBytes.Should().Be(1024);
        _ = buffer.Length.Should().Be(256); // 1024 bytes / 4 bytes per float
    }
    /// <summary>
    /// Gets device buffer_ supports multiple memory types.
    /// </summary>
    /// <param name="memoryType">The memory type.</param>
    /// <returns>The result of the operation.</returns>


    [Theory]
    [InlineData(MemoryType.Device)]
    [InlineData(MemoryType.Shared)]
    [InlineData(MemoryType.Pinned)]
    [Trait("Category", "BufferTypes")]
    public async Task DeviceBuffer_SupportsMultipleMemoryTypes(MemoryType memoryType)
    {
        // Arrange
        await using var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();

        // Act

        using var buffer = new TestMemoryBuffer<int>(128); // 512 bytes / 4 bytes per int

        // Assert
        _ = buffer.MemoryType.Should().Be(memoryType);
        _ = buffer.State.Should().Be(BufferState.Allocated);
    }
    /// <summary>
    /// Gets device buffer_ throws on host operations.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", "BufferTypes")]
    public async Task DeviceBuffer_ThrowsOnHostOperations()
    {
        // Arrange
        await using var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        using var buffer = new TestMemoryBuffer<double>(100); // 800 bytes / 8 bytes per double

        // Act & Assert

        Action spanAccess = () => buffer.AsSpan();
        Action readOnlySpanAccess = () => buffer.AsReadOnlySpan();

        _ = spanAccess.Should().Throw<NotSupportedException>("device buffers don't support direct span access");
        _ = readOnlySpanAccess.Should().Throw<NotSupportedException>("device buffers don't support direct span access");
    }
    /// <summary>
    /// Gets device buffer_ tracks disposal state.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    [Fact]
    [Trait("Category", "BufferTypes")]
    public async Task DeviceBuffer_TracksDisposalState()
    {
        // Arrange
        await using var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var buffer = new TestMemoryBuffer<float>(64); // 256 bytes / 4 bytes per float

        // Act

        await buffer.DisposeAsync();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
        _ = buffer.State.Should().Be(BufferState.Disposed);
    }
}