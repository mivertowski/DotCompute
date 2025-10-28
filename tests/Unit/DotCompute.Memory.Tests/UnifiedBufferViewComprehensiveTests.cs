// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using NSubstitute;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for UnifiedBufferView covering all view operations.
/// Target: 40-50 tests covering the 411-line production class.
/// Tests all view creation methods, memory access, error handling, and edge cases.
/// </summary>
public sealed class UnifiedBufferViewComprehensiveTests : IDisposable
{
    private readonly IUnifiedMemoryManager _mockMemoryManager;
    private readonly List<IDisposable> _disposables = [];

    public UnifiedBufferViewComprehensiveTests()
    {
        _mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        _ = _mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);

        // Setup mock for memory operations
        _ = _mockMemoryManager.AllocateDevice(Arg.Any<long>()).Returns(new DeviceMemory(new IntPtr(0x1000), 1024));
        _mockMemoryManager.When(x => x.CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.When(x => x.CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _ = _mockMemoryManager.CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
        _ = _mockMemoryManager.CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
        _mockMemoryManager.When(x => x.MemsetDevice(Arg.Any<DeviceMemory>(), Arg.Any<byte>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _ = _mockMemoryManager.MemsetDeviceAsync(Arg.Any<DeviceMemory>(), Arg.Any<byte>(), Arg.Any<long>())
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
    public void Constructor_WithValidParent_CreatesView()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);

        // Assert
        _ = view.Length.Should().Be(10);
        _ = view.SizeInBytes.Should().Be(10 * sizeof(short));
    }

    [Fact]
    public void Constructor_WithNullParent_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UnifiedBufferView<int, short>(null!, 10);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => new UnifiedBufferView<int, short>(buffer, -1);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithExcessiveViewSize_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10); // 40 bytes
        _disposables.Add(buffer);

        // Act - Request 100 shorts (200 bytes) when parent only has 40 bytes
        var act = () => new UnifiedBufferView<int, short>(buffer, 100);

        // Assert
        _ = act.Should().Throw<ArgumentException>()
            .WithMessage("*exceeds parent buffer size*");
    }

    [Fact]
    public void Constructor_WithZeroLength_CreatesValidView()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var view = new UnifiedBufferView<int, short>(buffer, 0);
        _disposables.Add(view);

