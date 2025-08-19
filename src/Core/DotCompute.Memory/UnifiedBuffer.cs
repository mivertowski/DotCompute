// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;

namespace DotCompute.Memory;


/// <summary>
/// A unified buffer that provides seamless host/device memory management with lazy transfer semantics.
/// Uses pinned memory allocation for zero-copy operations and GCHandle for AOT compatibility.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class UnifiedBuffer<T> : IMemoryBuffer<T>, IBuffer<T> where T : unmanaged
{
private readonly IMemoryManager _memoryManager;
private readonly SemaphoreSlim _asyncLock = new(1, 1);
private readonly Lock _lock = new();

private GCHandle _pinnedHandle;
private T[]? _hostArray;
private DeviceMemory _deviceMemory;
private IMemoryBuffer? _deviceBuffer;
private BufferState _state;
private volatile bool _disposed;

/// <summary>
/// Gets the length of the buffer in elements.
/// </summary>
public int Length { get; }

/// <summary>
/// Gets the size of the buffer in bytes.
/// </summary>
public long SizeInBytes { get; }

/// <summary>
/// Gets whether the buffer is currently available on the host.
/// </summary>
public bool IsOnHost => _state is BufferState.HostOnly or BufferState.Synchronized or BufferState.HostDirty;

/// <summary>
/// Gets whether the buffer is currently available on the device.
/// </summary>
public bool IsOnDevice => _state is BufferState.DeviceOnly or BufferState.Synchronized or BufferState.DeviceDirty;

/// <summary>
/// Gets whether the buffer has been modified and needs synchronization.
/// </summary>
public bool IsDirty => _state is BufferState.HostDirty or BufferState.DeviceDirty;

/// <summary>
/// Gets the accelerator this buffer is associated with (always null for unified buffers).
/// </summary>
public IAccelerator Accelerator => null!;

/// <summary>
/// Gets the memory options for this buffer.
/// </summary>
public DotCompute.Abstractions.MemoryOptions Options => DotCompute.Abstractions.MemoryOptions.None;

/// <summary>
/// Gets the buffer state for tracking transfers.
/// </summary>
public BufferState State => _state;

/// <summary>
/// Gets whether this buffer has been disposed.
/// </summary>
public bool IsDisposed => _disposed;

/// <summary>
/// Initializes a new instance of the UnifiedBuffer class.
/// </summary>
/// <param name="memoryManager">The memory manager to use for device operations.</param>
/// <param name="length">The length of the buffer in elements.</param>
public UnifiedBuffer(IMemoryManager memoryManager, int length)
{
    ArgumentNullException.ThrowIfNull(memoryManager);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

    _memoryManager = memoryManager;
    Length = length;
    SizeInBytes = length * Unsafe.SizeOf<T>();
    _state = BufferState.Uninitialized;
    _deviceMemory = DeviceMemory.Invalid;

    // Allocate pinned host memory for zero-copy operations
    AllocatePinnedHost();

    // After allocation, the buffer is on host
    _state = BufferState.HostOnly;
}

/// <summary>
/// Initializes a new instance of the UnifiedBuffer class with initial data.
/// </summary>
/// <param name="memoryManager">The memory manager to use for device operations.</param>
/// <param name="data">The initial data to populate the buffer with.</param>
public UnifiedBuffer(IMemoryManager memoryManager, ReadOnlySpan<T> data)
    : this(memoryManager, data.Length)
{
    data.CopyTo(AsSpan());
    _state = BufferState.HostOnly;
}

/// <summary>
/// Gets a span to the host memory. Will trigger transfer if needed.
/// </summary>
/// <returns>A span to the host memory.</returns>
public Span<T> AsSpan()
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    EnsureOnHost();
    return new Span<T>(_hostArray, 0, Length);
}

/// <summary>
/// Gets a read-only span to the host memory. Will trigger transfer if needed.
/// </summary>
/// <returns>A read-only span to the host memory.</returns>
public ReadOnlySpan<T> AsReadOnlySpan()
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    EnsureOnHost();
    return new ReadOnlySpan<T>(_hostArray, 0, Length);
}

/// <summary>
/// Gets a memory handle to the host memory. Will trigger transfer if needed.
/// </summary>
/// <returns>A memory handle to the host memory.</returns>
public Memory<T> AsMemory()
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    EnsureOnHost();
    return new Memory<T>(_hostArray, 0, Length);
}

/// <summary>
/// Gets a read-only memory handle to the host memory. Will trigger transfer if needed.
/// </summary>
/// <returns>A read-only memory handle to the host memory.</returns>
public ReadOnlyMemory<T> AsReadOnlyMemory()
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    EnsureOnHost();
    return new ReadOnlyMemory<T>(_hostArray, 0, Length);
}

/// <summary>
/// Gets the device memory handle. Will trigger transfer if needed.
/// </summary>
/// <returns>The device memory handle.</returns>
public DeviceMemory GetDeviceMemory()
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    EnsureOnDevice();
    return _deviceMemory;
}

