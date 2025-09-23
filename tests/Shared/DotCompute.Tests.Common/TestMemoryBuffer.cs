// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;

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

    #region IUnifiedMemoryBuffer<T> Implementation

    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    public int Length => _length;
    public IAccelerator Accelerator => TestAccelerator.Instance;
    public bool IsOnHost => _state == BufferState.HostReady || _state == BufferState.Synchronized || _state == BufferState.HostDirty;
    public bool IsOnDevice => _state == BufferState.DeviceReady || _state == BufferState.Synchronized || _state == BufferState.DeviceDirty;
    public bool IsDirty => _state == BufferState.HostDirty || _state == BufferState.DeviceDirty;
    public MemoryOptions Options => _options.MemoryOptions;
    public bool IsDisposed => _disposed;
    public BufferState State => _state;

    // Additional test-specific properties for compatibility
    public MemoryType MemoryType => MemoryType.Host;
    public IntPtr DevicePointer => _options.PinMemory ? _pinnedHandle.AddrOfPinnedObject() : IntPtr.Zero;

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2000:Dispose objects before losing scope", Justification = "Map method returns a disposable object for caller to manage")]
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Map(mode));
    }

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

    public void MarkHostDirty()
    {
        ThrowIfDisposed();
        _state = BufferState.HostDirty;
    }

    public void MarkDeviceDirty()
    {
        ThrowIfDisposed();
        _state = BufferState.DeviceDirty;
    }

    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

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

    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementOperationCount();

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

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ThrowIfDisposed();
        ValidateRange(offset, length);
        IncrementOperationCount();

        return new TestMemoryBufferSlice<T>(this, offset, length);
    }

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
    public void ResetOperationCount()
    {
        _operationCount = 0;
    }

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
        Interlocked.Increment(ref _operationCount);

        if (_options.FailAfterOperations > 0 && _operationCount >= _options.FailAfterOperations)
        {
            throw new InvalidOperationException($"Test buffer configured to fail after {_options.FailAfterOperations} operations");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

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

    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    public int Length => _length;
    public IAccelerator Accelerator => _parent.Accelerator;
    public bool IsOnHost => _parent.IsOnHost;
    public bool IsOnDevice => _parent.IsOnDevice;
    public bool IsDirty => _parent.IsDirty;
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;
    public BufferState State => _parent.State;

    public Span<T> AsSpan() => _parent.AsSpan().Slice(_offset, _length);
    public ReadOnlySpan<T> AsReadOnlySpan() => _parent.AsReadOnlySpan().Slice(_offset, _length);
    public Memory<T> AsMemory() => _parent.AsMemory().Slice(_offset, _length);
    public ReadOnlyMemory<T> AsReadOnlyMemory() => _parent.AsReadOnlyMemory().Slice(_offset, _length);

    public DeviceMemory GetDeviceMemory()
    {
        var parentDeviceMemory = _parent.GetDeviceMemory();
        var offsetBytes = _offset * Unsafe.SizeOf<T>();
        return new DeviceMemory(parentDeviceMemory.Handle + offsetBytes, SizeInBytes);
    }

    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => _parent.MapRange(_offset, _length, mode);
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => _parent.MapRange(_offset + offset, length, mode);
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => _parent.MapAsync(mode, cancellationToken);

    public void EnsureOnHost() => _parent.EnsureOnHost();
    public void EnsureOnDevice() => _parent.EnsureOnDevice();
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.EnsureOnHostAsync(context, cancellationToken);
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.EnsureOnDeviceAsync(context, cancellationToken);
    public void Synchronize() => _parent.Synchronize();
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.SynchronizeAsync(context, cancellationToken);
    public void MarkHostDirty() => _parent.MarkHostDirty();
    public void MarkDeviceDirty() => _parent.MarkDeviceDirty();

    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        if (source.Length > _length)
        {
            throw new ArgumentException("Source data is larger than slice capacity");
        }

        return _parent.CopyToAsync(0, _parent, _offset, source.Length, cancellationToken);
    }

    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        var sliceMemory = AsMemory();
        sliceMemory.CopyTo(destination);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(_offset, destination, 0, _length, cancellationToken);

    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(_offset + sourceOffset, destination, destinationOffset, count, cancellationToken);

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        => _parent.FillAsync(value, _offset, _length, cancellationToken);

    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        => _parent.FillAsync(value, _offset + offset, count, cancellationToken);

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset and length are out of range");
        }

        return new TestMemoryBufferSlice<T>(_parent, _offset + offset, length);
    }

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

    public void Dispose() { /* Slices don't own the parent buffer */ }
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

    public TestPooledBuffer(int sizeInElements, Action<BasePooledBuffer<T>>? returnAction)
        : base(sizeInElements * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), returnAction)
    {
        _memory = new T[sizeInElements];
        _memoryType = MemoryType.Host;
    }

    public TestPooledBuffer(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction)
        : base(sizeInBytes, returnAction)
    {
        var elementCount = (int)(sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        _memory = new T[elementCount];
        _memoryType = MemoryType.Host;
    }

    public override IntPtr DevicePointer => IntPtr.Zero;
    public override MemoryType MemoryType => _memoryType;
    public override bool IsOnHost => true;
    public override bool IsOnDevice => false;
    public override bool IsDirty => false;
    public override bool IsDisposed => _isDisposed;
    public override Memory<T> Memory => _memory;

    public override Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory.Span;
    }

    public override ReadOnlySpan<T> AsReadOnlySpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory.Span;
    }

    public override Memory<T> AsMemory()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory;
    }

    public override ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory;
    }

    public override DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);

    public override MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => new(_memory, null);

    public override MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
        => new(_memory.Slice(offset, length), null);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2000:Dispose objects before losing scope", Justification = "MappedMemory is returned to caller for disposal")]
    public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new MappedMemory<T>(_memory, null));

    public override void EnsureOnHost() { }
    public override void EnsureOnDevice() { }

    public override ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override void Synchronize() { }

    public override ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override void MarkHostDirty() { }
    public override void MarkDeviceDirty() { }

    // Implement the required abstract methods from BaseMemoryBuffer
    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_memory);
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_memory.Slice((int)offset));
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        _memory.CopyTo(destination);
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        _memory.Slice((int)offset).CopyTo(destination);
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        return destination.CopyFromAsync(_memory, cancellationToken);
    }

    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        var sourceData = _memory.Slice(sourceOffset, count);
        return destination.CopyFromAsync<T>(sourceData, destinationOffset, cancellationToken);
    }

    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        _memory.Span.Fill(value);
        return ValueTask.CompletedTask;
    }

    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        _memory.Span.Slice(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);

        return new TestPooledBuffer<T>(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), null);
    }

    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();

    public override IAccelerator Accelerator => TestAccelerator.Instance;
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    public override MemoryOptions Options => MemoryOptions.None;

    protected override void DisposeCore()
    {
        _isDisposed = true;
        base.DisposeCore();
    }
}