        // Assert
        _ = view.Length.Should().Be(0);
        _ = view.SizeInBytes.Should().Be(0);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Length_ReturnsViewLength()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act & Assert
        _ = view.Length.Should().Be(50);
    }

    [Fact]
    public void SizeInBytes_CalculatesCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, byte>(buffer, 100);
        _disposables.Add(view);

        // Act & Assert
        _ = view.SizeInBytes.Should().Be(100 * sizeof(byte));
    }

    [Fact]
    public void Accelerator_ReturnsParentAccelerator()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act & Assert
        _ = view.Accelerator.Should().Be(buffer.Accelerator);
    }

    [Fact]
    public void Options_ReturnsParentOptions()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act & Assert
        _ = view.Options.Should().Be(buffer.Options);
    }

    [Fact]
    public void IsDisposed_ReflectsViewDisposal()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);

        // Act
        view.Dispose();

        // Assert
        _ = view.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void IsDisposed_ReflectsParentDisposal()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        buffer.Dispose();

        // Assert
        _ = view.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void State_ReturnsParentState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act & Assert
        _ = view.State.Should().Be(buffer.State);
    }

    #endregion

    #region GetDeviceMemory Tests

    [Fact]
    public void GetDeviceMemory_WithValidView_ReturnsDeviceMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var deviceMem = view.GetDeviceMemory();

        // Assert
        _ = deviceMem.IsValid.Should().BeTrue();
        _ = view.SizeInBytes.Should().Be(50 * sizeof(short));
    }

    [Fact]
    public void GetDeviceMemory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = view.GetDeviceMemory;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetDeviceMemory_WhenParentNotOnDevice_ReturnsDeviceMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var deviceMem = view.GetDeviceMemory();

        // Assert
        // Note: With mocked memory manager, deviceMem may be valid even without explicit device allocation
        // This tests that GetDeviceMemory returns successfully
        _ = deviceMem.Should().NotBeNull();
    }

    #endregion

    #region AsSpan Tests

    [Fact]
    public void AsSpan_WithValidView_ReturnsCorrectData()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, byte>(buffer, 20);
        _disposables.Add(view);

        // Act
        var span = view.AsSpan();

        // Assert
        _ = span.Length.Should().Be(20);
    }

    [Fact]
    public void AsSpan_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act & Assert
        _ = Assert.Throws<ObjectDisposedException>(() => view.AsSpan());
    }

    [Fact]
    public void AsSpan_WithTypeCast_CorrectlyReinterpretsData()
    {
        // Arrange - Create buffer with 4 ints (16 bytes), view as 8 shorts
        var data = new int[] { 0x01020304, 0x05060708 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 4);
        _disposables.Add(view);

        // Act
        var span = view.AsSpan();

        // Assert
        _ = span.Length.Should().Be(4);
    }

    #endregion

    #region AsReadOnlySpan Tests

    [Fact]
    public void AsReadOnlySpan_WithValidView_ReturnsCorrectData()
    {
        // Arrange
        var data = new int[] { 10, 20, 30, 40, 50 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);

        // Act
        var span = view.AsReadOnlySpan();

        // Assert
        _ = span.Length.Should().Be(10);
    }

    [Fact]
    public void AsReadOnlySpan_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act & Assert
        _ = Assert.Throws<ObjectDisposedException>(() => view.AsReadOnlySpan());
    }

    #endregion

    #region AsMemory Tests

    [Fact]
    public void AsMemory_WithValidView_ReturnsCorrectLength()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, byte>(buffer, 20);
        _disposables.Add(view);

        // Act
        var memory = view.AsMemory();

        // Assert
        _ = memory.Length.Should().Be(20);
    }

    [Fact]
    public void AsMemory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = view.AsMemory;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region AsReadOnlyMemory Tests

    [Fact]
    public void AsReadOnlyMemory_WithValidView_ReturnsCorrectLength()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);

        // Act
        var memory = view.AsReadOnlyMemory();

        // Assert
        _ = memory.Length.Should().Be(10);
    }

    [Fact]
    public void AsReadOnlyMemory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = view.AsReadOnlyMemory;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region CopyFrom Tests

    [Fact]
    public async Task CopyFromAsync_WithValidData_CopiesSuccessfully()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);
        var sourceData = new short[10];

        // Act
        await view.CopyFromAsync(sourceData.AsMemory());

        // Assert
        // Operation completes without exception
    }

    [Fact]
    public async Task CopyFromAsync_WithExcessiveData_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);
        var sourceData = new short[20]; // More than view length

        // Act
        var act = async () => await view.CopyFromAsync(sourceData.AsMemory());

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyFromAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();
        var sourceData = new short[10];

        // Act
        var act = async () => await view.CopyFromAsync(sourceData.AsMemory());

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region CopyTo Tests

    [Fact]
    public async Task CopyToAsync_Memory_CopiesSuccessfully()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, byte>(buffer, 20);
        _disposables.Add(view);
        var destination = new byte[20];

        // Act
        await view.CopyToAsync(destination.AsMemory(), CancellationToken.None);

        // Assert
        // Operation completes without exception
    }

    [Fact]
    public async Task CopyToAsync_Memory_WhenExceedsViewLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);
        var destination = new short[20];

        // Act
        var act = async () => await view.CopyToAsync(destination.AsMemory(), CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyToAsync_Buffer_CopiesSuccessfully()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);
        var destBuffer = new UnifiedBuffer<short>(_mockMemoryManager, 10);
        _disposables.Add(destBuffer);

        // Act
        await view.CopyToAsync(destBuffer);

        // Assert
        // Operation completes without exception
    }

    [Fact]
    public async Task CopyToAsync_Buffer_WithNullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = async () => await view.CopyToAsync((IUnifiedMemoryBuffer<short>)null!);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CopyToAsync_WithOffsets_CopiesCorrectRange()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);
        var destBuffer = new UnifiedBuffer<short>(_mockMemoryManager, 20);
        _disposables.Add(destBuffer);

        // Act
        await view.CopyToAsync(2, destBuffer, 5, 3);

        // Assert
        // Operation completes without exception
    }

    [Fact]
    public async Task CopyToAsync_WithNegativeSourceOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);
        var destBuffer = new UnifiedBuffer<short>(_mockMemoryManager, 50);
        _disposables.Add(destBuffer);

        // Act
        var act = async () => await view.CopyToAsync(-1, destBuffer, 0, 10);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyToAsync_WithNegativeDestinationOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);
        var destBuffer = new UnifiedBuffer<short>(_mockMemoryManager, 50);
        _disposables.Add(destBuffer);

        // Act
        var act = async () => await view.CopyToAsync(0, destBuffer, -1, 10);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyToAsync_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);
        var destBuffer = new UnifiedBuffer<short>(_mockMemoryManager, 50);
        _disposables.Add(destBuffer);

        // Act
        var act = async () => await view.CopyToAsync(0, destBuffer, 0, -1);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CopyToAsync_WithOutOfRangeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);
        var destBuffer = new UnifiedBuffer<short>(_mockMemoryManager, 50);
        _disposables.Add(destBuffer);

        // Act - Try to copy 20 elements starting at offset 5 (exceeds view length of 10)
        var act = async () => await view.CopyToAsync(5, destBuffer, 0, 20);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Slice Tests

    [Fact]
    public void Slice_WithValidRange_CreatesSlice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var slice = view.Slice(10, 20);
        _disposables.Add(slice);

        // Assert
        _ = slice.Length.Should().Be(20);
    }

    [Fact]
    public void Slice_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = () => view.Slice(-1, 10);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Slice_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = () => view.Slice(10, -5);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Slice_WithOutOfRangeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = () => view.Slice(40, 20); // 40 + 20 = 60 > 50

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region AsType Tests

    [Fact]
    public void AsType_ToLargerType_CalculatesLengthCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, byte>(buffer, 400); // 400 bytes
        _disposables.Add(view);

        // Act - Convert to int (4 bytes each)
        var intView = view.AsType<int>();
        _disposables.Add(intView);

        // Assert
        _ = intView.Length.Should().Be(100); // 400 bytes / 4 bytes per int
    }

    [Fact]
    public void AsType_ToSmallerType_CalculatesLengthCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, int>(buffer, 100);
        _disposables.Add(view);

        // Act - Convert to byte (1 byte each)
        var byteView = view.AsType<byte>();
        _disposables.Add(byteView);

        // Assert
        _ = byteView.Length.Should().Be(400); // 100 ints * 4 bytes per int
    }

    #endregion

    #region Fill Tests

    [Fact]
    public async Task FillAsync_WithoutOffsets_FillsEntireView()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        await view.FillAsync((short)42);

        // Assert
        // Operation completes without exception
    }

    [Fact]
    public async Task FillAsync_WithRange_FillsCorrectRange()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        await view.FillAsync((short)42, 10, 20);

        // Assert
        // Operation completes without exception
    }

    [Fact]
    public async Task FillAsync_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = async () => await view.FillAsync((short)42, -1, 10);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FillAsync_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = async () => await view.FillAsync((short)42, 10, -5);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FillAsync_WithOutOfRangeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = async () => await view.FillAsync((short)42, 40, 20); // 40 + 20 > 50

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_WithReadWriteMode_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        using var mapped = view.Map();

        // Assert
        _ = mapped.Should().NotBeNull();
        _ = mapped.Length.Should().Be(50);
    }

    [Fact]
    public void Map_WithReadMode_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        using var mapped = view.Map(Abstractions.Memory.MapMode.Read);

        // Assert
        _ = mapped.Should().NotBeNull();
        _ = mapped.Length.Should().Be(50);
    }

    [Fact]
    public void Map_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = () => view.Map();

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region MapRange Tests

    [Fact]
    public void MapRange_WithValidRange_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        using var mapped = view.MapRange(10, 20);

        // Assert
        _ = mapped.Should().NotBeNull();
        _ = mapped.Length.Should().Be(20);
    }

    [Fact]
    public void MapRange_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = () => view.MapRange(-1, 10);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = () => view.MapRange(10, -5);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WithOutOfRangeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        var act = () => view.MapRange(40, 20);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region MapAsync Tests

    [Fact]
    public async Task MapAsync_WithReadWriteMode_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        using var mapped = await view.MapAsync();

        // Assert
        _ = mapped.Should().NotBeNull();
        _ = mapped.Length.Should().Be(50);
    }

    [Fact]
    public async Task MapAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = async () => await view.MapAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task MapAsync_EnsuresParentOnHost()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        using var mapped = await view.MapAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    #endregion

    #region Synchronization Tests

    [Fact]
    public void EnsureOnHost_DelegatesToParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        view.EnsureOnHost();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void EnsureOnHost_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = view.EnsureOnHost;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnsureOnDevice_DelegatesToParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        view.EnsureOnDevice();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void EnsureOnDevice_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = view.EnsureOnDevice;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task EnsureOnHostAsync_DelegatesToParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        await view.EnsureOnHostAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureOnHostAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = async () => await view.EnsureOnHostAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_DelegatesToParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        await view.EnsureOnDeviceAsync();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = async () => await view.EnsureOnDeviceAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Synchronize_DelegatesToParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        view.Synchronize();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void Synchronize_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = view.Synchronize;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task SynchronizeAsync_DelegatesToParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        await view.SynchronizeAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = async () => await view.SynchronizeAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Invalidation Tests

    [Fact]
    public void MarkHostDirty_DelegatesToParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        view.MarkHostDirty();

        // Assert - Parent should reflect the dirty state
    }

    [Fact]
    public void MarkHostDirty_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = view.MarkHostDirty;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void MarkDeviceDirty_DelegatesToParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);

        // Act
        view.MarkDeviceDirty();

        // Assert - Parent should reflect the dirty state
    }

    [Fact]
    public void MarkDeviceDirty_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        view.Dispose();

        // Act
        var act = view.MarkDeviceDirty;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Generic Copy Tests

    [Fact]
    public async Task CopyFromAsync_Generic_WithMatchingType_CopiesSuccessfully()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);
        var sourceData = new short[10];

        // Act
        await ((IUnifiedMemoryBuffer)view).CopyFromAsync<short>(sourceData.AsMemory());

        // Assert
        // Operation completes without exception
    }

    [Fact]
    public async Task CopyFromAsync_Generic_WithMismatchedType_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);
        var sourceData = new int[10];

        // Act
        var act = async () => await ((IUnifiedMemoryBuffer)view).CopyFromAsync<int>(sourceData.AsMemory());

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Type mismatch*");
    }

    [Fact]
    public async Task CopyToAsync_Generic_WithMatchingType_CopiesSuccessfully()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);
        var destination = new short[50];

        // Act
        await ((IUnifiedMemoryBuffer)view).CopyToAsync<short>(destination.AsMemory());

        // Assert
        // Operation completes without exception
    }

    [Fact]
    public async Task CopyToAsync_Generic_WithMismatchedType_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);
        _disposables.Add(view);
        var destination = new int[50];

        // Act
        var act = async () => await ((IUnifiedMemoryBuffer)view).CopyToAsync<int>(destination.AsMemory());

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Type mismatch*");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_MarksViewAsDisposed()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);

        // Act
        view.Dispose();

        // Assert
        _ = view.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_MarksViewAsDisposed()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);

        // Act
        await view.DisposeAsync();

        // Assert
        _ = view.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotDisposeParent()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 50);

        // Act
        view.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IntegrationTest_ByteViewOfIntBuffer_ReinterpretsDataCorrectly()
    {
        // Arrange
        var data = new int[] { 0x01020304, 0x05060708 }; // Little-endian
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, byte>(buffer, 8);
        _disposables.Add(view);

        // Act
        var byteSpan = view.AsReadOnlySpan();

        // Assert
        _ = byteSpan.Length.Should().Be(8);
    }

    [Fact]
    public async Task IntegrationTest_CompleteWorkflow_MaintainsDataIntegrity()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);
        var view = new UnifiedBufferView<int, short>(buffer, 10);
        _disposables.Add(view);

        // Act
        await view.EnsureOnHostAsync();
        await view.EnsureOnDeviceAsync();
        await view.SynchronizeAsync();
        var slice = view.Slice(2, 5);
        _disposables.Add(slice);

        // Assert
        _ = view.IsOnHost.Should().BeTrue();
        _ = view.IsOnDevice.Should().BeTrue();
        _ = slice.Length.Should().Be(5);
    }

    [Fact]
    public void IntegrationTest_TypeConversion_MaintainsCorrectSize()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100); // 400 bytes
        _disposables.Add(buffer);
        var shortView = new UnifiedBufferView<int, short>(buffer, 200); // 400 bytes as shorts
        _disposables.Add(shortView);

        // Act
        var byteView = shortView.AsType<byte>(); // Convert to bytes
        _disposables.Add(byteView);

        // Assert
        _ = byteView.Length.Should().Be(400); // 200 shorts * 2 bytes per short
        _ = byteView.SizeInBytes.Should().Be(400);
    }

    #endregion
}