/// <summary>
/// Ensures the buffer is available on the host. Triggers transfer if needed.
/// </summary>
public void EnsureOnHost()
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    lock (_lock)
    {
        switch (_state)
        {
            case BufferState.Uninitialized:
                _state = BufferState.HostOnly;
                break;

            case BufferState.HostOnly:
            case BufferState.Synchronized:
            case BufferState.HostDirty:
                // Already on host
                break;

            case BufferState.DeviceOnly:
            case BufferState.DeviceDirty:
                // Transfer from device to host
                TransferDeviceToHost();
                _state = BufferState.Synchronized;
                break;

            default:
                throw new InvalidOperationException($"Invalid buffer state: {_state}");
        }
    }
}

/// <summary>
/// Ensures the buffer is available on the device. Triggers transfer if needed.
/// </summary>
public void EnsureOnDevice()
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    lock (_lock)
    {
        switch (_state)
        {
            case BufferState.Uninitialized:
                AllocateDeviceMemory();
                _state = BufferState.DeviceOnly;
                break;

            case BufferState.DeviceOnly:
            case BufferState.Synchronized:
            case BufferState.DeviceDirty:
                // Already on device
                break;

            case BufferState.HostOnly:
            case BufferState.HostDirty:
                // Transfer from host to device
                EnsureDeviceAllocated();
                TransferHostToDevice();
                _state = BufferState.Synchronized;
                break;

            default:
                throw new InvalidOperationException($"Invalid buffer state: {_state}");
        }
    }
}

/// <summary>
/// Asynchronously ensures the buffer is available on the host.
/// </summary>
/// <param name="context">The accelerator context to use for the async operation.</param>
/// <param name="cancellationToken">Cancellation token for the async operation.</param>
public async ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        switch (_state)
        {
            case BufferState.Uninitialized:
                _state = BufferState.HostOnly;
                break;

            case BufferState.HostOnly:
            case BufferState.Synchronized:
            case BufferState.HostDirty:
                // Already on host
                break;

            case BufferState.DeviceOnly:
            case BufferState.DeviceDirty:
                // Transfer from device to host
                await TransferDeviceToHostAsync(context, cancellationToken).ConfigureAwait(false);
                _state = BufferState.Synchronized;
                break;

            default:
                throw new InvalidOperationException($"Invalid buffer state: {_state}");
        }
    }
    finally
    {
        _asyncLock.Release();
    }
}

/// <summary>
/// Asynchronously ensures the buffer is available on the device.
/// </summary>
/// <param name="context">The accelerator context to use for the async operation.</param>
/// <param name="cancellationToken">Cancellation token for the async operation.</param>
public async ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        switch (_state)
        {
            case BufferState.Uninitialized:
                await AllocateDeviceMemoryAsync(context, cancellationToken).ConfigureAwait(false);
                _state = BufferState.DeviceOnly;
                break;

            case BufferState.DeviceOnly:
            case BufferState.Synchronized:
            case BufferState.DeviceDirty:
                // Already on device
                break;

            case BufferState.HostOnly:
            case BufferState.HostDirty:
                // Transfer from host to device
                await EnsureDeviceAllocatedAsync(context, cancellationToken).ConfigureAwait(false);
                await TransferHostToDeviceAsync(context, cancellationToken).ConfigureAwait(false);
                _state = BufferState.Synchronized;
                break;

            default:
                throw new InvalidOperationException($"Invalid buffer state: {_state}");
        }
    }
    finally
    {
        _asyncLock.Release();
    }
}

/// <summary>
/// Marks the buffer as modified on the host.
/// </summary>
public void MarkHostDirty()
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    lock (_lock)
    {
        _state = _state switch
        {
            BufferState.HostOnly => BufferState.HostOnly,
            BufferState.Synchronized => BufferState.HostDirty,
            BufferState.DeviceDirty => BufferState.HostDirty,
            _ => _state
        };
    }
}

/// <summary>
/// Marks the buffer as modified on the device.
/// </summary>
public void MarkDeviceDirty()
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    lock (_lock)
    {
        _state = _state switch
        {
            BufferState.DeviceOnly => BufferState.DeviceOnly,
            BufferState.Synchronized => BufferState.DeviceDirty,
            BufferState.HostDirty => BufferState.DeviceDirty,
            _ => _state
        };
    }
}

/// <summary>
/// Synchronizes the buffer state between host and device.
/// </summary>
public void Synchronize()
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    lock (_lock)
    {
        switch (_state)
        {
            case BufferState.HostDirty:
                EnsureDeviceAllocated();
                TransferHostToDevice();
                _state = BufferState.Synchronized;
                break;

            case BufferState.DeviceDirty:
                TransferDeviceToHost();
                _state = BufferState.Synchronized;
                break;

            case BufferState.Synchronized:
                // Already synchronized
                break;

            case BufferState.HostOnly:
                // Data is only on host - no need to synchronize
                // Just ensure state is valid
                break;

            case BufferState.DeviceOnly:
                // Data is only on device - no need to synchronize
                // Just ensure state is valid
                break;

            case BufferState.Uninitialized:
                // Nothing to synchronize
                break;

            default:
                throw new InvalidOperationException($"Cannot synchronize from state: {_state}");
        }
    }
}

