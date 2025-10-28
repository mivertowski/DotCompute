// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
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
/// Comprehensive tests for UnifiedBufferCore covering all core functionality.
/// Target: 70-80 comprehensive tests for 507-line core buffer implementation.
/// Tests constructors, properties, memory access, device operations, copy operations, and state management.
/// </summary>
public sealed class UnifiedBufferCoreComprehensiveTests : IDisposable
{
    private IUnifiedMemoryManager _mockMemoryManager = null!;
    private readonly List<IDisposable> _disposables = new();

    public UnifiedBufferCoreComprehensiveTests()
    {
        _mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        _mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);
        _mockMemoryManager.AllocateDevice(Arg.Any<long>()).Returns(new DeviceMemory(new IntPtr(0x1000), 1024));
        _mockMemoryManager.When(x => x.CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.When(x => x.CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.When(x => x.CopyDeviceToDevice(Arg.Any<DeviceMemory>(), Arg.Any<DeviceMemory>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }

    #region Constructor Tests - Length-based

    [Fact]
    public void Constructor_WithValidLength_CreatesBuffer()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Assert
        buffer.Length.Should().Be(100);
        buffer.SizeInBytes.Should().Be(400); // 100 * sizeof(int)
        buffer.State.Should().Be(BufferState.HostOnly);
        buffer.IsOnHost.Should().BeTrue();
        buffer.IsOnDevice.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithZeroLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new UnifiedBuffer<int>(_mockMemoryManager, 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("length");
    }

    [Fact]
    public void Constructor_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new UnifiedBuffer<int>(_mockMemoryManager, -10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("length");
    }

    [Fact]
    public void Constructor_WithNullMemoryManager_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new UnifiedBuffer<int>(null!, 100);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("memoryManager");
    }

    [Fact]
    public void Constructor_ExceedsMaxAllocationSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var limitedManager = Substitute.For<IUnifiedMemoryManager>();
        limitedManager.MaxAllocationSize.Returns(100); // Very small limit

        // Act
        var act = () => new UnifiedBuffer<int>(limitedManager, 1000);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed size*");
    }

    [Fact]
    public void Constructor_WithLargeBuffer_CalculatesSizeCorrectly()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<long>(_mockMemoryManager, 1000);
        _disposables.Add(buffer);

        // Assert
        buffer.Length.Should().Be(1000);
        buffer.SizeInBytes.Should().Be(8000); // 1000 * sizeof(long)
    }

    #endregion

    #region Constructor Tests - Data-based

    [Fact]
    public void Constructor_WithData_CreatesAndPopulatesBuffer()
    {
        // Arrange
        ReadOnlySpan<int> data = stackalloc int[] { 1, 2, 3, 4, 5 };

        // Act
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data);
        _disposables.Add(buffer);

        // Assert
        buffer.Length.Should().Be(5);
        buffer.AsReadOnlySpan().ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Constructor_WithEmptyData_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var data = Array.Empty<int>();

        // Act
        var act = () => new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithDataCopiesCorrectly()
    {
        // Arrange
        var sourceData = new int[] { 10, 20, 30, 40 };

        // Act
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, sourceData.AsSpan());
        _disposables.Add(buffer);
        sourceData[0] = 999; // Modify source

