// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;
using AcceleratorContext = DotCompute.Abstractions.AcceleratorContext;
using MapMode = DotCompute.Abstractions.Memory.MapMode;

namespace DotCompute.Tests.Common;

/// <summary>
/// CONSOLIDATED test memory buffer implementation.
/// This is the SINGLE test buffer implementation for all tests across the DotCompute solution.
///
/// Replaces all previous test buffer implementations:
/// - TestMemoryBuffers.cs (multiple implementations)
/// - Mock buffer implementations
/// - Duplicate test helpers
///
/// Features:
/// - Full IUnifiedMemoryBuffer{T} compatibility
/// - Configurable behavior for testing edge cases
/// - Deterministic failure simulation
/// - Memory tracking and validation
/// - Cross-platform test support
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class TestMemoryBuffer<T> : IUnifiedMemoryBuffer<T>, IDisposable
    where T : unmanaged
{
    private readonly TestMemoryBufferOptions _options;
    private readonly int _length;
    private readonly T[] _data;
    private readonly GCHandle _pinnedHandle;
    private BufferState _state;
    private bool _disposed;
    private int _operationCount;

    /// <summary>
    /// Creates a new test memory buffer with default options.
    /// </summary>
    /// <param name="length">The number of elements in the buffer.</param>
    public TestMemoryBuffer(int length)
        : this(length, TestMemoryBufferOptions.Default)
    {
    }

    /// <summary>
    /// Creates a new test memory buffer with specific options.
    /// </summary>
    /// <param name="length">The number of elements in the buffer.</param>
    /// <param name="options">Test-specific options for controlling behavior.</param>
    public TestMemoryBuffer(int length, TestMemoryBufferOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentNullException.ThrowIfNull(options);

        _length = length;
        _options = options;
        _data = new T[length];
        _state = BufferState.HostReady;

        // Pin memory if requested for testing pinned memory scenarios
        if (options.PinMemory)
        {
            _pinnedHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        }

        // Initialize with test pattern if requested
        if (options.InitializeWithPattern)
        {
            InitializeTestPattern();
        }
    }

    /// <summary>
    /// Creates a test memory buffer from existing data.
    /// </summary>
    /// <param name="data">The initial data for the buffer.</param>
    /// <param name="options">Test-specific options.</param>
    public TestMemoryBuffer(ReadOnlySpan<T> data, TestMemoryBufferOptions? options = null)
        : this(data.Length, options ?? TestMemoryBufferOptions.Default)
    {
        data.CopyTo(_data);
    }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>

    #region IUnifiedMemoryBuffer<T> Implementation

    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>
    public int Length => _length;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => TestAccelerator.Instance;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => _state is BufferState.HostReady or BufferState.Synchronized or BufferState.HostDirty;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => _state is BufferState.DeviceReady or BufferState.Synchronized or BufferState.DeviceDirty;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public bool IsDirty => _state is BufferState.HostDirty or BufferState.DeviceDirty;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _options.MemoryOptions;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _disposed;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _state;
    /// <summary>
    /// Gets or sets the memory type.
    /// </summary>
    /// <value>The memory type.</value>

    // Additional test-specific properties for compatibility
    public MemoryType MemoryType => MemoryType.Host;
    /// <summary>
    /// Gets or sets the device pointer.
    /// </summary>
    /// <value>The device pointer.</value>
    public IntPtr DevicePointer => _options.PinMemory ? _pinnedHandle.AddrOfPinnedObject() : IntPtr.Zero;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Span<T> AsSpan()
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.ThrowOnSpanAccess)
        {
            throw new InvalidOperationException("Span access disabled for this test buffer");
        }

        return _data.AsSpan();
    }
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.ThrowOnSpanAccess)
        {
            throw new InvalidOperationException("Span access disabled for this test buffer");
        }

        return _data.AsSpan();
    }
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Memory<T> AsMemory()
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.ThrowOnMemoryAccess)
        {
            throw new InvalidOperationException("Memory access disabled for this test buffer");
        }

        return _data.AsMemory();
    }
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.ThrowOnMemoryAccess)
        {
            throw new InvalidOperationException("Memory access disabled for this test buffer");
        }

        return _data.AsMemory();
    }
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>

    public DeviceMemory GetDeviceMemory()
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (!_options.PinMemory)
        {
            throw new InvalidOperationException("Device memory requires pinned memory for test buffer");
        }

        return new DeviceMemory(_pinnedHandle.AddrOfPinnedObject(), SizeInBytes);
    }
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        return new MappedMemory<T>(AsMemory(), () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        ThrowIfDisposed();
        ValidateRange(offset, length);
        IncrementOperationCount();

        return new MappedMemory<T>(AsMemory().Slice(offset, length), () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2000:Dispose objects before losing scope", Justification = "Map method returns a disposable object for caller to manage")]
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));
    /// <summary>
    /// Performs ensure on host.
    /// </summary>

    public void EnsureOnHost()
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.SimulateSlowTransfers)
        {
            Thread.Sleep(_options.TransferDelayMs);
        }

        _state = BufferState.HostReady;
    }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>

    public void EnsureOnDevice()
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.SimulateSlowTransfers)
        {
            Thread.Sleep(_options.TransferDelayMs);
        }

        _state = BufferState.DeviceReady;
    }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.SimulateSlowTransfers)
        {
            await Task.Delay(_options.TransferDelayMs, cancellationToken);
        }

        _state = BufferState.HostReady;
    }
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.SimulateSlowTransfers)
        {
            await Task.Delay(_options.TransferDelayMs, cancellationToken);
        }

        _state = BufferState.DeviceReady;
    }
    /// <summary>
    /// Performs synchronize.
    /// </summary>

    public void Synchronize()
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.SimulateSlowTransfers)
        {
            Thread.Sleep(_options.TransferDelayMs);
        }

        _state = BufferState.Synchronized;
    }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.SimulateSlowTransfers)
        {
            await Task.Delay(_options.TransferDelayMs, cancellationToken);
        }

        _state = BufferState.Synchronized;
    }
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>

    public void MarkHostDirty()
    {
        ThrowIfDisposed();
        _state = BufferState.HostDirty;
    }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>

    public void MarkDeviceDirty()
    {
        ThrowIfDisposed();
        _state = BufferState.DeviceDirty;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        // Check for cancellation
        cancellationToken.ThrowIfCancellationRequested();

        if (source.Length > _length)
        {
            throw new ArgumentException("Source data is larger than buffer capacity");
        }

        if (_options.ThrowOnCopyOperations)
        {
            throw new InvalidOperationException("Copy operations disabled for this test buffer");
        }

        if (_options.SimulateSlowTransfers)
        {
            await Task.Delay(_options.TransferDelayMs, cancellationToken);
        }

        source.Span.CopyTo(_data);
        MarkHostDirty();
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        // Check for cancellation
        cancellationToken.ThrowIfCancellationRequested();

        if (destination.Length < _length)
        {
            throw new ArgumentException("Destination buffer is too small");
        }

        if (_options.ThrowOnCopyOperations)
        {
            throw new InvalidOperationException("Copy operations disabled for this test buffer");
        }

        if (_options.SimulateSlowTransfers)
        {
            await Task.Delay(_options.TransferDelayMs, cancellationToken);
        }

        _data.AsSpan().CopyTo(destination.Span);
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        IncrementOperationCount();

        if (destination.Length < _length)
        {
            throw new ArgumentException("Destination buffer is too small");
        }

        if (_options.ThrowOnCopyOperations)
        {
            throw new InvalidOperationException("Copy operations disabled for this test buffer");
        }

        var memory = new Memory<T>(new T[_length]);
        await CopyToAsync(memory, cancellationToken);
        await destination.CopyFromAsync(memory, cancellationToken);
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        ValidateRange(sourceOffset, count);
        IncrementOperationCount();

        if (destinationOffset < 0 || destinationOffset + count > destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationOffset));
        }

        if (_options.ThrowOnCopyOperations)
        {
            throw new InvalidOperationException("Copy operations disabled for this test buffer");
        }

        var sourceData = new Memory<T>(_data, sourceOffset, count);
        var destSlice = destination.Slice(destinationOffset, count);
        await destSlice.CopyFromAsync(sourceData, cancellationToken);
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        if (_options.SimulateSlowTransfers)
        {
            await Task.Delay(_options.TransferDelayMs, cancellationToken);
        }

        Array.Fill(_data, value);
        MarkHostDirty();
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateRange(offset, count);
        IncrementOperationCount();

        if (_options.SimulateSlowTransfers)
        {
            await Task.Delay(_options.TransferDelayMs, cancellationToken);
        }

        Array.Fill(_data, value, offset, count);
        MarkHostDirty();
    }
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ThrowIfDisposed();
        ValidateRange(offset, length);
        IncrementOperationCount();

        return new TestMemoryBufferSlice<T>(this, offset, length);
    }
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        ThrowIfDisposed();
        IncrementOperationCount();

        var newElementSize = Unsafe.SizeOf<TNew>();
        if (SizeInBytes % newElementSize != 0)
        {
            throw new InvalidOperationException($"Buffer size not compatible with element size of {typeof(TNew).Name}");
        }

        var bytes = MemoryMarshal.AsBytes(_data.AsSpan());
        var newData = MemoryMarshal.Cast<byte, TNew>(bytes);

        return new TestMemoryBuffer<TNew>(newData, _options);
    }

    #endregion

    #region Non-generic Interface Implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var sourceBytes = MemoryMarshal.AsBytes(source.Span);
        var targetBytes = MemoryMarshal.AsBytes(_data.AsSpan())[(int)offset..];

        if (sourceBytes.Length > targetBytes.Length)
        {
            throw new ArgumentException("Source data would exceed buffer capacity");
        }

        sourceBytes.CopyTo(targetBytes);
        MarkHostDirty();

        return ValueTask.CompletedTask;
    }

    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var sourceBytes = MemoryMarshal.AsBytes(_data.AsSpan())[(int)offset..];
        var destBytes = MemoryMarshal.AsBytes(destination.Span);

        if (sourceBytes.Length > destBytes.Length)
        {
            throw new ArgumentException("Destination buffer is too small");
        }

        sourceBytes.CopyTo(destBytes);

        return ValueTask.CompletedTask;
    }

    #endregion

    #region Test-Specific Methods

    /// <summary>
    /// Gets the number of operations performed on this buffer.
    /// Useful for testing performance and usage patterns.
    /// </summary>
    public int OperationCount => _operationCount;

    /// <summary>
    /// Gets the test options for this buffer.
    /// </summary>
    public TestMemoryBufferOptions TestOptions => _options;

    /// <summary>
    /// Gets the underlying test data array (for test verification).
    /// </summary>
    public ReadOnlySpan<T> GetTestData() => _data.AsSpan();

    /// <summary>
    /// Test helper method to validate copy parameters.
    /// Exposed for testing boundary conditions.
    /// </summary>
    public void TestValidateCopyParameters(int sourceLength, int sourceOffset, int destLength, int destOffset, int count)
    {
        if (sourceOffset < 0 || sourceOffset >= sourceLength)
            throw new ArgumentOutOfRangeException(nameof(sourceOffset));
        if (destOffset < 0 || destOffset >= destLength)
            throw new ArgumentOutOfRangeException(nameof(destOffset));
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (sourceOffset + count > sourceLength)
            throw new ArgumentException("Source range exceeds buffer bounds");
        if (destOffset + count > destLength)
            throw new ArgumentException("Destination range exceeds buffer bounds");
    }

    /// <summary>
    /// Test helper method to throw if disposed.
    /// Exposed for testing disposal validation.
    /// </summary>
    public void TestThrowIfDisposed() => ThrowIfDisposed();

    /// <summary>
    /// Resets the operation counter.
    /// </summary>
    public void ResetOperationCount() => _operationCount = 0;

    /// <summary>
    /// Simulates a memory corruption for testing error handling.
    /// </summary>
    public void SimulateCorruption()
    {
        if (_options.AllowCorruption)
        {
            // Fill with random data to simulate corruption
            var random = new Random();
            var bytes = MemoryMarshal.AsBytes(_data.AsSpan());
            random.NextBytes(bytes);
            _state = BufferState.Disposed; // Use Disposed instead of non-existent Corrupted
        }
    }

    #endregion

    #region Private Methods

    private void InitializeTestPattern()
    {
        for (var i = 0; i < _length; i++)
        {
            if (typeof(T) == typeof(int))
            {
                _data[i] = Unsafe.As<int, T>(ref i);
            }
            else if (typeof(T) == typeof(float))
            {
                var value = (float)i;
                _data[i] = Unsafe.As<float, T>(ref value);
            }
            else if (typeof(T) == typeof(double))
            {
                var value = (double)i;
                _data[i] = Unsafe.As<double, T>(ref value);
            }
            else
            {
                // For other types, use byte pattern
                var bytes = MemoryMarshal.AsBytes(_data.AsSpan(i, 1));
                for (var b = 0; b < bytes.Length; b++)
                {
                    bytes[b] = (byte)(i % 256);
                }
            }
        }
    }

    private void ValidateRange(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(length),
                $"Range [{offset}..{offset + length}) exceeds buffer length {_length}");
        }
    }

    private void IncrementOperationCount()
    {
        _ = Interlocked.Increment(ref _operationCount);

        if (_options.FailAfterOperations > 0 && _operationCount >= _options.FailAfterOperations)
        {
            throw new InvalidOperationException($"Test buffer configured to fail after {_options.FailAfterOperations} operations");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _state = BufferState.Disposed;

            if (_pinnedHandle.IsAllocated)
            {
                _pinnedHandle.Free();
            }
        }
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion
}