/// <summary>
/// Asynchronously synchronizes the buffer state between host and device.
/// </summary>
/// <param name="context">The accelerator context to use for the async operation.</param>
/// <param name="cancellationToken">Cancellation token for the async operation.</param>
public async ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        switch (_state)
        {
            case BufferState.HostDirty:
                await EnsureDeviceAllocatedAsync(context, cancellationToken).ConfigureAwait(false);
                await TransferHostToDeviceAsync(context, cancellationToken).ConfigureAwait(false);
                _state = BufferState.Synchronized;
                break;

            case BufferState.DeviceDirty:
                await TransferDeviceToHostAsync(context, cancellationToken).ConfigureAwait(false);
                _state = BufferState.Synchronized;
                break;

            case BufferState.Synchronized:
                // Already synchronized
                break;

            case BufferState.HostOnly:
                // Data is only on host - no need to synchronize
                // Just ensure state is valid
                break;

            case BufferState.DeviceOnly:
                // Data is only on device - no need to synchronize
                // Just ensure state is valid
                break;

            case BufferState.Uninitialized:
                // Nothing to synchronize
                break;

            default:
                throw new InvalidOperationException($"Cannot synchronize from state: {_state}");
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // Handle cancellation gracefully - don't change state
        throw;
    }
    catch (Exception ex)
    {
        // Handle other exceptions - buffer state may be inconsistent
        throw new InvalidOperationException($"Failed to synchronize buffer: {ex.Message}", ex);
    }
    finally
    {
        _asyncLock.Release();
    }
}

/// <summary>
/// Gets the underlying memory buffer. Required by CPU backend.
/// </summary>
/// <returns>A memory handle to the buffer.</returns>
public Memory<T> GetMemory()
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    EnsureOnHost();
    return new Memory<T>(_hostArray, 0, Length);
}

/// <summary>
/// Copies data from a source memory into this buffer.
/// </summary>
/// <param name="source">The source data to copy.</param>
/// <param name="cancellationToken">Cancellation token.</param>
public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(source.Length, Length);

    await Task.Run(() =>
    {
        EnsureOnHost();
        source.Span.CopyTo(new Span<T>(_hostArray, 0, source.Length));
        MarkHostDirty();
    }, cancellationToken);
}

/// <summary>
/// Copies data from this buffer to a destination memory.
/// </summary>
/// <param name="destination">The destination memory.</param>
/// <param name="cancellationToken">Cancellation token.</param>
public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(destination.Length, Length);

    await Task.Run(() =>
    {
        EnsureOnHost();
        var sourceSpan = new ReadOnlySpan<T>(_hostArray, 0, destination.Length);
        sourceSpan.CopyTo(destination.Span);
    }, cancellationToken);
}

/// <summary>
/// Releases all resources used by the UnifiedBuffer.
/// </summary>
public void Dispose()
{
    if (_disposed)
    {
        return;
    }

    lock (_lock)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Free device memory
        if (_deviceMemory.IsValid)
        {
            // Dispose the device buffer if allocated
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - Required for synchronous disposal
            _deviceBuffer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            _deviceBuffer = null;
            _deviceMemory = DeviceMemory.Invalid;
        }

        // Free pinned host memory
        if (_pinnedHandle.IsAllocated)
        {
            _pinnedHandle.Free();
        }

        _hostArray = null;
        _state = BufferState.Uninitialized;

        // Dispose async lock
        _asyncLock.Dispose();
    }
}

#region Private Methods

private void AllocatePinnedHost()
{
    _hostArray = new T[Length];
    _pinnedHandle = GCHandle.Alloc(_hostArray, GCHandleType.Pinned);
}

private void AllocateDeviceMemory()
{
    if (_deviceBuffer == null)
    {
        // Allocate real device memory using the memory manager
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - Required for synchronous allocation
        var deviceBuffer = _memoryManager.AllocateAsync(SizeInBytes, Options)
            .AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

        // Store the device buffer reference
        _deviceBuffer = deviceBuffer;

        // Create a device memory handle
        // Use a non-zero pointer value to indicate allocated memory
        // The actual device pointer is managed internally by the device buffer
        _deviceMemory = new DeviceMemory(
            new IntPtr(1), // Use non-zero value to indicate valid allocation
            SizeInBytes
        );
    }
}

private void EnsureDeviceAllocated()
{
    if (_deviceBuffer == null)
    {
        AllocateDeviceMemory();
    }
}

private void TransferHostToDevice()
{
    if (_hostArray == null)
    {
        throw new InvalidOperationException("Host array is not allocated");
    }

    if (_deviceBuffer == null)
    {
        throw new InvalidOperationException("Device buffer is not allocated");
    }

    // Transfer data from host to device using the memory manager
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - Required for synchronous transfer
    _deviceBuffer.CopyFromHostAsync<T>(_hostArray.AsMemory())
        .AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
}

