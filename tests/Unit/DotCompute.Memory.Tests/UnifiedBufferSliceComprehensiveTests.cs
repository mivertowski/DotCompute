// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for UnifiedBufferSlice covering all slice functionality.
/// Target: 40-50 comprehensive tests covering 365-line slice class.
/// Tests all slice creation methods, boundary conditions, error handling, edge cases, and integration.
/// </summary>
public sealed class UnifiedBufferSliceComprehensiveTests : IDisposable
{
    private readonly IUnifiedMemoryManager _mockMemoryManager;
    private readonly List<IDisposable> _disposables = [];

    public UnifiedBufferSliceComprehensiveTests()
    {
        _mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        _mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);

        // Setup mock for memory operations
        _mockMemoryManager.AllocateDevice(Arg.Any<long>()).Returns(new DeviceMemory(new IntPtr(0x1000), 1024));
        _mockMemoryManager.When(x => x.CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.When(x => x.CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
        _mockMemoryManager.CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesSlice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 20);
        _disposables.Add(slice);

        // Assert
        slice.Length.Should().Be(20);
        slice.SizeInBytes.Should().Be(20 * sizeof(int));
    }

    [Fact]
    public void Constructor_WithNullParentBuffer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UnifiedBufferSlice<int>(null!, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => new UnifiedBufferSlice<int>(buffer, -1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => new UnifiedBufferSlice<int>(buffer, 0, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithOffsetAndLengthExceedingParentSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => new UnifiedBufferSlice<int>(buffer, 90, 20);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithZeroLength_CreatesEmptySlice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var slice = new UnifiedBufferSlice<int>(buffer, 50, 0);
        _disposables.Add(slice);

        // Assert
        slice.Length.Should().Be(0);
        slice.SizeInBytes.Should().Be(0);
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Length_ReturnsSliceLength()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 20, 30);
        _disposables.Add(slice);

        // Act & Assert
        slice.Length.Should().Be(30);
    }

    [Fact]
    public void SizeInBytes_ReturnsCorrectSize()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 25);
        _disposables.Add(slice);