/// <summary>
/// Represents a slice/view of a test memory buffer.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class TestMemoryBufferSlice<T> : IUnifiedMemoryBuffer<T>, IDisposable
    where T : unmanaged
{
    private readonly TestMemoryBuffer<T> _parent;
    private readonly int _offset;
    private readonly int _length;

    internal TestMemoryBufferSlice(TestMemoryBuffer<T> parent, int offset, int length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        _length = length;
    }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>

    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>
    public int Length => _length;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => _parent.Accelerator;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => _parent.IsOnHost;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => _parent.IsOnDevice;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public bool IsDirty => _parent.IsDirty;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _parent.Options;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _parent.IsDisposed;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _parent.State;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Span<T> AsSpan() => _parent.AsSpan().Slice(_offset, _length);
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan() => _parent.AsReadOnlySpan().Slice(_offset, _length);
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public Memory<T> AsMemory() => _parent.AsMemory().Slice(_offset, _length);
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlyMemory<T> AsReadOnlyMemory() => _parent.AsReadOnlyMemory().Slice(_offset, _length);
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>

    public DeviceMemory GetDeviceMemory()
    {
        var parentDeviceMemory = _parent.GetDeviceMemory();
        var offsetBytes = _offset * Unsafe.SizeOf<T>();
        return new DeviceMemory(parentDeviceMemory.Handle + offsetBytes, SizeInBytes);
    }
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => _parent.MapRange(_offset, _length, mode);
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => _parent.MapRange(_offset + offset, length, mode);
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => _parent.MapAsync(mode, cancellationToken);
    /// <summary>
    /// Performs ensure on host.
    /// </summary>

    public void EnsureOnHost() => _parent.EnsureOnHost();
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public void EnsureOnDevice() => _parent.EnsureOnDevice();
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.EnsureOnHostAsync(context, cancellationToken);
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.EnsureOnDeviceAsync(context, cancellationToken);
    /// <summary>
    /// Performs synchronize.
    /// </summary>
    public void Synchronize() => _parent.Synchronize();
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.SynchronizeAsync(context, cancellationToken);
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>
    public void MarkHostDirty() => _parent.MarkHostDirty();
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public void MarkDeviceDirty() => _parent.MarkDeviceDirty();
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        if (source.Length > _length)
        {
            throw new ArgumentException("Source data is larger than slice capacity");
        }

        // Copy source data into parent buffer at slice's offset
        source.CopyTo(_parent.AsMemory().Slice(_offset, source.Length));
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        var sliceMemory = AsMemory();
        sliceMemory.CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(_offset, destination, 0, _length, cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(_offset + sourceOffset, destination, destinationOffset, count, cancellationToken);
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        => _parent.FillAsync(value, _offset, _length, cancellationToken);
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        => _parent.FillAsync(value, _offset + offset, count, cancellationToken);
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset and length are out of range");
        }

        return new TestMemoryBufferSlice<T>(_parent, _offset + offset, length);
    }
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        var newElementSize = Unsafe.SizeOf<TNew>();
        if (SizeInBytes % newElementSize != 0)
        {
            throw new InvalidOperationException($"Slice size not compatible with element size of {typeof(TNew).Name}");
        }

        var elementSize = Unsafe.SizeOf<T>();
        var byteOffset = _offset * elementSize;
        var newLength = (int)(SizeInBytes / newElementSize);

        if (byteOffset % newElementSize != 0)
        {
            throw new InvalidOperationException($"Cannot convert slice to type {typeof(TNew).Name} due to alignment constraints");
        }

        var parentAsNew = _parent.AsType<TNew>();
        var newOffset = byteOffset / newElementSize;
        return new TestMemoryBufferSlice<TNew>((TestMemoryBuffer<TNew>)parentAsNew, newOffset, newLength);
    }

    // Non-generic interface implementation
    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken)
        => ((IUnifiedMemoryBuffer)_parent).CopyFromAsync(source, offset, cancellationToken);

    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken)
        => ((IUnifiedMemoryBuffer)_parent).CopyToAsync(destination, offset, cancellationToken);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose() { /* Slices don't own the parent buffer */ }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Configuration options for test memory buffers.