private void TransferDeviceToHost()
{
    if (_hostArray == null)
    {
        throw new InvalidOperationException("Host array is not allocated");
    }

    if (_deviceBuffer == null)
    {
        throw new InvalidOperationException("Device buffer is not allocated");
    }

    // Transfer data from device to host using the memory manager
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - Required for synchronous transfer
    _deviceBuffer.CopyToHostAsync<T>(_hostArray.AsMemory())
        .AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
}

/// <summary>
/// Asynchronously transfers data from host to device memory.
/// </summary>
/// <param name="context">The accelerator context to use for the transfer.</param>
/// <param name="cancellationToken">Cancellation token for the async operation.</param>
private async ValueTask TransferHostToDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken)
{
    if (_hostArray == null)
    {
        throw new InvalidOperationException("Host array is not allocated");
    }

    if (_deviceBuffer == null)
    {
        throw new InvalidOperationException("Device buffer is not allocated");
    }

    cancellationToken.ThrowIfCancellationRequested();

    try
    {
        // Transfer data from host to device using the actual device buffer
        await _deviceBuffer.CopyFromHostAsync<T>(_hostArray.AsMemory(), 0, cancellationToken).ConfigureAwait(false);

        // Check cancellation after transfer
        cancellationToken.ThrowIfCancellationRequested();
    }
    catch (OperationCanceledException)
    {
        // Re-throw cancellation exceptions
        throw;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to transfer data from host to device: {ex.Message}", ex);
    }
}

/// <summary>
/// Asynchronously transfers data from device to host memory.
/// </summary>
/// <param name="context">The accelerator context to use for the transfer.</param>
/// <param name="cancellationToken">Cancellation token for the async operation.</param>
private async ValueTask TransferDeviceToHostAsync(AcceleratorContext context, CancellationToken cancellationToken)
{
    if (_hostArray == null)
    {
        throw new InvalidOperationException("Host array is not allocated");
    }

    if (_deviceBuffer == null)
    {
        throw new InvalidOperationException("Device buffer is not allocated");
    }

    cancellationToken.ThrowIfCancellationRequested();

    try
    {
        // Transfer data from device to host using the actual device buffer
        await _deviceBuffer.CopyToHostAsync<T>(_hostArray.AsMemory(), 0, cancellationToken).ConfigureAwait(false);

        // Check cancellation after transfer
        cancellationToken.ThrowIfCancellationRequested();
    }
    catch (OperationCanceledException)
    {
        // Re-throw cancellation exceptions
        throw;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to transfer data from device to host: {ex.Message}", ex);
    }
}

/// <summary>
/// Asynchronously allocates device memory.
/// </summary>
/// <param name="context">The accelerator context to use for allocation.</param>
/// <param name="cancellationToken">Cancellation token for the async operation.</param>
private async ValueTask AllocateDeviceMemoryAsync(AcceleratorContext context, CancellationToken cancellationToken)
{
    if (_deviceBuffer != null)
    {
        return;
    }

    cancellationToken.ThrowIfCancellationRequested();

    try
    {
        // Allocate real device memory using the memory manager
        _deviceBuffer = await _memoryManager.AllocateAsync(SizeInBytes, Options, cancellationToken)
            .ConfigureAwait(false);

        // Create a device memory handle
        // Use a non-zero pointer value to indicate allocated memory
        // The actual device pointer is managed internally by the device buffer
        _deviceMemory = new DeviceMemory(
            new IntPtr(1), // Use non-zero value to indicate valid allocation
            SizeInBytes
        );

        cancellationToken.ThrowIfCancellationRequested();
    }
    catch (OperationCanceledException)
    {
        // Re-throw cancellation exceptions
        throw;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to allocate device memory: {ex.Message}", ex);
    }
}

/// <summary>
/// Asynchronously ensures device memory is allocated.
/// </summary>
/// <param name="context">The accelerator context to use for allocation.</param>
/// <param name="cancellationToken">Cancellation token for the async operation.</param>
private async ValueTask EnsureDeviceAllocatedAsync(AcceleratorContext context, CancellationToken cancellationToken)
{
    if (_deviceBuffer == null)
    {
        await AllocateDeviceMemoryAsync(context, cancellationToken).ConfigureAwait(false);
    }
}

#endregion

#region Public Read/Write Methods

/// <summary>
/// Asynchronously reads data from the buffer.
/// </summary>
/// <param name="offset">The offset to start reading from.</param>
/// <param name="count">The number of elements to read (null for all remaining).</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>An array containing the read data.</returns>
public async ValueTask<T[]> ReadAsync(
    int offset = 0,
    int? count = null,
    CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentOutOfRangeException.ThrowIfNegative(offset);

    count ??= Length - offset;
    ArgumentOutOfRangeException.ThrowIfNegative(count.Value);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count.Value, Length);

    await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await EnsureOnHostAsync(cancellationToken).ConfigureAwait(false);

        var result = new T[count.Value];
        var sourceSpan = new ReadOnlySpan<T>(_hostArray, offset, count.Value);
        sourceSpan.CopyTo(result);

        return result;
    }
    finally
    {
        _asyncLock.Release();
    }
}

