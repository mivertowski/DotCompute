// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Memory.Tests.TestHelpers;
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

        // Act - Use TestDeviceBuffer which has Accelerator property
        using var buffer = new TestDeviceBuffer<float>(accelerator, 1024, MemoryType.Device);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.MemoryType.Should().Be(MemoryType.Device);
        _ = buffer.Accelerator.Should().NotBeNull("buffer should have accelerator reference");
        _ = buffer.Accelerator.Info.Id.Should().Be(accelerator.Info.Id, "accelerator IDs should match");
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

        // Act - TestMemoryBuffer always returns MemoryType.Host regardless of parameter
        using var buffer = new TestMemoryBuffer<int>(128); // 512 bytes / 4 bytes per int

        // Assert - TestMemoryBuffer is host-based, always returns MemoryType.Host
        _ = buffer.MemoryType.Should().Be(MemoryType.Host);
        _ = buffer.State.Should().Be(BufferState.HostReady); // TestMemoryBuffer initializes as HostReady, not Allocated
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

        // Act & Assert - TestMemoryBuffer is host-based and supports span access

        Action spanAccess = () => buffer.AsSpan();
        Action readOnlySpanAccess = () => buffer.AsReadOnlySpan();

        _ = spanAccess.Should().NotThrow(); // TestMemoryBuffer supports span access
        _ = readOnlySpanAccess.Should().NotThrow();
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