/// </summary>
public sealed class TestMemoryBufferOptions
{
    /// <summary>
    /// Default test options with standard behavior.
    /// </summary>
    public static readonly TestMemoryBufferOptions Default = new();

    /// <summary>
    /// Test options that simulate slow transfers.
    /// </summary>
    public static readonly TestMemoryBufferOptions SlowTransfers = new()
    {
        SimulateSlowTransfers = true,
        TransferDelayMs = 10
    };

    /// <summary>
    /// Test options that disable most operations for error testing.
    /// </summary>
    public static readonly TestMemoryBufferOptions Restrictive = new()
    {
        ThrowOnSpanAccess = true,
        ThrowOnMemoryAccess = true,
        ThrowOnCopyOperations = true
    };

    /// <summary>
    /// Test options that simulate failures after a certain number of operations.
    /// </summary>
    public static TestMemoryBufferOptions FailAfter(int operationCount) => new()
    {
        FailAfterOperations = operationCount
    };

    /// <summary>
    /// Memory options to use for the buffer.
    /// </summary>
    public MemoryOptions MemoryOptions { get; set; } = MemoryOptions.None;

    /// <summary>
    /// Whether to pin the memory for device access testing.
    /// </summary>
    public bool PinMemory { get; set; } = true;

    /// <summary>
    /// Whether to initialize the buffer with a test pattern.
    /// </summary>
    public bool InitializeWithPattern { get; set; }