/// <summary>
/// Asynchronously writes data to the buffer.
/// </summary>
/// <param name="data">The data to write.</param>
/// <param name="offset">The offset to start writing at.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the write operation.</returns>
public async ValueTask WriteAsync(
    ReadOnlyMemory<T> data,
    int offset = 0,
    CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + data.Length, Length);

    await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await EnsureOnHostAsync(cancellationToken).ConfigureAwait(false);

        var destSpan = new Span<T>(_hostArray, offset, data.Length);
        data.Span.CopyTo(destSpan);

        MarkHostDirty();
    }
    finally
    {
        _asyncLock.Release();
    }
}

/// <summary>
/// Asynchronously writes array data to the buffer.
/// </summary>
/// <param name="data">The data to write.</param>
/// <param name="offset">The offset to start writing at.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the write operation.</returns>
public async ValueTask WriteAsync(
    T[] data,
    int offset = 0,
    CancellationToken cancellationToken = default) => await WriteAsync(data.AsMemory(), offset, cancellationToken).ConfigureAwait(false);

/// <summary>
/// Asynchronously ensures the buffer is available on the host.
/// </summary>
private async ValueTask EnsureOnHostAsync(CancellationToken cancellationToken = default)
{
    if (IsOnHost)
    {
        return;
    }

    // If buffer is on device only, transfer to host
    if (_state is BufferState.DeviceOnly or BufferState.DeviceDirty)
    {
        await TransferFromDeviceAsync(cancellationToken).ConfigureAwait(false);
    }

    EnsureOnHost(); // Ensure host allocation
}

/// <summary>
/// Asynchronously transfers data from device to host.
/// </summary>
private async ValueTask TransferFromDeviceAsync(CancellationToken cancellationToken = default)
{
    if (!_deviceMemory.IsValid || _hostArray == null || _deviceBuffer == null)
    {
        return;
    }

    try
    {
        var hostMemory = new Memory<T>(_hostArray);
        // Transfer data from device to host using the actual device buffer
        await _deviceBuffer.CopyToHostAsync<T>(hostMemory, 0, cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            _state = BufferState.Synchronized;
        }
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to transfer from device: {ex.Message}", ex);
    }
}

#endregion

#region IBuffer<T> Interface Implementation

/// <summary>
/// Creates a slice of this buffer.
/// </summary>
/// <param name="offset">The offset in elements.</param>
/// <param name="length">The length of the slice in elements.</param>
/// <returns>A slice of this buffer.</returns>
public IBuffer<T> Slice(int offset, int length)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(length);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);

    return new UnifiedBufferSlice<T>(this, offset, length);
}

/// <summary>
/// Creates a view of this buffer with a different element type.
/// </summary>
/// <typeparam name="TNew">The new element type.</typeparam>
/// <returns>A view of this buffer as the new type.</returns>
public IBuffer<TNew> AsType<TNew>() where TNew : unmanaged
{
    var newLength = Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>() / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
    return new UnifiedBufferView<T, TNew>(this, newLength);
}

/// <summary>
/// Copies data from this buffer to another buffer.
/// </summary>
/// <param name="destination">The destination buffer.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the copy operation.</returns>
public async ValueTask CopyToAsync(IBuffer<T> destination, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(destination);

    var sourceData = await ReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    await destination.CopyFromHostAsync<T>(sourceData, cancellationToken: cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Copies data from this buffer to another buffer with specified ranges.
/// </summary>
/// <param name="sourceOffset">The offset in this buffer.</param>
/// <param name="destination">The destination buffer.</param>
/// <param name="destinationOffset">The offset in the destination buffer.</param>
/// <param name="count">The number of elements to copy.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the copy operation.</returns>
public async ValueTask CopyToAsync(
    int sourceOffset,
    IBuffer<T> destination,
    int destinationOffset,
    int count,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(destination);

    var sourceData = await ReadAsync(sourceOffset, count, cancellationToken).ConfigureAwait(false);
    await destination.CopyFromHostAsync<T>(sourceData.AsMemory(), offset: destinationOffset, cancellationToken: cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Fills this buffer with a specified value.
/// </summary>
/// <param name="value">The value to fill with.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the fill operation.</returns>
public async ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => await FillAsync(value, 0, Length, cancellationToken).ConfigureAwait(false);

/// <summary>
/// Fills a portion of this buffer with a specified value.
/// </summary>
/// <param name="value">The value to fill with.</param>
/// <param name="offset">The offset to start filling at.</param>
/// <param name="count">The number of elements to fill.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the fill operation.</returns>
public async ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, Length);

    await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await EnsureOnHostAsync(cancellationToken).ConfigureAwait(false);

        var span = new Span<T>(_hostArray, offset, count);
        span.Fill(value);

        MarkHostDirty();
    }
    finally
    {
        _asyncLock.Release();
    }
}

/// <summary>
/// Maps this buffer to host memory for direct access.
/// </summary>
/// <param name="mode">The mapping mode.</param>
/// <returns>A mapped memory region.</returns>
public DotCompute.Abstractions.MappedMemory<T> Map(DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite)
{
    EnsureOnHost();
    return new DotCompute.Abstractions.MappedMemory<T>(this, AsMemory(), mode);
}

/// <summary>
/// Maps a portion of this buffer to host memory for direct access.
/// </summary>
/// <param name="offset">The offset to start mapping at.</param>
/// <param name="length">The number of elements to map.</param>
/// <param name="mode">The mapping mode.</param>
/// <returns>A mapped memory region.</returns>
public DotCompute.Abstractions.MappedMemory<T> MapRange(int offset, int length, DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(length);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);

    EnsureOnHost();
    var memory = new Memory<T>(_hostArray, offset, length);
    return new DotCompute.Abstractions.MappedMemory<T>(this, memory, mode);
}

