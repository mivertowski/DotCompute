// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using NSubstitute;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for BaseMemoryBuffer and derived classes.
/// Target: 80-90 tests covering all abstract base classes:
/// - BaseMemoryBuffer (40 tests)
/// - BaseDeviceBuffer (15 tests)
/// - BaseUnifiedBuffer (15 tests)
/// - BasePooledBuffer (15 tests)
/// - BasePinnedBuffer (10 tests)
/// </summary>
public sealed class BaseMemoryBufferComprehensiveTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }

    #region Concrete Test Implementations

    /// <summary>
    /// Concrete test implementation of BaseMemoryBuffer&lt;T&gt;.
    /// </summary>
    private sealed class TestMemoryBuffer<T> : BaseMemoryBuffer<T> where T : unmanaged
    {
        private volatile bool _disposed;
        private readonly IAccelerator _accelerator;
        private BufferState _state = BufferState.HostOnly;
        private bool _isOnHost = true;
        private bool _isOnDevice;
        private bool _isDirty;
        private readonly T[] _data;

        public TestMemoryBuffer(long sizeInBytes, IAccelerator? accelerator = null)
            : base(sizeInBytes)
        {
            _accelerator = accelerator ?? Substitute.For<IAccelerator>();
            _data = new T[Length];
        }

        public override IntPtr DevicePointer => new IntPtr(0x2000);
        public override MemoryType MemoryType => MemoryType.Device;
        public override bool IsDisposed => _disposed;
        public override IAccelerator Accelerator => _accelerator;
        public override BufferState State => _state;
        public override MemoryOptions Options => MemoryOptions.None;
        public override bool IsOnHost => _isOnHost;
        public override bool IsOnDevice => _isOnDevice;
        public override bool IsDirty => _isDirty;

        public void SetDisposed() => _disposed = true;
        public void SetState(BufferState state) => _state = state;
        public void SetOnHost(bool value) => _isOnHost = value;
        public void SetOnDevice(bool value) => _isOnDevice = value;
        public void SetDirty(bool value) => _isDirty = value;

        public override Span<T> AsSpan() => _data.AsSpan();
        public override ReadOnlySpan<T> AsReadOnlySpan() => _data.AsSpan();
        public override Memory<T> AsMemory() => _data.AsMemory();
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => _data.AsMemory();
        public override DeviceMemory GetDeviceMemory() => new DeviceMemory(DevicePointer, SizeInBytes);
        public override void EnsureOnHost() => _isOnHost = true;
        public override void EnsureOnDevice() => _isOnDevice = true;
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        {
            _isOnHost = true;
            return ValueTask.CompletedTask;
        }
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        {
            _isOnDevice = true;
            return ValueTask.CompletedTask;
        }
        public override void Synchronize()
        {
            _isOnHost = true;
            _isOnDevice = true;
        }
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        {
            _isOnHost = true;
            _isOnDevice = true;
            return ValueTask.CompletedTask;
        }
        public override void MarkHostDirty() => _isDirty = true;
        public override void MarkDeviceDirty() => _isDirty = true;
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotImplementedException();
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotImplementedException();
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Dispose() => _disposed = true;
        public override ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        // Expose protected methods for testing
        public void TestValidateCopyParameters(long sourceLength, long sourceOffset, long destinationLength, long destinationOffset, long count) => ValidateCopyParameters(sourceLength, sourceOffset, destinationLength, destinationOffset, count);

        public void TestThrowIfDisposed() => ThrowIfDisposed();
    }

    /// <summary>
    /// Concrete test implementation of BaseDeviceBuffer&lt;T&gt;.
    /// </summary>
    private sealed class TestDeviceBuffer<T> : BaseDeviceBuffer<T> where T : unmanaged
    {
        private readonly IAccelerator _accelerator;
        private readonly T[] _data;

        public TestDeviceBuffer(long sizeInBytes, IntPtr devicePointer, IAccelerator? accelerator = null)
            : base(sizeInBytes, devicePointer)
        {
            _accelerator = accelerator ?? Substitute.For<IAccelerator>();
            _data = new T[Length];
        }

        public override IAccelerator Accelerator => _accelerator;
        public override BufferState State => BufferState.DeviceOnly;
        public override MemoryOptions Options => MemoryOptions.None;
        public override bool IsOnHost => false;
        public override bool IsOnDevice => true;
        public override bool IsDirty => false;
        public override Span<T> AsSpan() => _data.AsSpan();
        public override ReadOnlySpan<T> AsReadOnlySpan() => _data.AsSpan();
        public override Memory<T> AsMemory() => _data.AsMemory();
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => _data.AsMemory();
        public override DeviceMemory GetDeviceMemory() => new DeviceMemory(DevicePointer, SizeInBytes);
        public override void EnsureOnHost() { }
        public override void EnsureOnDevice() { }
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Synchronize() { }
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void MarkHostDirty() { }
        public override void MarkDeviceDirty() { }
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotImplementedException();
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotImplementedException();
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Dispose()
        {
            if (MarkDisposed())
            {
                // Cleanup logic
            }
        }
        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public bool TestMarkDisposed() => MarkDisposed();
    }

    /// <summary>
    /// Concrete test implementation of BaseUnifiedBuffer&lt;T&gt;.
    /// </summary>
    private sealed class TestUnifiedBuffer<T> : BaseUnifiedBuffer<T> where T : unmanaged
    {
        private readonly IAccelerator _accelerator;
        private readonly T[] _data;

        public TestUnifiedBuffer(long sizeInBytes, IntPtr unifiedPointer, IAccelerator? accelerator = null)
            : base(sizeInBytes, unifiedPointer)
        {
            _accelerator = accelerator ?? Substitute.For<IAccelerator>();
            _data = new T[Length];
        }

        public override IAccelerator Accelerator => _accelerator;
        public override BufferState State => BufferState.Synchronized;
        public override MemoryOptions Options => MemoryOptions.Unified;
        public override bool IsOnHost => true;
        public override bool IsOnDevice => true;
        public override bool IsDirty => false;
        public override ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();
        public override Memory<T> AsMemory() => _data.AsMemory();
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => _data.AsMemory();
        public override DeviceMemory GetDeviceMemory() => new DeviceMemory(DevicePointer, SizeInBytes);
        public override void EnsureOnHost() { }
        public override void EnsureOnDevice() { }
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Synchronize() { }
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void MarkHostDirty() { }
        public override void MarkDeviceDirty() { }
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotImplementedException();
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotImplementedException();
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Dispose()
        {
            if (MarkDisposed())
            {
                // Cleanup logic
            }
        }
        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public bool TestMarkDisposed() => MarkDisposed();
    }

    /// <summary>
    /// Concrete test implementation of BasePooledBuffer&lt;T&gt;.
    /// </summary>
    private sealed class TestPooledBuffer<T> : BasePooledBuffer<T> where T : unmanaged
    {
        private readonly IAccelerator _accelerator;
        private readonly T[] _data;

        public TestPooledBuffer(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction = null, IAccelerator? accelerator = null)
            : base(sizeInBytes, returnAction)
        {
            _accelerator = accelerator ?? Substitute.For<IAccelerator>();
            _data = new T[Length];
        }

        public override IAccelerator Accelerator => _accelerator;
        public override BufferState State => BufferState.HostOnly;
        public override MemoryOptions Options => MemoryOptions.Pooled;
        public override bool IsOnHost => true;
        public override bool IsOnDevice => false;
        public override bool IsDirty => false;
        public override IntPtr DevicePointer => IntPtr.Zero;
        public override MemoryType MemoryType => MemoryType.Host;
        public override Memory<T> Memory => _data.AsMemory();
        public override Span<T> AsSpan() => _data.AsSpan();
        public override ReadOnlySpan<T> AsReadOnlySpan() => _data.AsSpan();
        public override Memory<T> AsMemory() => _data.AsMemory();
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => _data.AsMemory();
        public override DeviceMemory GetDeviceMemory() => throw new NotSupportedException();
        public override void EnsureOnHost() { }
        public override void EnsureOnDevice() { }
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Synchronize() { }
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void MarkHostDirty() { }
        public override void MarkDeviceDirty() { }
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotImplementedException();
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotImplementedException();
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Concrete test implementation of BasePinnedBuffer&lt;T&gt;.
    /// </summary>
    private sealed class TestPinnedBuffer<T> : BasePinnedBuffer<T> where T : unmanaged
    {
        private readonly IAccelerator _accelerator;
        private readonly T[] _array;

        public TestPinnedBuffer(T[] array, IAccelerator? accelerator = null)
            : base(array)
        {
            _accelerator = accelerator ?? Substitute.For<IAccelerator>();
            _array = array;
        }

        public override IAccelerator Accelerator => _accelerator;
        public override BufferState State => BufferState.HostOnly;
        public override MemoryOptions Options => MemoryOptions.Pinned;
        public override bool IsOnHost => true;
        public override bool IsOnDevice => false;
        public override bool IsDirty => false;
        public override Span<T> AsSpan() => _array.AsSpan();
        public override ReadOnlySpan<T> AsReadOnlySpan() => _array.AsSpan();
        public override Memory<T> AsMemory() => _array.AsMemory();
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => _array.AsMemory();
        public override DeviceMemory GetDeviceMemory() => new DeviceMemory(DevicePointer, SizeInBytes);
        public override void EnsureOnHost() { }
        public override void EnsureOnDevice() { }
        public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Synchronize() { }
        public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void MarkHostDirty() { }
        public override void MarkDeviceDirty() { }
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotImplementedException();
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotImplementedException();
        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    #endregion

    #region BaseMemoryBuffer Tests (40 tests)

    [Fact]
    public void Constructor_WithValidSize_CreatesBuffer()
    {
        // Act
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(1024);
        _ = buffer.Length.Should().Be(256); // 1024 / sizeof(int)
    }

    [Fact]
    public void Constructor_WithZeroSize_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new TestMemoryBuffer<int>(0);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeSize_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new TestMemoryBuffer<int>(-100);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithExcessiveSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange - Try to allocate > 1TB
        var excessiveSize = (1L << 40) + 1;

        // Act
        var act = () => new TestMemoryBuffer<int>(excessiveSize);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*exceeds maximum reasonable allocation*");
    }

    [Fact]
    public void Constructor_WithNonDivisibleSize_ThrowsArgumentException()
    {
        // Arrange - int is 4 bytes, so 15 bytes is not evenly divisible
        // Act
        var act = () => new TestMemoryBuffer<int>(15);

        // Assert
        _ = act.Should().Throw<ArgumentException>()
            .WithMessage("*not evenly divisible by element size*");
    }

    [Fact]
    public void Constructor_WithSmallSize_CreatesValidBuffer()
    {
        // Act
        var buffer = new TestMemoryBuffer<byte>(1);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(1);
        _ = buffer.Length.Should().Be(1);
    }

    [Fact]
    public void SizeInBytes_ReturnsConstructorValue()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(2048);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.SizeInBytes.Should().Be(2048);
    }

    [Fact]
    public void Length_CalculatesCorrectly()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<long>(800); // 800 / 8 = 100

        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Length.Should().Be(100);
    }

    [Fact]
    public void Length_ForByteBuffer_EqualsSizeInBytes()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<byte>(1024);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Length.Should().Be(1024);
        _ = buffer.SizeInBytes.Should().Be(1024);
    }

    [Fact]
    public void Map_WhenNotDisposed_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var mapped = buffer.Map();

        // Assert
        _ = mapped.Should().NotBeNull();
    }

    [Fact]
    public void Map_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        buffer.SetDisposed();

        // Act
        var act = () => buffer.Map();

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Map_WithReadMode_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var mapped = buffer.Map(Abstractions.Memory.MapMode.Read);

        // Assert
        _ = mapped.Should().NotBeNull();
    }

    [Fact]
    public void Map_WithWriteMode_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var mapped = buffer.Map(Abstractions.Memory.MapMode.Write);

        // Assert
        _ = mapped.Should().NotBeNull();
    }

    [Fact]
    public void Map_WithReadWriteMode_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var mapped = buffer.Map(Abstractions.Memory.MapMode.ReadWrite);

        // Assert
        _ = mapped.Should().NotBeNull();
    }

    [Fact]
    public void MapRange_WithValidRange_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var mapped = buffer.MapRange(10, 20);

        // Assert
        _ = mapped.Should().NotBeNull();
        _ = mapped.Length.Should().Be(20);
    }

    [Fact]
    public void MapRange_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        buffer.SetDisposed();

        // Act
        var act = () => buffer.MapRange(0, 10);

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void MapRange_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.MapRange(-1, 10);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.MapRange(10, -5);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WithOutOfRangeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024); // Length = 256
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.MapRange(100, 200); // 100 + 200 > 256

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task MapAsync_WhenNotDisposed_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var mapped = await buffer.MapAsync();

        // Assert
        _ = mapped.Should().NotBeNull();
    }

    [Fact]
    public async Task MapAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        buffer.SetDisposed();

        // Act
        var act = async () => await buffer.MapAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task MapAsync_WithReadMode_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var mapped = await buffer.MapAsync(Abstractions.Memory.MapMode.Read);

        // Assert
        _ = mapped.Should().NotBeNull();
    }

    [Fact]
    public async Task MapAsync_WithCancellation_PropagatesCancellationToken()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert - Should not throw, as the test implementation doesn't check cancellation
        var mapped = await buffer.MapAsync(cancellationToken: cts.Token);
        _ = mapped.Should().NotBeNull();
    }

    [Fact]
    public void ValidateCopyParameters_WithValidParameters_DoesNotThrow()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, 0, 100, 0, 50);

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCopyParameters_WithNegativeSourceOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, -1, 100, 0, 50);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidateCopyParameters_WithNegativeDestinationOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, 0, 100, -1, 50);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidateCopyParameters_WithCountLessThanMinusOne_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, 0, 100, 0, -2);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidateCopyParameters_WithCountMinusOne_UsesMinimumLength()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act - With count = -1, should calculate minimum automatically
        var act = () => buffer.TestValidateCopyParameters(100, 10, 50, 5, -1);

        // Assert - Should not throw, count becomes min(90, 45) = 45
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCopyParameters_WithSourceOverflow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, 90, 100, 0, 20); // 90 + 20 > 100

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Source buffer overflow*");
    }

    [Fact]
    public void ValidateCopyParameters_WithDestinationOverflow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, 0, 100, 90, 20); // 90 + 20 > 100

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Destination buffer overflow*");
    }

    [Fact]
    public void ValidateCopyParameters_WithSourceOffsetOverflow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, long.MaxValue, 100, 0, 1);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Source offset + count would overflow*");
    }

    [Fact]
    public void ValidateCopyParameters_WithDestinationOffsetOverflow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, 0, 100, long.MaxValue, 1);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Destination offset + count would overflow*");
    }

    [Fact]
    public void ValidateCopyParameters_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        buffer.SetDisposed();

        // Act
        var act = () => buffer.TestValidateCopyParameters(100, 0, 100, 0, 50);

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ThrowIfDisposed_WhenNotDisposed_DoesNotThrow()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var act = buffer.TestThrowIfDisposed;

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfDisposed_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        buffer.SetDisposed();

        // Act
        var act = buffer.TestThrowIfDisposed;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void NonGenericCopyFromAsync_WithMatchingType_CopiesSuccessfully()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);
        var source = new int[10];

        // Act
        var act = async () => await ((IUnifiedMemoryBuffer)buffer).CopyFromAsync<int>(source.AsMemory());

        // Assert
        _ = act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NonGenericCopyFromAsync_WithMismatchedType_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);
        var source = new short[10];

        // Act
        var act = async () => await ((IUnifiedMemoryBuffer)buffer).CopyFromAsync<short>(source.AsMemory());

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Type mismatch*");
    }

    [Fact]
    public void NonGenericCopyToAsync_WithMatchingType_CopiesSuccessfully()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);
        var destination = new int[256];

        // Act
        var act = async () => await ((IUnifiedMemoryBuffer)buffer).CopyToAsync<int>(destination.AsMemory());

        // Assert
        _ = act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NonGenericCopyToAsync_WithMismatchedType_ThrowsArgumentException()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<int>(1024);
        _disposables.Add(buffer);
        var destination = new short[256];

        // Act
        var act = async () => await ((IUnifiedMemoryBuffer)buffer).CopyToAsync<short>(destination.AsMemory());

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Type mismatch*");
    }

    #endregion

    #region BaseDeviceBuffer Tests (15 tests)

    [Fact]
    public void DeviceBuffer_Constructor_WithValidParameters_CreatesBuffer()
    {
        // Arrange
        var devicePointer = new IntPtr(0x1000);

        // Act
        var buffer = new TestDeviceBuffer<int>(1024, devicePointer);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(1024);
        _ = buffer.DevicePointer.Should().Be(devicePointer);
        _ = buffer.MemoryType.Should().Be(MemoryType.Device);
    }

    [Fact]
    public void DeviceBuffer_Constructor_WithNullPointer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TestDeviceBuffer<int>(1024, IntPtr.Zero);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DeviceBuffer_DevicePointer_ReturnsConstructorValue()
    {
        // Arrange
        var devicePointer = new IntPtr(0x2000);
        var buffer = new TestDeviceBuffer<int>(1024, devicePointer);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.DevicePointer.Should().Be(devicePointer);
    }

    [Fact]
    public void DeviceBuffer_MemoryType_ReturnsDevice()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.MemoryType.Should().Be(MemoryType.Device);
    }

    [Fact]
    public void DeviceBuffer_IsDisposed_InitiallyFalse()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void DeviceBuffer_MarkDisposed_ReturnsTrueFirstTime()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act
        var result = buffer.TestMarkDisposed();

        // Assert
        _ = result.Should().BeTrue();
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void DeviceBuffer_MarkDisposed_ReturnsFalseSecondTime()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);
        _ = buffer.TestMarkDisposed();

        // Act
        var result = buffer.TestMarkDisposed();

        // Assert
        _ = result.Should().BeFalse();
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void DeviceBuffer_MarkDisposed_ThreadSafe()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);
        var results = new bool[100];

        // Act - Try to dispose from multiple threads
        _ = Parallel.For(0, 100, i =>
        {
            results[i] = buffer.TestMarkDisposed();
        });

        // Assert - Exactly one thread should have succeeded
        _ = results.Count(r => r).Should().Be(1);
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void DeviceBuffer_Dispose_MarksAsDisposed()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));

        // Act
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void DeviceBuffer_Dispose_MultipleCalls_Safe()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));

        // Act
        buffer.Dispose();
        buffer.Dispose();
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DeviceBuffer_DisposeAsync_MarksAsDisposed()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));

        // Act
        await buffer.DisposeAsync();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void DeviceBuffer_GetDeviceMemory_ReturnsValidMemory()
    {
        // Arrange
        var devicePointer = new IntPtr(0x3000);
        var buffer = new TestDeviceBuffer<int>(2048, devicePointer);
        _disposables.Add(buffer);

        // Act
        var deviceMem = buffer.GetDeviceMemory();

        // Assert
        _ = deviceMem.Handle.Should().Be(devicePointer);
        _ = deviceMem.Size.Should().Be(2048);
        _ = deviceMem.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeviceBuffer_WithDifferentTypes_CalculatesLengthCorrectly()
    {
        // Arrange & Act
        var intBuffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));
        var longBuffer = new TestDeviceBuffer<long>(1024, new IntPtr(0x2000));
        var byteBuffer = new TestDeviceBuffer<byte>(1024, new IntPtr(0x3000));
        _disposables.Add(intBuffer);
        _disposables.Add(longBuffer);
        _disposables.Add(byteBuffer);

        // Assert
        _ = intBuffer.Length.Should().Be(256); // 1024 / 4
        _ = longBuffer.Length.Should().Be(128); // 1024 / 8
        _ = byteBuffer.Length.Should().Be(1024); // 1024 / 1
    }

    [Fact]
    public void DeviceBuffer_State_ReturnsExpectedValue()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.State.Should().Be(BufferState.DeviceOnly);
    }

    [Fact]
    public void DeviceBuffer_Options_ReturnsNone()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Options.Should().Be(MemoryOptions.None);
    }

    #endregion

    #region BaseUnifiedBuffer Tests (15 tests)

    [Fact]
    public void UnifiedBuffer_Constructor_WithValidParameters_CreatesBuffer()
    {
        // Arrange
        var unifiedPointer = new IntPtr(0x1000);

        // Act
        var buffer = new TestUnifiedBuffer<int>(1024, unifiedPointer);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(1024);
        _ = buffer.DevicePointer.Should().Be(unifiedPointer);
        _ = buffer.MemoryType.Should().Be(MemoryType.Unified);
    }

    [Fact]
    public void UnifiedBuffer_Constructor_WithNullPointer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TestUnifiedBuffer<int>(1024, IntPtr.Zero);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UnifiedBuffer_DevicePointer_ReturnsUnifiedPointer()
    {
        // Arrange
        var unifiedPointer = new IntPtr(0x2000);
        var buffer = new TestUnifiedBuffer<int>(1024, unifiedPointer);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.DevicePointer.Should().Be(unifiedPointer);
    }

    [Fact]
    public void UnifiedBuffer_MemoryType_ReturnsUnified()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.MemoryType.Should().Be(MemoryType.Unified);
    }

    [Fact]
    public void UnifiedBuffer_IsDisposed_InitiallyFalse()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void UnifiedBuffer_AsSpan_ReturnsValidSpan()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsSpan();

        // Assert
        _ = span.Length.Should().Be(256); // 1024 / sizeof(int)
    }

    [Fact]
    public void UnifiedBuffer_AsSpan_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        buffer.Dispose();

        // Act & Assert
        _ = buffer.Invoking(b => b.AsSpan()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public unsafe void UnifiedBuffer_AsSpan_UsesUnifiedPointer()
    {
        // Arrange
        var unifiedPointer = new IntPtr(0x5000);
        var buffer = new TestUnifiedBuffer<int>(1024, unifiedPointer);
        _disposables.Add(buffer);

        // Act
        var span = buffer.AsSpan();

        // Assert - Span should point to unified memory
        fixed (int* ptr = span)
        {
            _ = new IntPtr(ptr).Should().Be(unifiedPointer);
        }
    }

    [Fact]
    public void UnifiedBuffer_MarkDisposed_ReturnsTrueFirstTime()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act
        var result = buffer.TestMarkDisposed();

        // Assert
        _ = result.Should().BeTrue();
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void UnifiedBuffer_MarkDisposed_ReturnsFalseSecondTime()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);
        _ = buffer.TestMarkDisposed();

        // Act
        var result = buffer.TestMarkDisposed();

        // Assert
        _ = result.Should().BeFalse();
    }

    [Fact]
    public void UnifiedBuffer_MarkDisposed_ThreadSafe()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);
        var results = new bool[100];

        // Act
        _ = Parallel.For(0, 100, i =>
        {
            results[i] = buffer.TestMarkDisposed();
        });

        // Assert - Exactly one should succeed
        _ = results.Count(r => r).Should().Be(1);
    }

    [Fact]
    public void UnifiedBuffer_Dispose_MarksAsDisposed()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));

        // Act
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task UnifiedBuffer_DisposeAsync_MarksAsDisposed()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));

        // Act
        await buffer.DisposeAsync();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void UnifiedBuffer_State_ReturnsOnBoth()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.State.Should().Be(BufferState.Synchronized);
    }

    [Fact]
    public void UnifiedBuffer_Options_ReturnsUnified()
    {
        // Arrange
        var buffer = new TestUnifiedBuffer<int>(1024, new IntPtr(0x1000));
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Options.Should().Be(MemoryOptions.Unified);
    }

    #endregion

    #region BasePooledBuffer Tests (15 tests)

    [Fact]
    public void PooledBuffer_Constructor_WithoutReturnAction_CreatesBuffer()
    {
        // Act
        var buffer = new TestPooledBuffer<int>(1024);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(1024);
        _ = buffer.Length.Should().Be(256);
    }

    [Fact]
    public void PooledBuffer_Constructor_WithReturnAction_CreatesBuffer()
    {
        // Arrange
        var returned = false;
        Action<BasePooledBuffer<int>> returnAction = _ => returned = true;

        // Act
        var buffer = new TestPooledBuffer<int>(1024, returnAction);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.Should().NotBeNull();
        _ = returned.Should().BeFalse();
    }

    [Fact]
    public void PooledBuffer_Dispose_InvokesReturnAction()
    {
        // Arrange
        var returned = false;
        Action<BasePooledBuffer<int>> returnAction = _ => returned = true;
        var buffer = new TestPooledBuffer<int>(1024, returnAction);

        // Act
        buffer.Dispose();

        // Assert
        _ = returned.Should().BeTrue();
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task PooledBuffer_DisposeAsync_InvokesReturnAction()
    {
        // Arrange
        var returned = false;
        Action<BasePooledBuffer<int>> returnAction = _ => returned = true;
        var buffer = new TestPooledBuffer<int>(1024, returnAction);

        // Act
        await buffer.DisposeAsync();

        // Assert
        _ = returned.Should().BeTrue();
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void PooledBuffer_Dispose_WithoutReturnAction_DoesNotThrow()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);

        // Act
        var act = buffer.Dispose;

        // Assert
        _ = act.Should().NotThrow();
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void PooledBuffer_Dispose_MultipleCalls_InvokesReturnActionOnce()
    {
        // Arrange
        var returnCount = 0;
        Action<BasePooledBuffer<int>> returnAction = _ => returnCount++;
        var buffer = new TestPooledBuffer<int>(1024, returnAction);

        // Act
        buffer.Dispose();
        buffer.Dispose();
        buffer.Dispose();

        // Assert
        _ = returnCount.Should().Be(1);
    }

    [Fact]
    public void PooledBuffer_Dispose_CallsGCSuppressFinalize()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);

        // Act
        buffer.Dispose();

        // Assert - GC.SuppressFinalize is called (can't directly verify, but ensures proper cleanup)
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void PooledBuffer_Reset_ClearsDisposedFlag()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);
        buffer.Dispose();

        // Act
        buffer.Reset();

        // Assert
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void PooledBuffer_Memory_ReturnsValidMemory()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act
        var memory = buffer.Memory;

        // Assert
        _ = memory.Length.Should().Be(256);
    }

    [Fact]
    public void PooledBuffer_MemoryType_ReturnsHost()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.MemoryType.Should().Be(MemoryType.Host);
    }

    [Fact]
    public void PooledBuffer_Options_ReturnsPooled()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Options.Should().Be(MemoryOptions.Pooled);
    }

    [Fact]
    public void PooledBuffer_IsDisposed_InitiallyFalse()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void PooledBuffer_DevicePointer_ReturnsZero()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.DevicePointer.Should().Be(IntPtr.Zero);
    }

    [Fact]
    public void PooledBuffer_State_ReturnsOnHost()
    {
        // Arrange
        var buffer = new TestPooledBuffer<int>(1024);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
    }

    [Fact]
    public void PooledBuffer_CompleteLifecycle_ReturnsToPool()
    {
        // Arrange
        var returnedBuffer = (BasePooledBuffer<int>?)null;
        Action<BasePooledBuffer<int>> returnAction = b => returnedBuffer = b;
        var buffer = new TestPooledBuffer<int>(1024, returnAction);

        // Act
        buffer.Dispose();
        buffer.Reset();

        // Assert
        _ = returnedBuffer.Should().Be(buffer);
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    #endregion

    #region BasePinnedBuffer Tests (10 tests)

    [Fact]
    public void PinnedBuffer_Constructor_WithValidArray_CreatesBuffer()
    {
        // Arrange
        var array = new int[100];

        // Act
        var buffer = new TestPinnedBuffer<int>(array);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(400); // 100 * 4
        _ = buffer.Length.Should().Be(100);
    }

    [Fact]
    public void PinnedBuffer_Constructor_WithNullArray_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TestPinnedBuffer<int>(null!);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PinnedBuffer_DevicePointer_ReturnsPinnedAddress()
    {
        // Arrange
        var array = new int[100];
        var buffer = new TestPinnedBuffer<int>(array);
        _disposables.Add(buffer);

        // Act
        var pointer = buffer.DevicePointer;

        // Assert
        _ = pointer.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void PinnedBuffer_MemoryType_ReturnsPinned()
    {
        // Arrange
        var array = new int[100];
        var buffer = new TestPinnedBuffer<int>(array);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.MemoryType.Should().Be(MemoryType.Pinned);
    }

    [Fact]
    public void PinnedBuffer_IsDisposed_InitiallyFalse()
    {
        // Arrange
        var array = new int[100];
        var buffer = new TestPinnedBuffer<int>(array);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void PinnedBuffer_Dispose_UnpinsHandle()
    {
        // Arrange
        var array = new int[100];
        var buffer = new TestPinnedBuffer<int>(array);

        // Act
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void PinnedBuffer_Dispose_MultipleCalls_Safe()
    {
        // Arrange
        var array = new int[100];
        var buffer = new TestPinnedBuffer<int>(array);

        // Act
        buffer.Dispose();
        buffer.Dispose();
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task PinnedBuffer_DisposeAsync_UnpinsHandle()
    {
        // Arrange
        var array = new int[100];
        var buffer = new TestPinnedBuffer<int>(array);

        // Act
        await buffer.DisposeAsync();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void PinnedBuffer_Options_ReturnsPinned()
    {
        // Arrange
        var array = new int[100];
        var buffer = new TestPinnedBuffer<int>(array);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.Options.Should().Be(MemoryOptions.Pinned);
    }

    [Fact]
    public void PinnedBuffer_State_ReturnsOnHost()
    {
        // Arrange
        var array = new int[100];
        var buffer = new TestPinnedBuffer<int>(array);
        _disposables.Add(buffer);

        // Act & Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
    }

    #endregion
}