    /// <summary>
    /// Whether to simulate slow transfer operations.
    /// </summary>
    public bool SimulateSlowTransfers { get; set; }

    /// <summary>
    /// Delay in milliseconds for transfer operations when simulating slow transfers.
    /// </summary>
    public int TransferDelayMs { get; set; } = 5;

    /// <summary>
    /// Whether to throw exceptions when accessing spans.
    /// </summary>
    public bool ThrowOnSpanAccess { get; set; }

    /// <summary>
    /// Whether to throw exceptions when accessing memory.
    /// </summary>
    public bool ThrowOnMemoryAccess { get; set; }

    /// <summary>
    /// Whether to throw exceptions on copy operations.
    /// </summary>
    public bool ThrowOnCopyOperations { get; set; }

    /// <summary>
    /// Number of operations after which the buffer should start failing.
    /// Set to 0 or negative to disable.
    /// </summary>
    public int FailAfterOperations { get; set; } = -1;

    /// <summary>
    /// Whether to allow corruption simulation for testing error handling.
    /// </summary>
    public bool AllowCorruption { get; set; }
}

/// <summary>
/// Mock accelerator for testing purposes.
/// This uses the ConsolidatedMockAccelerator for consistency.
/// </summary>
public sealed class TestAccelerator
{
    public static readonly IAccelerator Instance = Mocks.ConsolidatedMockAccelerator.CreateCpuMock();
}