/// <summary>
/// Asynchronously maps this buffer to host memory.
/// </summary>
/// <param name="mode">The mapping mode.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task that returns the mapped memory region.</returns>
public async ValueTask<DotCompute.Abstractions.MappedMemory<T>> MapAsync(DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite, CancellationToken cancellationToken = default)
{
    await EnsureOnHostAsync(cancellationToken).ConfigureAwait(false);
    return new DotCompute.Abstractions.MappedMemory<T>(this, AsMemory(), mode);
}

#endregion

#region IMemoryBuffer Interface Implementation

/// <summary>
/// Copies data from host memory to this buffer.
/// </summary>
/// <typeparam name="TSource">The source element type.</typeparam>
/// <param name="source">The source data.</param>
/// <param name="offset">The byte offset in this buffer.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the copy operation.</returns>
public async ValueTask CopyFromHostAsync<TSource>(
    ReadOnlyMemory<TSource> source,
    long offset = 0,
    CancellationToken cancellationToken = default) where TSource : unmanaged
{
    if (typeof(TSource) != typeof(T))
    {
        throw new ArgumentException($"Source type {typeof(TSource)} does not match buffer type {typeof(T)}");
    }

    var typedSource = MemoryMarshal.Cast<TSource, T>(source.Span);
    var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());

    await WriteAsync(typedSource.ToArray(), elementOffset, cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Copies data from this buffer to host memory.
/// </summary>
/// <typeparam name="TDestination">The destination element type.</typeparam>
/// <param name="destination">The destination memory.</param>
/// <param name="offset">The byte offset in this buffer.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the copy operation.</returns>
public async ValueTask CopyToHostAsync<TDestination>(
    Memory<TDestination> destination,
    long offset = 0,
    CancellationToken cancellationToken = default) where TDestination : unmanaged
{
    if (typeof(TDestination) != typeof(T))
    {
        throw new ArgumentException($"Destination type {typeof(TDestination)} does not match buffer type {typeof(T)}");
    }

    var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
    var elementCount = destination.Length;

    var sourceData = await ReadAsync(elementOffset, elementCount, cancellationToken).ConfigureAwait(false);
    var typedDestination = MemoryMarshal.Cast<TDestination, T>(destination.Span);
    sourceData.AsSpan().CopyTo(typedDestination);
}

/// <summary>
/// Disposes this buffer asynchronously.
/// </summary>
/// <returns>A task representing the disposal operation.</returns>
public async ValueTask DisposeAsync()
{
    if (_disposed)
    {
        return;
    }

    await _asyncLock.WaitAsync();
    try
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Free device memory
        if (_deviceMemory.IsValid)
        {
            // Dispose the device buffer if allocated
            if (_deviceBuffer != null)
            {
                await _deviceBuffer.DisposeAsync().ConfigureAwait(false);
                _deviceBuffer = null;
            }
            _deviceMemory = DeviceMemory.Invalid;
        }

        // Free pinned host memory  
        if (_pinnedHandle.IsAllocated)
        {
            _pinnedHandle.Free();
        }

        _hostArray = null;
        _state = BufferState.Uninitialized;
    }
    finally
    {
        _asyncLock.Dispose();
    }
}

#endregion
}

/// <summary>
/// Represents a slice of a unified buffer.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class UnifiedBufferSlice<T>(UnifiedBuffer<T> parent, int offset, int length) : IBuffer<T>, IMemoryBuffer, IDisposable where T : unmanaged
{
#pragma warning disable CA2213 // Disposable fields should be disposed - Slice doesn't own parent buffer
private readonly UnifiedBuffer<T> _parent = parent ?? throw new ArgumentNullException(nameof(parent));
#pragma warning restore CA2213
private readonly int _offset = offset;
private readonly int _length = length;

public IAccelerator Accelerator => _parent.Accelerator;
public int Length => _length;
public long SizeInBytes => _length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
public DotCompute.Abstractions.MemoryOptions Options => _parent.Options;
public bool IsDisposed => _parent.IsDisposed;

public ValueTask CopyFromHostAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0, CancellationToken cancellationToken = default) where TSource : unmanaged
{
    if (typeof(TSource) != typeof(T))
    {
        throw new ArgumentException($"Source type {typeof(TSource)} does not match buffer type {typeof(T)}");
    }

    var typedSource = MemoryMarshal.Cast<TSource, T>(source.Span);
    return _parent.WriteAsync(typedSource.ToArray(), _offset + (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()), cancellationToken);
}

