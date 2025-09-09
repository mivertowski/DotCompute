// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Memory.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for BaseUnifiedBuffer specialization.
/// </summary>
public class BaseUnifiedBufferTests
{
    [Fact]
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_InitializesCorrectly()
    {
        // Arrange & Act
        using var buffer = new TestUnifiedBuffer<double>(512); // 64 elements

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.MemoryType.Should().Be(MemoryType.Unified);
        _ = buffer.SizeInBytes.Should().Be(512);
        _ = buffer.Length.Should().Be(64); // 512 bytes / 8 bytes per double
    }


    [Fact]
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_SupportsWrappingExistingMemory()
    {
        // Arrange
        var existingPointer = IntPtr.Zero; // Test pointer

        // Act

        using var buffer = new TestUnifiedBuffer<int>(existingPointer, 256);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = buffer.SizeInBytes.Should().Be(256);
        _ = buffer.MemoryType.Should().Be(MemoryType.Unified);
    }


    [Fact]
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_HandlesSlicing()
    {
        // Arrange
        using var buffer = new TestUnifiedBuffer<float>(64); // 16 elements

        // Act

        var slice = buffer.Slice(4, 8); // Get middle 8 elements

        // Assert
        _ = slice.Should().NotBeNull();
        _ = slice.Should().BeSameAs(buffer); // Test implementation returns self
    }


    [Theory]
    [InlineData(-1, 4)] // Negative start
    [InlineData(0, -1)] // Negative length
    [InlineData(10, 20)] // Beyond buffer bounds
    [InlineData(15, 5)] // Start + length > buffer length
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_SliceValidatesParameters(int start, int length)
    {
        // Arrange
        using var buffer = new TestUnifiedBuffer<float>(64); // 16 elements

        // Act & Assert

        Action act = () => buffer.Slice(start, length);
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }


    [Fact]
    [Trait("Category", "BufferTypes")]
    public void UnifiedBuffer_ProvidesHostAndDeviceAccess()
    {
        // Arrange
        using var buffer = new TestUnifiedBuffer<long>(256); // 32 elements

        // Act

        var span = buffer.AsSpan();
        var readOnlySpan = buffer.AsReadOnlySpan();
        var memory = buffer.Memory;
        var devicePtr = buffer.DevicePointer;

        // Assert
        _ = span.Length.Should().BeGreaterThan(0);
        _ = readOnlySpan.Length.Should().BeGreaterThan(0);
        _ = memory.Should().NotBeNull();
        _ = devicePtr.Should().NotBe(IntPtr.Zero); // Unified buffer has valid device pointer
    }
}