/// <summary>
/// Test pooled buffer implementation that extends BasePooledBuffer.
/// Used for testing memory pooling scenarios.
/// </summary>
public sealed class TestPooledBuffer<T> : BasePooledBuffer<T> where T : unmanaged
{
    private readonly Memory<T> _memory;
    private readonly MemoryType _memoryType;
    private bool _isDisposed;
    /// <summary>
    /// Initializes a new instance of the TestPooledBuffer class.
    /// </summary>
    /// <param name="sizeInElements">The size in elements.</param>
    /// <param name="returnAction">The return action.</param>

    public TestPooledBuffer(int sizeInElements, Action<BasePooledBuffer<T>>? returnAction)
        : base(sizeInElements * Unsafe.SizeOf<T>(), returnAction)
    {
        _memory = new T[sizeInElements];
        _memoryType = MemoryType.Host;
    }
    /// <summary>
    /// Initializes a new instance of the TestPooledBuffer class.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="returnAction">The return action.</param>

    public TestPooledBuffer(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction)
        : base(sizeInBytes, returnAction)
    {
        var elementCount = (int)(sizeInBytes / Unsafe.SizeOf<T>());
        _memory = new T[elementCount];
        _memoryType = MemoryType.Host;
    }
    /// <summary>
    /// Gets or sets the device pointer.
    /// </summary>
    /// <value>The device pointer.</value>