        // Assert
        buffer.AsReadOnlySpan()[0].Should().Be(10); // Should not be affected
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Length_ReturnsCorrectValue()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 42);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.Length.Should().Be(42);
    }

    [Fact]
    public void SizeInBytes_CalculatedCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<float>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.SizeInBytes.Should().Be(400); // 100 * 4
    }

    [Fact]
    public void IsOnHost_InitiallyTrue()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void IsOnDevice_InitiallyFalse()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.IsOnDevice.Should().BeFalse();
    }

    [Fact]
    public void IsDirty_InitiallyFalse()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Accelerator_ReturnsNull()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.Accelerator.Should().BeNull();
    }

    [Fact]
    public void Options_ReturnsNone()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.Options.Should().Be(MemoryOptions.None);
    }

    [Fact]
    public void State_InitiallyHostOnly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.State.Should().Be(BufferState.HostOnly);
    }

    [Fact]
    public void IsDisposed_InitiallyFalse()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void DevicePointer_WhenNotOnDevice_ReturnsZero()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.DevicePointer.Should().Be(IntPtr.Zero);
    }

    [Fact]
    public void DevicePointer_WhenOnDevice_ReturnsNonZero()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act & Assert
        buffer.DevicePointer.Should().NotBe(IntPtr.Zero);
    }

    #endregion

    #region Memory Access Tests

    [Fact]
    public void AsSpan_ReturnsValidSpan()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsSpan();

        // Assert
        span.Length.Should().Be(100);
    }

    [Fact]
    public void AsSpan_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act & Assert
        buffer.Invoking(b => b.AsSpan()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AsSpan_AllowsModification()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsSpan();
        span[0] = 42;

        // Assert
        buffer.AsReadOnlySpan()[0].Should().Be(42);
    }

    [Fact]
    public void AsReadOnlySpan_ReturnsValidSpan()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsReadOnlySpan();

        // Assert
        span.Length.Should().Be(5);
        span.ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void AsReadOnlySpan_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act & Assert
        buffer.Invoking(b => b.AsReadOnlySpan()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AsMemory_ReturnsValidMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var memory = buffer.AsMemory();

        // Assert
        memory.Length.Should().Be(100);
    }

    [Fact]
    public void AsMemory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.AsMemory();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AsReadOnlyMemory_ReturnsValidMemory()
    {
        // Arrange
        var data = new int[] { 10, 20, 30 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var memory = buffer.AsReadOnlyMemory();

        // Assert
        memory.Length.Should().Be(3);
        memory.ToArray().Should().Equal(10, 20, 30);
    }

    [Fact]
    public void AsReadOnlyMemory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.AsReadOnlyMemory();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Device Operations Tests

    [Fact]
    public void GetDeviceMemory_ReturnsValidDeviceMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var deviceMemory = buffer.GetDeviceMemory();

        // Assert
        deviceMemory.Handle.Should().NotBe(IntPtr.Zero);
        buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void GetDeviceMemory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.GetDeviceMemory();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetHostPointer_ReturnsValidPointer()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var pointer = buffer.GetHostPointer();

        // Assert
        pointer.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void GetHostPointer_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.GetHostPointer();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Slice Tests

    [Fact]
    public void Slice_WithValidRange_CreatesSlice()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var slice = buffer.Slice(2, 5);
        _disposables.Add(slice);

        // Assert
        slice.Length.Should().Be(5);
        slice.AsReadOnlySpan().ToArray().Should().Equal(3, 4, 5, 6, 7);
    }

    [Fact]
    public void Slice_WithNegativeStart_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.Slice(-1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("start");
    }

    [Fact]
    public void Slice_WithStartBeyondLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.Slice(100, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("start");
    }

    [Fact]
    public void Slice_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.Slice(10, -5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("length");
    }

    [Fact]
    public void Slice_WithLengthExceedingBuffer_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.Slice(90, 20);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("length");
    }

    [Fact]
    public void Slice_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.Slice(0, 10);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Slice_AtStart_CreatesCorrectSlice()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var slice = buffer.Slice(0, 3);
        _disposables.Add(slice);

        // Assert
        slice.AsReadOnlySpan().ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Slice_AtEnd_CreatesCorrectSlice()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var slice = buffer.Slice(3, 2);
        _disposables.Add(slice);

        // Assert
        slice.AsReadOnlySpan().ToArray().Should().Equal(4, 5);
    }

    #endregion

    #region CopyFrom Tests

    [Fact]
    public void CopyFrom_WithMatchingLength_CopiesData()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3, 4, 5 };
        var source = new UnifiedBuffer<int>(_mockMemoryManager, sourceData.AsSpan());
        _disposables.Add(source);

        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        _disposables.Add(dest);

        // Act
        dest.CopyFrom(source);

        // Assert
        dest.AsReadOnlySpan().ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void CopyFrom_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.CopyFrom(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CopyFrom_WithMismatchedLength_ThrowsArgumentException()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(source);

        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 20);
        _disposables.Add(dest);

        // Act
        var act = () => dest.CopyFrom(source);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*lengths must match*");
    }

    [Fact]
    public void CopyFrom_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(source);

        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        dest.Dispose();

        // Act
        var act = () => dest.CopyFrom(source);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void CopyFrom_UnifiedBuffer_DeviceToDevice_UsesOptimizedPath()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(source);
        source.EnsureOnDevice();

        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(dest);
        dest.EnsureOnDevice();

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        dest.CopyFrom(source);

        // Assert
        _mockMemoryManager.Received(1).CopyDeviceToDevice(
            Arg.Any<DeviceMemory>(),
            Arg.Any<DeviceMemory>(),
            Arg.Any<long>());
    }

    #endregion

    #region CopyToAsync Tests

    [Fact]
    public async Task CopyToAsync_WithMatchingLength_CopiesData()
    {
        // Arrange
        var sourceData = new int[] { 10, 20, 30 };
        var source = new UnifiedBuffer<int>(_mockMemoryManager, sourceData.AsSpan());
        _disposables.Add(source);

        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 3);
        _disposables.Add(dest);

        // Act
        await source.CopyToAsync(dest);

        // Assert
        dest.AsReadOnlySpan().ToArray().Should().Equal(10, 20, 30);
    }

    [Fact]
    public async Task CopyToAsync_WithNullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = async () => await buffer.CopyToAsync((IUnifiedMemoryBuffer<int>)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CopyToAsync_WithMismatchedLength_ThrowsArgumentException()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(source);

        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 20);
        _disposables.Add(dest);

        // Act
        var act = async () => await source.CopyToAsync(dest);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CopyToAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        source.Dispose();

        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(dest);

        // Act
        var act = async () => await source.CopyToAsync(dest);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region CopyFromAsync Tests

    [Fact]
    public async Task CopyFromAsync_WithValidData_CopiesSuccessfully()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        _disposables.Add(buffer);
        var sourceData = new int[] { 1, 2, 3, 4, 5 };

        // Act
        await buffer.CopyFromAsync(sourceData.AsMemory());

        // Assert
        buffer.AsReadOnlySpan().ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task CopyFromAsync_WithLargerSource_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 3);
        _disposables.Add(buffer);
        var sourceData = new int[] { 1, 2, 3, 4, 5 };

        // Act
        var act = async () => await buffer.CopyFromAsync(sourceData.AsMemory());

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyFromAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        buffer.Dispose();
        var sourceData = new int[] { 1, 2, 3, 4, 5 };

        // Act
        var act = async () => await buffer.CopyFromAsync(sourceData.AsMemory());

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task CopyFromAsync_MarksHostDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.Synchronize();

        var sourceData = new int[] { 1, 2, 3, 4, 5 };

        // Act
        await buffer.CopyFromAsync(sourceData.AsMemory());

        // Assert
        buffer.State.Should().Be(BufferState.HostDirty);
    }

    #endregion

    #region CopyToAsync Memory Tests

    [Fact]
    public async Task CopyToAsync_Memory_WithValidDestination_CopiesSuccessfully()
    {
        // Arrange
        var sourceData = new int[] { 10, 20, 30, 40 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, sourceData.AsSpan());
        _disposables.Add(buffer);

        var destination = new int[4];

        // Act
        await buffer.CopyToAsync(destination.AsMemory());

        // Assert
        destination.Should().Equal(10, 20, 30, 40);
    }

    [Fact]
    public async Task CopyToAsync_Memory_WithSmallerDestination_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);
        var destination = new int[5];

        // Act
        var act = async () => await buffer.CopyToAsync(destination.AsMemory());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CopyToAsync_Memory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        buffer.Dispose();
        var destination = new int[5];

        // Act
        var act = async () => await buffer.CopyToAsync(destination.AsMemory());

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region CopyToAsync with Offsets Tests

    [Fact]
    public async Task CopyToAsync_WithOffsets_CopiesCorrectRange()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var source = new UnifiedBuffer<int>(_mockMemoryManager, sourceData.AsSpan());
        _disposables.Add(source);

        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(dest);

        // Act
        await source.CopyToAsync(2, dest, 3, 4);

        // Assert
        var result = dest.AsReadOnlySpan();
        result[3].Should().Be(3);
        result[4].Should().Be(4);
        result[5].Should().Be(5);
        result[6].Should().Be(6);
    }

    [Fact]
    public async Task CopyToAsync_WithOffsets_NullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act
        var act = async () => await buffer.CopyToAsync(0, null!, 0, 5);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CopyToAsync_WithOffsets_NegativeSourceOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(source);
        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(dest);

        // Act
        var act = async () => await source.CopyToAsync(-1, dest, 0, 5);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyToAsync_WithOffsets_NegativeDestinationOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(source);
        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(dest);

        // Act
        var act = async () => await source.CopyToAsync(0, dest, -1, 5);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyToAsync_WithOffsets_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(source);
        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(dest);

        // Act
        var act = async () => await source.CopyToAsync(0, dest, 0, -5);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyToAsync_WithOffsets_ExceedsSourceLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(source);
        var dest = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(dest);

        // Act
        var act = async () => await source.CopyToAsync(5, dest, 0, 10);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region FillAsync Tests

    [Fact]
    public async Task FillAsync_FillsEntireBuffer()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act
        await buffer.FillAsync(42);

        // Assert
        buffer.AsReadOnlySpan().ToArray().Should().OnlyContain(x => x == 42);
    }

    [Fact]
    public async Task FillAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        buffer.Dispose();

        // Act
        var act = async () => await buffer.FillAsync(42);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task FillAsync_MarksHostDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);
        buffer.Synchronize();

        // Act
        await buffer.FillAsync(100);

        // Assert
        buffer.State.Should().Be(BufferState.HostDirty);
    }

    #endregion

    #region FillAsync with Range Tests

    [Fact]
    public async Task FillAsync_WithRange_FillsCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act
        await buffer.FillAsync(99, 2, 5);

        // Assert
        var result = buffer.AsReadOnlySpan();
        result[0].Should().Be(0);
        result[1].Should().Be(0);
        result[2].Should().Be(99);
        result[6].Should().Be(99);
        result[7].Should().Be(0);
    }

    [Fact]
    public async Task FillAsync_WithRange_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act
        var act = async () => await buffer.FillAsync(42, -1, 5);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FillAsync_WithRange_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act
        var act = async () => await buffer.FillAsync(42, 0, -5);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FillAsync_WithRange_ExceedsLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);

        // Act
        var act = async () => await buffer.FillAsync(42, 5, 10);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FillAsync_WithRange_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        buffer.Dispose();

        // Act
        var act = async () => await buffer.FillAsync(42, 0, 5);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region AsType Tests

    [Fact]
    public void AsType_CompatibleTypes_ConvertsSuccessfully()
    {
        // Arrange - int to float (both 4 bytes)
        var data = new int[] { 1, 2, 3, 4 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var converted = buffer.AsType<float>();
        _disposables.Add(converted);

        // Assert
        converted.Length.Should().Be(4);
        converted.SizeInBytes.Should().Be(16);
    }

    [Fact]
    public void AsType_LargerToSmaller_ConvertsSuccessfully()
    {
        // Arrange - long (8 bytes) to int (4 bytes)
        var data = new long[] { 1L, 2L };
        var buffer = new UnifiedBuffer<long>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var converted = buffer.AsType<int>();
        _disposables.Add(converted);

        // Assert
        converted.Length.Should().Be(4); // 2 longs = 4 ints
    }

    [Fact]
    public void AsType_IncompatibleSizes_ThrowsArgumentException()
    {
        // Arrange - 3 ints (12 bytes) cannot evenly divide into longs (8 bytes each)
        var data = new int[] { 1, 2, 3 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.AsType<long>();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*sizes are not compatible*");
    }

    [Fact]
    public void AsType_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        buffer.Dispose();

        // Act
        var act = () => buffer.AsType<float>();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AsType_ByteToInt_ConvertsCorrectly()
    {
        // Arrange
        var data = new byte[16]; // 16 bytes = 4 ints
        var buffer = new UnifiedBuffer<byte>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var converted = buffer.AsType<int>();
        _disposables.Add(converted);

        // Assert
        converted.Length.Should().Be(4);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void MarkHostDirty_FromHostOnly_RemainsHostOnly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.MarkHostDirty();

        // Assert
        buffer.State.Should().Be(BufferState.HostOnly);
    }

    [Fact]
    public void MarkHostDirty_FromSynchronized_BecomesHostDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();

        // Act
        buffer.MarkHostDirty();

        // Assert
        buffer.State.Should().Be(BufferState.HostDirty);
    }

    [Fact]
    public void MarkHostDirty_FromDeviceDirty_BecomesHostDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();
        buffer.MarkDeviceDirty();

        // Act
        buffer.MarkHostDirty();

        // Assert
        buffer.State.Should().Be(BufferState.HostDirty);
    }

    [Fact]
    public void MarkHostDirty_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.MarkHostDirty();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void MarkDeviceDirty_FromDeviceOnly_BecomesDeviceDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.InvalidateHost();

        // Act
        buffer.MarkDeviceDirty();

        // Assert
        buffer.State.Should().Be(BufferState.DeviceDirty);
    }

    [Fact]
    public void MarkDeviceDirty_FromSynchronized_BecomesDeviceDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();

        // Act
        buffer.MarkDeviceDirty();

        // Assert
        buffer.State.Should().Be(BufferState.DeviceDirty);
    }

    [Fact]
    public void MarkDeviceDirty_FromHostDirty_BecomesDeviceDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();
        buffer.MarkHostDirty();

        // Act
        buffer.MarkDeviceDirty();

        // Assert
        buffer.State.Should().Be(BufferState.DeviceDirty);
    }

    [Fact]
    public void MarkDeviceDirty_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.MarkDeviceDirty();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Non-Generic Interface Tests

    [Fact]
    public void NonGenericCopyFromAsync_MatchingTypes_CopiesSuccessfully()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        _disposables.Add(buffer);
        var sourceData = new int[] { 1, 2, 3, 4, 5 };

        // Act
        IUnifiedMemoryBuffer nonGeneric = buffer;
        var task = nonGeneric.CopyFromAsync<int>(sourceData.AsMemory(), 0);
        task.AsTask().Wait();

        // Assert
        buffer.AsReadOnlySpan().ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task NonGenericCopyFromAsync_TypeMismatch_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        _disposables.Add(buffer);
        var sourceData = new long[] { 1L, 2L };

        // Act
        IUnifiedMemoryBuffer nonGeneric = buffer;
        var act = async () => await nonGeneric.CopyFromAsync<long>(sourceData.AsMemory(), 0);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Type mismatch*");
    }

    [Fact]
    public void NonGenericCopyFromAsync_WithOffset_CopiesCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);
        var sourceData = new int[] { 100, 200, 300 };

        // Act
        IUnifiedMemoryBuffer nonGeneric = buffer;
        var task = nonGeneric.CopyFromAsync<int>(sourceData.AsMemory(), sizeof(int) * 2);
        task.AsTask().Wait();

        // Assert
        buffer.AsReadOnlySpan()[2].Should().Be(100);
        buffer.AsReadOnlySpan()[3].Should().Be(200);
        buffer.AsReadOnlySpan()[4].Should().Be(300);
    }

    [Fact]
    public async Task NonGenericCopyFromAsync_ExceedsCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 3);
        _disposables.Add(buffer);
        var sourceData = new int[] { 1, 2, 3, 4, 5 };

        // Act
        IUnifiedMemoryBuffer nonGeneric = buffer;
        var act = async () => await nonGeneric.CopyFromAsync<int>(sourceData.AsMemory(), 0);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NonGenericCopyToAsync_MatchingTypes_CopiesSuccessfully()
    {
        // Arrange
        var sourceData = new int[] { 10, 20, 30 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, sourceData.AsSpan());
        _disposables.Add(buffer);
        var destination = new int[5];

        // Act
        IUnifiedMemoryBuffer nonGeneric = buffer;
        var task = nonGeneric.CopyToAsync<int>(destination.AsMemory(), 0);
        task.AsTask().Wait();

        // Assert
        destination[0].Should().Be(10);
        destination[1].Should().Be(20);
        destination[2].Should().Be(30);
    }

    [Fact]
    public async Task NonGenericCopyToAsync_TypeMismatch_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 5);
        _disposables.Add(buffer);
        var destination = new long[5];

        // Act
        IUnifiedMemoryBuffer nonGeneric = buffer;
        var act = async () => await nonGeneric.CopyToAsync<long>(destination.AsMemory(), 0);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Type mismatch*");
    }

    [Fact]
    public void NonGenericCopyToAsync_WithOffset_CopiesCorrectly()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, sourceData.AsSpan());
        _disposables.Add(buffer);
        var destination = new int[3];

        // Act
        IUnifiedMemoryBuffer nonGeneric = buffer;
        var task = nonGeneric.CopyToAsync<int>(destination.AsMemory(), sizeof(int) * 2);
        task.AsTask().Wait();

        // Assert
        destination.Should().Equal(3, 4, 5);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void CompleteLifecycle_CreationToDisposal_WorksCorrectly()
    {
        // Arrange & Act
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.AsSpan()[0] = 42;
        buffer.EnsureOnDevice();
        buffer.Synchronize();
        var value = buffer.AsReadOnlySpan()[0];
        buffer.Dispose();

        // Assert
        value.Should().Be(42);
        buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void ZeroLengthBuffer_Operations_ThrowsAppropriately()
    {
        // Act & Assert
        var act = () => new UnifiedBuffer<int>(_mockMemoryManager, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LargeBuffer_OperationsSucceed()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10000);
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsSpan();
        span[5000] = 999;

        // Assert
        buffer.AsReadOnlySpan()[5000].Should().Be(999);
    }

    #endregion
}
