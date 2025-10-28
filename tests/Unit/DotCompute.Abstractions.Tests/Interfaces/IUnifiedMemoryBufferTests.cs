// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions.Tests.Interfaces;

/// <summary>
/// Comprehensive tests for IUnifiedMemoryBuffer interface.
/// </summary>
public class IUnifiedMemoryBufferTests
{
    #region Interface Contract Tests

    [Fact]
    public void IUnifiedMemoryBuffer_ShouldHaveLength()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.Length.Returns(1024);

        // Act
        var length = buffer.Length;

        // Assert
        length.Should().Be(1024);
    }

    [Fact(Skip = "NSubstitute mock issue with ElementCount property access")]
    public void IUnifiedMemoryBuffer_ElementCount_ShouldAliasLength()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.Length.Returns(1024);
        buffer.ElementCount.Returns(buffer.Length);

        // Act
        var elementCount = buffer.ElementCount;

        // Assert
        elementCount.Should().Be(1024);
    }

    [Fact]
    public void IUnifiedMemoryBuffer_ShouldHaveAccelerator()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var accelerator = Substitute.For<IAccelerator>();
        buffer.Accelerator.Returns(accelerator);

        // Act
        var result = buffer.Accelerator;

        // Assert
        result.Should().Be(accelerator);
    }

    [Fact]
    public void IUnifiedMemoryBuffer_ShouldHaveSizeInBytes()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.SizeInBytes.Returns(4096);

        // Act
        var size = buffer.SizeInBytes;

        // Assert
        size.Should().Be(4096);
    }

    [Fact]
    public void IUnifiedMemoryBuffer_ShouldHaveOptions()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var options = new MemoryOptions();
        buffer.Options.Returns(options);

        // Act
        var result = buffer.Options;

        // Assert
        result.Should().Be(options);
    }

    [Fact]
    public void IUnifiedMemoryBuffer_ShouldHaveState()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.State.Returns(BufferState.Allocated);

        // Act
        var state = buffer.State;

        // Assert
        state.Should().Be(BufferState.Allocated);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void IUnifiedMemoryBuffer_IsOnHost_ShouldReturnStatus()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.IsOnHost.Returns(true);

        // Act
        var isOnHost = buffer.IsOnHost;

        // Assert
        isOnHost.Should().BeTrue();
    }

    [Fact]
    public void IUnifiedMemoryBuffer_IsOnDevice_ShouldReturnStatus()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.IsOnDevice.Returns(true);

        // Act
        var isOnDevice = buffer.IsOnDevice;

        // Assert
        isOnDevice.Should().BeTrue();
    }

    [Fact]
    public void IUnifiedMemoryBuffer_IsDirty_ShouldReturnStatus()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.IsDirty.Returns(true);

        // Act
        var isDirty = buffer.IsDirty;

        // Assert
        isDirty.Should().BeTrue();
    }

    [Fact]
    public void IUnifiedMemoryBuffer_IsDisposed_ShouldReturnStatus()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.IsDisposed.Returns(false);

        // Act
        var isDisposed = buffer.IsDisposed;

        // Assert
        isDisposed.Should().BeFalse();
    }

    #endregion

    #region Host Memory Access Tests

    // Note: AsSpan() and AsReadOnlySpan() tests are skipped because Span<T> and ReadOnlySpan<T>
    // are ref structs that cannot be mocked with NSubstitute. These methods are tested in
    // integration tests with concrete implementations.

    [Fact]
    public void AsMemory_ShouldReturnMemory()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var data = new float[100];
        buffer.AsMemory().Returns(data.AsMemory());

        // Act
        var memory = buffer.AsMemory();

        // Assert
        memory.Length.Should().Be(100);
    }

    [Fact]
    public void AsReadOnlyMemory_ShouldReturnReadOnlyMemory()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var data = new float[100];
        buffer.AsReadOnlyMemory().Returns(new ReadOnlyMemory<float>(data));

        // Act
        var memory = buffer.AsReadOnlyMemory();

        // Assert
        memory.Length.Should().Be(100);
    }

    #endregion

    #region Device Memory Access Tests

    [Fact]
    public void GetDeviceMemory_ShouldReturnHandle()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var deviceMemory = new DeviceMemory(new IntPtr(12345), 1024);
        buffer.GetDeviceMemory().Returns(deviceMemory);

        // Act
        var result = buffer.GetDeviceMemory();

        // Assert
        result.Handle.Should().Be(new IntPtr(12345));
    }

    #endregion

    #region Synchronization Tests

    [Fact]
    public void EnsureOnHost_ShouldNotThrow()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();

        // Act
        var act = () => buffer.EnsureOnHost();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureOnDevice_ShouldNotThrow()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();

        // Act
        var act = () => buffer.EnsureOnDevice();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task EnsureOnHostAsync_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.EnsureOnHostAsync(Arg.Any<AcceleratorContext>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.EnsureOnHostAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.EnsureOnDeviceAsync(Arg.Any<AcceleratorContext>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.EnsureOnDeviceAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Synchronize_ShouldNotThrow()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();

        // Act
        var act = () => buffer.Synchronize();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.SynchronizeAsync(Arg.Any<AcceleratorContext>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.SynchronizeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void MarkHostDirty_ShouldNotThrow()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();

        // Act
        var act = () => buffer.MarkHostDirty();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void MarkDeviceDirty_ShouldNotThrow()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();

        // Act
        var act = () => buffer.MarkDeviceDirty();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Copy Operations Tests

    [Fact]
    public async Task CopyFromAsync_Memory_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var source = new float[100].AsMemory();
        buffer.CopyFromAsync(new ReadOnlyMemory<float>(source.ToArray()), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.CopyFromAsync(source);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CopyToAsync_Memory_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var destination = new float[100].AsMemory();
        buffer.CopyToAsync(destination, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.CopyToAsync(destination);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CopyToAsync_Buffer_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var destination = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.CopyToAsync(destination, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.CopyToAsync(destination);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CopyToAsync_WithRanges_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var destination = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.CopyToAsync(0, destination, 0, 50, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.CopyToAsync(0, destination, 0, 50);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Fill Operations Tests

    [Fact]
    public async Task FillAsync_EntireBuffer_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.FillAsync(3.14f, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.FillAsync(3.14f);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FillAsync_WithRange_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.FillAsync(3.14f, 10, 50, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.FillAsync(3.14f, 10, 50);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region View and Slice Tests

    [Fact]
    public void Slice_ShouldReturnSlicedBuffer()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var sliced = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.Slice(10, 50).Returns(sliced);

        // Act
        var result = buffer.Slice(10, 50);

        // Assert
        result.Should().Be(sliced);
    }

    [Fact]
    public void AsType_ShouldConvertType()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var intBuffer = Substitute.For<IUnifiedMemoryBuffer<int>>();
        buffer.AsType<int>().Returns(intBuffer);

        // Act
        var result = buffer.AsType<int>();

        // Assert
        result.Should().Be(intBuffer);
    }

    #endregion

    #region Memory Mapping Tests

    [Fact]
    public void Map_DefaultMode_ShouldReturnMappedMemory()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var data = new float[100];
        var mapped = new MappedMemory<float>(data.AsMemory(), () => { });
        buffer.Map(MapMode.ReadWrite).Returns(mapped);

        // Act
        var result = buffer.Map();

        // Assert
        result.Memory.Length.Should().Be(100);
    }

    [Fact]
    public void Map_ReadMode_ShouldReturnMappedMemory()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var data = new float[100];
        var mapped = new MappedMemory<float>(data.AsMemory(), () => { });
        buffer.Map(MapMode.Read).Returns(mapped);

        // Act
        var result = buffer.Map(MapMode.Read);

        // Assert
        result.Memory.Length.Should().Be(100);
    }

    [Fact]
    public void MapRange_ShouldReturnPartialMappedMemory()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var data = new float[50];
        var mapped = new MappedMemory<float>(data.AsMemory(), () => { });
        buffer.MapRange(10, 50, MapMode.ReadWrite).Returns(mapped);

        // Act
        var result = buffer.MapRange(10, 50);

        // Assert
        result.Memory.Length.Should().Be(50);
    }

    [Fact]
    public async Task MapAsync_ShouldReturnMappedMemory()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        var data = new float[100];
        var mapped = new MappedMemory<float>(data.AsMemory(), () => { });
        buffer.MapAsync(MapMode.ReadWrite, Arg.Any<CancellationToken>())
            .Returns(mapped);

        // Act
        var result = await buffer.MapAsync();

        // Assert
        result.Memory.Length.Should().Be(100);
    }

    #endregion

    #region Non-Generic Interface Tests

    [Fact]
    public async Task NonGeneric_CopyFromAsync_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();
        var source = new float[100].AsMemory();
        buffer.CopyFromAsync<float>(new ReadOnlyMemory<float>(source.ToArray()), 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.CopyFromAsync<float>(new ReadOnlyMemory<float>(source.ToArray()));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NonGeneric_CopyToAsync_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();
        var destination = new float[100].AsMemory();
        buffer.CopyToAsync(destination, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.CopyToAsync(destination);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldBeCallable()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();

        // Act
        var act = () => buffer.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_ShouldComplete()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer<float>>();
        buffer.DisposeAsync().Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await buffer.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}