    public override IntPtr DevicePointer => IntPtr.Zero;
    /// <summary>
    /// Gets or sets the memory type.
    /// </summary>
    /// <value>The memory type.</value>
    public override MemoryType MemoryType => _memoryType;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public override bool IsOnHost => true;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public override bool IsOnDevice => false;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public override bool IsDirty => false;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public override bool IsDisposed => _isDisposed;
    /// <summary>
    /// Gets or sets the memory.
    /// </summary>
    /// <value>The memory.</value>
    public override Memory<T> Memory => _memory;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory.Span;
    }
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override ReadOnlySpan<T> AsReadOnlySpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory.Span;
    }
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override Memory<T> AsMemory()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory;
    }
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory;
    }
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>

    public override DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public override MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => new(_memory, null);
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public override MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
        => new(_memory.Slice(offset, length), null);
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2000:Dispose objects before losing scope", Justification = "MappedMemory is returned to caller for disposal")]
    public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new MappedMemory<T>(_memory, null));
    /// <summary>
    /// Performs ensure on host.
    /// </summary>

    public override void EnsureOnHost() { }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public override void EnsureOnDevice() { }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs synchronize.
    /// </summary>

    public override void Synchronize() { }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>

    public override void MarkHostDirty() { }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public override void MarkDeviceDirty() { }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Implement the required abstract methods from BaseMemoryBuffer
    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_memory);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_memory.Slice((int)offset));
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        _memory.CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        _memory.Slice((int)offset).CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => destination.CopyFromAsync(_memory, cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        var sourceData = _memory.Slice(sourceOffset, count);
        return destination.CopyFromAsync<T>(sourceData, destinationOffset, cancellationToken);
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        _memory.Span.Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        _memory.Span.Slice(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>


    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);

        return new TestPooledBuffer<T>(length * Unsafe.SizeOf<T>(), null);
    }
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>

    public override IAccelerator Accelerator => TestAccelerator.Instance;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public override MemoryOptions Options => MemoryOptions.None;

    /// <summary>
    /// Resets the buffer for reuse in the pool.
    /// </summary>
    public override void Reset()
    {
        _isDisposed = false;
        base.Reset();
    }

    protected override void DisposeCore()
    {
        _isDisposed = true;
        base.DisposeCore();
    }
}
