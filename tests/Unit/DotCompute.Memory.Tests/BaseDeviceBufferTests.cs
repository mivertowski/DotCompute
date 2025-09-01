// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;
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
    public void DeviceBuffer_InitializesCorrectly()
    {
        // Arrange
        var accelerator = new TestAccelerator();
        
        // Act
        using var buffer = new TestDeviceBuffer<float>(accelerator, 1024);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.MemoryType.Should().Be(MemoryType.Device);
        buffer.Accelerator.Should().BeSameAs(accelerator);
        buffer.SizeInBytes.Should().Be(1024);
        buffer.Length.Should().Be(256); // 1024 bytes / 4 bytes per float
    }
    
    [Theory]
    [InlineData(MemoryType.Device)]
    [InlineData(MemoryType.Shared)]
    [InlineData(MemoryType.Pinned)]
    [Trait("Category", "BufferTypes")]
    public void DeviceBuffer_SupportsMultipleMemoryTypes(MemoryType memoryType)
    {
        // Arrange
        var accelerator = new TestAccelerator();
        
        // Act
        using var buffer = new TestDeviceBuffer<int>(accelerator, 512, memoryType);
        
        // Assert
        buffer.MemoryType.Should().Be(memoryType);
        buffer.State.Should().Be(BufferState.Allocated);
    }
    
    [Fact]
    [Trait("Category", "BufferTypes")]
    public void DeviceBuffer_ThrowsOnHostOperations()
    {
        // Arrange
        var accelerator = new TestAccelerator();
        using var buffer = new TestDeviceBuffer<double>(accelerator, 800);
        
        // Act & Assert
        Action spanAccess = () => buffer.AsSpan();
        Action readOnlySpanAccess = () => buffer.AsReadOnlySpan();
        
        spanAccess.Should().Throw<NotSupportedException>("device buffers don't support direct span access");
        readOnlySpanAccess.Should().Throw<NotSupportedException>("device buffers don't support direct span access");
    }
    
    [Fact]
    [Trait("Category", "BufferTypes")]
    public void DeviceBuffer_TracksDisposalState()
    {
        // Arrange
        var accelerator = new TestAccelerator();
        var buffer = new TestDeviceBuffer<float>(accelerator, 256);
        
        // Act
        buffer.Dispose();
        
        // Assert
        buffer.IsDisposed.Should().BeTrue();
        buffer.State.Should().Be(BufferState.Disposed);
    }
}