public ValueTask CopyToHostAsync<TDestination>(Memory<TDestination> destination, long offset = 0, CancellationToken cancellationToken = default) where TDestination : unmanaged
{
    if (typeof(TDestination) != typeof(T))
    {
        throw new ArgumentException($"Destination type {typeof(TDestination)} does not match buffer type {typeof(T)}");
    }

    return _parent.CopyToHostAsync(destination, offset + _offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), cancellationToken);
}

public IBuffer<T> Slice(int offset, int length)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(length);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

    return new UnifiedBufferSlice<T>(_parent, _offset + offset, length);
}

public IBuffer<TNew> AsType<TNew>() where TNew : unmanaged
{
    var newLength = _length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>() / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
    return new UnifiedBufferView<T, TNew>(_parent, newLength);
}

public ValueTask CopyToAsync(IBuffer<T> destination, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(destination);

    // Create the read task
    var readTask = _parent.ReadAsync(_offset, _length, cancellationToken);

    return CopyToAsyncImplAsync(readTask, destination, cancellationToken);
}

private static async ValueTask CopyToAsyncImplAsync(ValueTask<T[]> readTask, IBuffer<T> destination, CancellationToken cancellationToken)
{
    var data = await readTask.ConfigureAwait(false);
    await destination.CopyFromHostAsync<T>(data.AsMemory(), 0, cancellationToken).ConfigureAwait(false);
}

public ValueTask CopyToAsync(int sourceOffset, IBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(destination);
    ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
    ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceOffset + count, _length);

    var readTask = _parent.ReadAsync(_offset + sourceOffset, count, cancellationToken);

    return CopyToAsyncWithOffsetImplAsync(readTask, destination, destinationOffset, cancellationToken);
}

private static async ValueTask CopyToAsyncWithOffsetImplAsync(ValueTask<T[]> readTask, IBuffer<T> destination, int destinationOffset, CancellationToken cancellationToken)
{
    var data = await readTask.ConfigureAwait(false);
    await destination.CopyFromHostAsync<T>(data.AsMemory(), destinationOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), cancellationToken).ConfigureAwait(false);
}

public async ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
{
    var fillData = new T[_length];
    Array.Fill(fillData, value);
    await _parent.WriteAsync(fillData, _offset, cancellationToken);
}

public async ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, _length);

    var fillData = new T[count];
    Array.Fill(fillData, value);
    await _parent.WriteAsync(fillData, _offset + offset, cancellationToken);
}

public DotCompute.Abstractions.MappedMemory<T> Map(DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite)
{
    var parentMapped = _parent.Map(mode);
    var sliceMemory = parentMapped.Memory.Slice(_offset, _length);
    return new DotCompute.Abstractions.MappedMemory<T>(this, sliceMemory, mode);
}

public DotCompute.Abstractions.MappedMemory<T> MapRange(int offset, int length, DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(length);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

    var parentMapped = _parent.Map(mode);
    var rangeMemory = parentMapped.Memory.Slice(_offset + offset, length);
    return new DotCompute.Abstractions.MappedMemory<T>(this, rangeMemory, mode);
}

public async ValueTask<DotCompute.Abstractions.MappedMemory<T>> MapAsync(DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite, CancellationToken cancellationToken = default)
{
    var parentMapped = await _parent.MapAsync(mode, cancellationToken);
    var sliceMemory = parentMapped.Memory.Slice(_offset, _length);
    return new DotCompute.Abstractions.MappedMemory<T>(this, sliceMemory, mode);
}

public ValueTask DisposeAsync() => ValueTask.CompletedTask;

public void Dispose()
{
    // Slice doesn't own the parent buffer, so no disposal needed
}
}

/// <summary>
/// Represents a view of a unified buffer with a different element type.
/// </summary>
/// <typeparam name="TOriginal">The original element type.</typeparam>
/// <typeparam name="TNew">The new element type.</typeparam>
internal sealed class UnifiedBufferView<TOriginal, TNew>(UnifiedBuffer<TOriginal> parent, int length) : IBuffer<TNew>, IMemoryBuffer, IDisposable
where TOriginal : unmanaged
where TNew : unmanaged
{
#pragma warning disable CA2213 // Disposable fields should be disposed - View doesn't own parent buffer
private readonly UnifiedBuffer<TOriginal> _parent = parent ?? throw new ArgumentNullException(nameof(parent));
#pragma warning restore CA2213
private readonly int _length = length;

public IAccelerator Accelerator => _parent.Accelerator;
public int Length => _length;
public long SizeInBytes => _length * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
public DotCompute.Abstractions.MemoryOptions Options => _parent.Options;
public bool IsDisposed => _parent.IsDisposed;

public ValueTask CopyFromHostAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0, CancellationToken cancellationToken = default) where TSource : unmanaged
{
    if (typeof(TSource) != typeof(TNew))
    {
        throw new ArgumentException($"Source type {typeof(TSource)} does not match buffer type {typeof(TNew)}");
    }

    // Convert from TNew to TOriginal
    var sourceSpan = MemoryMarshal.Cast<TSource, TNew>(source.Span);
    var originalSpan = MemoryMarshal.Cast<TNew, TOriginal>(sourceSpan);
    var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>());

    return _parent.CopyFromHostAsync<TOriginal>(originalSpan.ToArray().AsMemory(), elementOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>(), cancellationToken);
}