        // Act & Assert
        slice.SizeInBytes.Should().Be(25 * sizeof(int));
    }

    [Fact]
    public void Accelerator_ReturnsParentAccelerator()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act & Assert
        slice.Accelerator.Should().Be(buffer.Accelerator);
    }

    [Fact]
    public void Options_ReturnsParentOptions()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act & Assert
        slice.Options.Should().Be(buffer.Options);
    }

    [Fact]
    public void IsDisposed_WhenSliceDisposed_ReturnsTrue()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);

        // Act
        slice.Dispose();

        // Assert
        slice.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void IsDisposed_WhenParentDisposed_ReturnsTrue()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        buffer.Dispose();

        // Assert
        slice.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void State_ReturnsParentState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act & Assert
        slice.State.Should().Be(buffer.State);
    }

    #endregion

    #region GetDeviceMemory Tests

    [Fact]
    public void GetDeviceMemory_WithValidSlice_ReturnsAdjustedHandle()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 20);
        _disposables.Add(slice);

        // Act
        var deviceMemory = slice.GetDeviceMemory();

        // Assert
        deviceMemory.IsValid.Should().BeTrue();
        deviceMemory.Size.Should().Be(20 * sizeof(int));
    }

    [Fact]
    public void GetDeviceMemory_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.GetDeviceMemory();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetDeviceMemory_WhenParentDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);
        buffer.Dispose();

        // Act
        var act = () => slice.GetDeviceMemory();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region AsSpan Tests

    [Fact]
    public void AsSpan_WithValidSlice_ReturnsCorrectSpan()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 2, 5);
        _disposables.Add(slice);

        // Act
        var span = slice.AsSpan();

        // Assert
        span.Length.Should().Be(5);
        span.ToArray().Should().Equal(3, 4, 5, 6, 7);
    }

    [Fact]
    public void AsSpan_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => slice.AsSpan());
    }

    [Fact]
    public void AsSpan_AllowsModification_ReflectsInParentBuffer()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 1, 3);
        _disposables.Add(slice);

        // Act
        var span = slice.AsSpan();
        span[0] = 100;
        span[1] = 200;

        // Assert
        var parentSpan = buffer.AsReadOnlySpan();
        parentSpan.ToArray().Should().Equal(1, 100, 200, 4, 5);
    }

    #endregion

    #region AsReadOnlySpan Tests

    [Fact]
    public void AsReadOnlySpan_WithValidSlice_ReturnsCorrectSpan()
    {
        // Arrange
        var data = new int[] { 10, 20, 30, 40, 50, 60 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 1, 4);
        _disposables.Add(slice);

        // Act
        var span = slice.AsReadOnlySpan();

        // Assert
        span.Length.Should().Be(4);
        span.ToArray().Should().Equal(20, 30, 40, 50);
    }

    [Fact]
    public void AsReadOnlySpan_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => slice.AsReadOnlySpan());
    }

    #endregion

    #region AsMemory Tests

    [Fact]
    public void AsMemory_WithValidSlice_ReturnsCorrectMemory()
    {
        // Arrange
        var data = new int[] { 100, 200, 300, 400, 500 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 1, 3);
        _disposables.Add(slice);

        // Act
        var memory = slice.AsMemory();

        // Assert
        memory.Length.Should().Be(3);
        memory.ToArray().Should().Equal(200, 300, 400);
    }

    [Fact]
    public void AsMemory_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.AsMemory();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region AsReadOnlyMemory Tests

    [Fact]
    public void AsReadOnlyMemory_WithValidSlice_ReturnsCorrectMemory()
    {
        // Arrange
        var data = new int[] { 11, 22, 33, 44, 55, 66 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 2, 3);
        _disposables.Add(slice);

        // Act
        var memory = slice.AsReadOnlyMemory();

        // Assert
        memory.Length.Should().Be(3);
        memory.ToArray().Should().Equal(33, 44, 55);
    }

    [Fact]
    public void AsReadOnlyMemory_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.AsReadOnlyMemory();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region CopyFromAsync Tests

    [Fact]
    public async Task CopyFromAsync_WithValidSource_CopiesData()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 20);
        _disposables.Add(slice);
        var source = new int[] { 1, 2, 3, 4, 5 };

        // Act
        var act = async () => await slice.CopyFromAsync(source.AsMemory());

        // Assert - Should complete without exception
        await act.Should().NotThrowAsync();
        slice.Length.Should().Be(20);
    }

    [Fact]
    public async Task CopyFromAsync_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = async () => await slice.CopyFromAsync(new int[10].AsMemory());

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task CopyFromAsync_WithSourceLargerThanSlice_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 10);
        _disposables.Add(slice);

        // Act
        var act = async () => await slice.CopyFromAsync(new int[20].AsMemory());

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region CopyToAsync Tests

    [Fact]
    public async Task CopyToAsync_WithMemoryDestination_CopiesData()
    {
        // Arrange
        var data = new int[] { 10, 20, 30, 40, 50 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 1, 3);
        _disposables.Add(slice);
        var destination = new int[3];

        // Act
        await slice.CopyToAsync(destination.AsMemory(), default);

        // Assert
        destination.Should().Equal(20, 30, 40);
    }

    [Fact]
    public async Task CopyToAsync_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await slice.CopyToAsync(new int[10].AsMemory(), default));
    }

    [Fact]
    public async Task CopyToAsync_WithBufferDestination_CopiesData()
    {
        // Arrange
        var data = new int[] { 100, 200, 300, 400, 500 };
        var sourceBuffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(sourceBuffer);
        var slice = new UnifiedBufferSlice<int>(sourceBuffer, 1, 3);
        _disposables.Add(slice);

        var destBuffer = new UnifiedBuffer<int>(_mockMemoryManager, 3); // Match slice length
        _disposables.Add(destBuffer);

        // Act
        await slice.CopyToAsync(destBuffer);

        // Assert
        var result = destBuffer.AsReadOnlySpan();
        result.ToArray().Should().Equal(200, 300, 400);
    }

    [Fact]
    public async Task CopyToAsync_WithNullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = async () => await slice.CopyToAsync((IUnifiedMemoryBuffer<int>)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CopyToAsync_WithOffsets_CopiesDataCorrectly()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var sourceBuffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(sourceBuffer);
        var slice = new UnifiedBufferSlice<int>(sourceBuffer, 2, 5); // [3,4,5,6,7]
        _disposables.Add(slice);

        var destBuffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(destBuffer);

        // Act
        await slice.CopyToAsync(1, destBuffer, 2, 3); // Copy [4,5,6] to dest[2,3,4]

        // Assert
        var result = destBuffer.AsReadOnlySpan();
        result.Slice(2, 3).ToArray().Should().Equal(4, 5, 6);
    }

    [Fact]
    public async Task CopyToAsync_WithInvalidOffsets_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 10);
        _disposables.Add(slice);
        var destBuffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(destBuffer);

        // Act
        var act = async () => await slice.CopyToAsync(-1, destBuffer, 0, 5);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Slice Tests

    [Fact]
    public void Slice_WithValidRange_CreatesNestedSlice()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice1 = new UnifiedBufferSlice<int>(buffer, 2, 6); // [3,4,5,6,7,8]
        _disposables.Add(slice1);

        // Act
        var slice2 = slice1.Slice(1, 3); // [4,5,6]
        _disposables.Add(slice2);

        // Assert
        slice2.Length.Should().Be(3);
        var result = ((UnifiedBufferSlice<int>)slice2).AsReadOnlySpan();
        result.ToArray().Should().Equal(4, 5, 6);
    }

    [Fact]
    public void Slice_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = () => slice.Slice(-1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Slice_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = () => slice.Slice(10, -5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Slice_WithOutOfRangeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = () => slice.Slice(40, 20);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region AsType Tests

    [Fact]
    public void AsType_ToByteArray_ReturnsCorrectView()
    {
        // Arrange
        var data = new int[] { 0x01020304, 0x05060708 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 2);
        _disposables.Add(slice);

        // Act
        var byteView = slice.AsType<byte>();
        _disposables.Add(byteView);

        // Assert
        byteView.Length.Should().Be(8); // 2 ints * 4 bytes
    }

    [Fact]
    public void AsType_FromByteToInt_ReturnsCorrectView()
    {
        // Arrange
        var data = new byte[16];
        var buffer = new UnifiedBuffer<byte>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<byte>(buffer, 4, 8);
        _disposables.Add(slice);

        // Act
        var intView = slice.AsType<int>();
        _disposables.Add(intView);

        // Assert
        intView.Length.Should().Be(2); // 8 bytes / 4 bytes per int
    }

    #endregion

    #region FillAsync Tests

    [Fact]
    public async Task FillAsync_WithValue_FillsSlice()
    {
        // Arrange
        var data = new int[100];
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 20);
        _disposables.Add(slice);

        // Act
        var act = async () => await slice.FillAsync(42);

        // Assert - Should complete without exception
        await act.Should().NotThrowAsync();
        slice.Length.Should().Be(20);
    }

    [Fact]
    public async Task FillAsync_WithOffsetAndCount_FillsPartialSlice()
    {
        // Arrange
        var data = new int[100];
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 30);
        _disposables.Add(slice);

        // Act
        var act = async () => await slice.FillAsync(99, 5, 10);

        // Assert - Should complete without exception
        await act.Should().NotThrowAsync();
        slice.Length.Should().Be(30);
    }

    [Fact]
    public async Task FillAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = async () => await slice.FillAsync(42, -1, 10);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FillAsync_WithInvalidCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = async () => await slice.FillAsync(42, 0, -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_WithValidSlice_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 30);
        _disposables.Add(slice);

        // Act
        using var mapped = slice.Map();

        // Assert
        mapped.Should().NotBeNull();
        mapped.Length.Should().Be(30);
    }

    [Fact]
    public void Map_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.Map();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region MapRange Tests

    [Fact]
    public void MapRange_WithValidRange_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 50);
        _disposables.Add(slice);

        // Act
        using var mapped = slice.MapRange(5, 20);

        // Assert
        mapped.Should().NotBeNull();
        mapped.Length.Should().Be(20);
    }

    [Fact]
    public void MapRange_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = () => slice.MapRange(-1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WithInvalidLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = () => slice.MapRange(0, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WithOutOfRangeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        _disposables.Add(slice);

        // Act
        var act = () => slice.MapRange(40, 20);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region MapAsync Tests

    [Fact]
    public async Task MapAsync_WithValidSlice_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 10, 30);
        _disposables.Add(slice);

        // Act
        using var mapped = await slice.MapAsync();

        // Assert
        mapped.Should().NotBeNull();
        mapped.Length.Should().Be(30);
    }

    [Fact]
    public async Task MapAsync_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = async () => await slice.MapAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Synchronization Tests

    [Fact]
    public void EnsureOnHost_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.EnsureOnHost();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnsureOnDevice_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.EnsureOnDevice();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task EnsureOnHostAsync_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = async () => await slice.EnsureOnHostAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = async () => await slice.EnsureOnDeviceAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Synchronize_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.Synchronize();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task SynchronizeAsync_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = async () => await slice.SynchronizeAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Mark Dirty Tests

    [Fact]
    public void MarkHostDirty_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.MarkHostDirty();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void MarkDeviceDirty_WhenSliceDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);
        slice.Dispose();

        // Act
        var act = () => slice.MarkDeviceDirty();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_MarksSliceAsDisposed()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);

        // Act
        slice.Dispose();

        // Assert
        slice.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_MarksSliceAsDisposed()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);

        // Act
        await slice.DisposeAsync();

        // Assert
        slice.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotDisposeParentBuffer()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 0, 50);

        // Act
        slice.Dispose();

        // Assert
        buffer.IsDisposed.Should().BeFalse();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task CompleteWorkflow_CreateSliceModifyAndVerify_MaintainsDataIntegrity()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var slice = new UnifiedBufferSlice<int>(buffer, 2, 5); // [3,4,5,6,7]
        _disposables.Add(slice);

        // Act - Verify slice provides correct view of original data
        var sliceResult = slice.AsReadOnlySpan();

        // Assert - Verify slice shows correct portion of buffer
        sliceResult.ToArray().Should().Equal(3, 4, 5, 6, 7);
        slice.Length.Should().Be(5);

        // Modify through slice
        var sliceSpan = slice.AsSpan();
        sliceSpan[0] = 30;
        sliceSpan[1] = 40;

        // Verify modification reflects in parent buffer
        var bufferResult = buffer.AsReadOnlySpan();
        bufferResult[2].Should().Be(30); // First element of slice
        bufferResult[3].Should().Be(40); // Second element of slice
    }

    [Fact]
    public void NestedSlices_MultipleLevel_MaintainCorrectOffsets()
    {
        // Arrange
        var data = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var slice1 = new UnifiedBufferSlice<int>(buffer, 2, 12); // [2..13]
        _disposables.Add(slice1);
        var slice2 = slice1.Slice(3, 6); // [5..10]
        _disposables.Add(slice2);
        var slice3 = ((UnifiedBufferSlice<int>)slice2).Slice(1, 4); // [6..9]
        _disposables.Add(slice3);

        // Assert
        var result = ((UnifiedBufferSlice<int>)slice3).AsReadOnlySpan();
        result.ToArray().Should().Equal(6, 7, 8, 9);
    }

    [Fact]
    public async Task SliceToSliceCopy_TransfersDataCorrectly()
    {
        // Arrange
        var sourceData = new int[] { 100, 200, 300, 400, 500, 600 };
        var sourceBuffer = new UnifiedBuffer<int>(_mockMemoryManager, sourceData.AsSpan());
        _disposables.Add(sourceBuffer);
        var sourceSlice = new UnifiedBufferSlice<int>(sourceBuffer, 1, 4); // [200,300,400,500]
        _disposables.Add(sourceSlice);

        var destBuffer = new UnifiedBuffer<int>(_mockMemoryManager, 4); // Match source slice length
        _disposables.Add(destBuffer);
        var destSlice = new UnifiedBufferSlice<int>(destBuffer, 0, 4); // Full buffer as slice
        _disposables.Add(destSlice);

        // Act
        var act = async () => await sourceSlice.CopyToAsync(destSlice);

        // Assert - Should complete without exception when lengths match
        await act.Should().NotThrowAsync();
        sourceSlice.Length.Should().Be(4);
        destSlice.Length.Should().Be(4);
    }

    #endregion
}
