// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.RingKernels;
using FluentAssertions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Unit tests for CudaDevicePointerBuffer wrapper.
/// Tests buffer properties, state management, and interface implementation.
/// </summary>
public class CudaDevicePointerBufferTests
{
    [Fact(DisplayName = "Constructor should initialize properties correctly")]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var devicePtr = new IntPtr(0x1000);
        var sizeInBytes = 4096L;
        var options = MemoryOptions.None;

        // Act
        var buffer = new CudaDevicePointerBuffer(devicePtr, sizeInBytes, options);

        // Assert
        buffer.DevicePointer.Should().Be(devicePtr);
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.Options.Should().Be(options);
        buffer.IsDisposed.Should().BeFalse();
        buffer.State.Should().Be(BufferState.Allocated);
    }

    [Fact(DisplayName = "Constructor should accept zero pointer")]
    public void Constructor_ShouldAcceptZeroPointer()
    {
        // Act
        var buffer = new CudaDevicePointerBuffer(IntPtr.Zero, 0, MemoryOptions.None);

        // Assert
        buffer.DevicePointer.Should().Be(IntPtr.Zero);
        buffer.SizeInBytes.Should().Be(0);
    }

    [Fact(DisplayName = "CopyFromAsync should throw NotSupportedException")]
    public async Task CopyFromAsync_ShouldThrowNotSupportedException()
    {
        // Arrange
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None);
        var data = new int[] { 1, 2, 3, 4 };

        // Act
        Func<Task> act = async () => await buffer.CopyFromAsync<int>(data);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*read-only wrapper*");
    }

    [Fact(DisplayName = "CopyToAsync should throw NotSupportedException")]
    public async Task CopyToAsync_ShouldThrowNotSupportedException()
    {
        // Arrange
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None);
        var data = new int[4];

        // Act
        Func<Task> act = async () => await buffer.CopyToAsync<int>(data);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*read-only wrapper*");
    }

    [Fact(DisplayName = "Dispose should mark buffer as disposed")]
    public void Dispose_ShouldMarkAsDisposed()
    {
        // Arrange
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None);

        // Act
        buffer.Dispose();

        // Assert
        buffer.IsDisposed.Should().BeTrue();
        buffer.State.Should().Be(BufferState.Disposed);
    }

    [Fact(DisplayName = "Dispose should be idempotent")]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None);

        // Act
        buffer.Dispose();
        buffer.Dispose();
        buffer.Dispose();

        // Assert
        buffer.IsDisposed.Should().BeTrue();
    }

    [Fact(DisplayName = "DisposeAsync should mark buffer as disposed")]
    public async Task DisposeAsync_ShouldMarkAsDisposed()
    {
        // Arrange
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None);

        // Act
        await buffer.DisposeAsync();

        // Assert
        buffer.IsDisposed.Should().BeTrue();
        buffer.State.Should().Be(BufferState.Disposed);
    }

    [Fact(DisplayName = "DevicePointer should preserve exact pointer value")]
    public void DevicePointer_ShouldPreserveExactValue()
    {
        // Arrange
        var expectedPtr = new IntPtr(0xDEADBEEF);

        // Act
        var buffer = new CudaDevicePointerBuffer(expectedPtr, 2048, MemoryOptions.None);

        // Assert
        buffer.DevicePointer.Should().Be(expectedPtr, "exact pointer value must be preserved");
    }

    [Theory(DisplayName = "SizeInBytes should accept various sizes")]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(4096L)]
    [InlineData(1024L * 1024L)]
    [InlineData(long.MaxValue)]
    public void SizeInBytes_ShouldAcceptVariousSizes(long size)
    {
        // Act
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), size, MemoryOptions.None);

        // Assert
        buffer.SizeInBytes.Should().Be(size);
    }

    [Fact(DisplayName = "Options should preserve all memory option flags")]
    public void Options_ShouldPreserveAllFlags()
    {
        // Arrange
        var options = MemoryOptions.Pinned | MemoryOptions.WriteCombined;

        // Act
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, options);

        // Assert
        buffer.Options.Should().Be(options);
        buffer.Options.Should().HaveFlag(MemoryOptions.Pinned);
        buffer.Options.Should().HaveFlag(MemoryOptions.WriteCombined);
    }

    [Fact(DisplayName = "State should transition correctly on disposal")]
    public void State_ShouldTransitionCorrectly()
    {
        // Arrange
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None);

        // Act & Assert - Initial state
        buffer.State.Should().Be(BufferState.Allocated);
        buffer.IsDisposed.Should().BeFalse();

        // Act - Dispose
        buffer.Dispose();

        // Assert - Final state
        buffer.State.Should().Be(BufferState.Disposed);
        buffer.IsDisposed.Should().BeTrue();
    }

    [Fact(DisplayName = "Should implement IUnifiedMemoryBuffer interface")]
    public void ShouldImplementIUnifiedMemoryBufferInterface()
    {
        // Arrange
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None);

        // Act & Assert
        buffer.Should().BeAssignableTo<IUnifiedMemoryBuffer>();
    }

    [Fact(DisplayName = "Multiple buffers with same pointer should be independent")]
    public void MultipleBuffers_WithSamePointer_ShouldBeIndependent()
    {
        // Arrange
        var sharedPtr = new IntPtr(0x5000);
        var buffer1 = new CudaDevicePointerBuffer(sharedPtr, 1024, MemoryOptions.None);
        var buffer2 = new CudaDevicePointerBuffer(sharedPtr, 1024, MemoryOptions.None);

        // Act
        buffer1.Dispose();

        // Assert
        buffer1.IsDisposed.Should().BeTrue();
        buffer2.IsDisposed.Should().BeFalse("buffer2 should remain independent");
        buffer2.DevicePointer.Should().Be(sharedPtr, "pointer should be unchanged");
    }

    [Fact(DisplayName = "Should be lightweight value wrapper")]
    public void ShouldBeLightweightValueWrapper()
    {
        // Arrange & Act
        var buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None);

        // Assert - Verify lightweight nature
        buffer.Should().NotBeNull();
        buffer.DevicePointer.Should().NotBe(IntPtr.Zero);
        buffer.SizeInBytes.Should().BeGreaterThan(0);

        // Dispose should not free memory (parent owns it)
        buffer.Dispose();
        buffer.DevicePointer.Should().NotBe(IntPtr.Zero, "pointer should remain valid after dispose");
    }

    [Fact(DisplayName = "Should support using statement pattern")]
    public void ShouldSupportUsingStatementPattern()
    {
        // Arrange
        CudaDevicePointerBuffer? buffer = null;

        // Act
        using (buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None))
        {
            buffer.IsDisposed.Should().BeFalse();
        }

        // Assert
        buffer.IsDisposed.Should().BeTrue();
    }

    [Fact(DisplayName = "Should support await using pattern")]
    public async Task ShouldSupportAwaitUsingPattern()
    {
        // Arrange
        CudaDevicePointerBuffer? buffer = null;

        // Act
        await using (buffer = new CudaDevicePointerBuffer(new IntPtr(0x1000), 1024, MemoryOptions.None))
        {
            buffer.IsDisposed.Should().BeFalse();
        }

        // Assert
        buffer.IsDisposed.Should().BeTrue();
    }
}