public async ValueTask CopyToHostAsync<TDestination>(Memory<TDestination> destination, long offset = 0, CancellationToken cancellationToken = default) where TDestination : unmanaged
{
    if (typeof(TDestination) != typeof(TNew))
    {
        throw new ArgumentException($"Destination type {typeof(TDestination)} does not match buffer type {typeof(TNew)}");
    }

    var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>());
    var elementCount = destination.Length;

    // Calculate how many TOriginal elements we need
    var originalElementCount = (elementCount * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>()) / System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();
    var originalOffset = (elementOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>()) / System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();

    var originalData = await _parent.ReadAsync(originalOffset, originalElementCount, cancellationToken);
    var newSpan = MemoryMarshal.Cast<TOriginal, TNew>(originalData.AsSpan());
    var destSpan = MemoryMarshal.Cast<TDestination, TNew>(destination.Span);

    newSpan[..Math.Min(newSpan.Length, destSpan.Length)].CopyTo(destSpan);
}

public IBuffer<TNew> Slice(int offset, int length)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(length);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

    // Create a slice view by adjusting the parent offset
    var originalOffset = (offset * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>()) / System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();
    var originalLength = (length * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>()) / System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();

    // The slice is wrapped by the view which manages its lifetime
#pragma warning disable CA2000 // Dispose objects before losing scope
    var parentSlice = new UnifiedBufferSlice<TOriginal>(_parent, originalOffset, originalLength);
#pragma warning restore CA2000 // Dispose objects before losing scope
    return new UnifiedBufferView<TOriginal, TNew>(parentSlice as UnifiedBuffer<TOriginal> ?? _parent, length);
}

public IBuffer<TNew2> AsType<TNew2>() where TNew2 : unmanaged
{
    var newLength = _length * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>() / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew2>();
    return new UnifiedBufferView<TOriginal, TNew2>(_parent, newLength);
}

public async ValueTask CopyToAsync(IBuffer<TNew> destination, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(destination);

    var tempData = new TNew[_length];
    await CopyToHostAsync<TNew>(tempData.AsMemory(), 0, cancellationToken);
    await destination.CopyFromHostAsync<TNew>(tempData.AsMemory(), 0, cancellationToken);
}

public async ValueTask CopyToAsync(int sourceOffset, IBuffer<TNew> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(destination);
    ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
    ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceOffset + count, _length);

    var tempData = new TNew[count];
    await CopyToHostAsync<TNew>(tempData.AsMemory(), sourceOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>(), cancellationToken);
    await destination.CopyFromHostAsync<TNew>(tempData.AsMemory(), destinationOffset * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>(), cancellationToken);
}

public async ValueTask FillAsync(TNew value, CancellationToken cancellationToken = default)
{
    var fillData = new TNew[_length];
    Array.Fill(fillData, value);
    await CopyFromHostAsync<TNew>(fillData.AsMemory(), 0, cancellationToken);
}

public async ValueTask FillAsync(TNew value, int offset, int count, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, _length);

    var fillData = new TNew[count];
    Array.Fill(fillData, value);
    await CopyFromHostAsync<TNew>(fillData.AsMemory(), offset * System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>(), cancellationToken);
}

public DotCompute.Abstractions.MappedMemory<TNew> Map(DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite)
{
    var parentMapped = _parent.Map(mode);
    var viewMemory = MemoryMarshal.Cast<TOriginal, TNew>(parentMapped.Memory.Span)[.._length];
    return new DotCompute.Abstractions.MappedMemory<TNew>(this, viewMemory.ToArray().AsMemory(), mode);
}

public DotCompute.Abstractions.MappedMemory<TNew> MapRange(int offset, int length, DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(length);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

    var parentMapped = _parent.Map(mode);
    var viewMemory = MemoryMarshal.Cast<TOriginal, TNew>(parentMapped.Memory.Span);
    var rangeMemory = viewMemory.Slice(offset, length);
    return new DotCompute.Abstractions.MappedMemory<TNew>(this, rangeMemory.ToArray().AsMemory(), mode);
}

public async ValueTask<DotCompute.Abstractions.MappedMemory<TNew>> MapAsync(DotCompute.Abstractions.MapMode mode = DotCompute.Abstractions.MapMode.ReadWrite, CancellationToken cancellationToken = default)
{
    var parentMapped = await _parent.MapAsync(mode, cancellationToken);
    var viewMemory = MemoryMarshal.Cast<TOriginal, TNew>(parentMapped.Memory.Span)[.._length];
    return new DotCompute.Abstractions.MappedMemory<TNew>(this, viewMemory.ToArray().AsMemory(), mode);
}

public ValueTask DisposeAsync() => ValueTask.CompletedTask;

public void Dispose()
{
    // View doesn't own the parent buffer, so no disposal needed
}
}
