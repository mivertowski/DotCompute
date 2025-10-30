// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory;

/// <summary>
/// Synchronization implementation for unified buffers.
/// Handles data transfer between host and device memory.
/// </summary>
public sealed partial class UnifiedBuffer<T>
{
    /// <summary>
    /// Ensures the buffer data is available on the host.
    /// </summary>
    public void EnsureOnHost()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            switch (_state)
            {
                case BufferState.HostOnly:
                case BufferState.Synchronized:
                case BufferState.HostDirty:
                    // Already on host or host is current
                    EnsureHostMemoryAllocated();
                    return;

                case BufferState.DeviceOnly:
                case BufferState.DeviceDirty:
                    // Need to copy from device to host
                    EnsureHostMemoryAllocated();
                    CopyFromDeviceToHost();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.Uninitialized:
                    // Initialize with empty data
                    EnsureHostMemoryAllocated();
                    _state = BufferState.HostOnly;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown buffer state: {_state}");
            }
        }
    }

    /// <summary>
    /// Ensures the buffer data is available on the device.
    /// </summary>
    public void EnsureOnDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            switch (_state)
            {
                case BufferState.DeviceOnly:
                case BufferState.Synchronized:
                case BufferState.DeviceDirty:
                    // Already on device or device is current
                    AllocateDeviceMemoryIfNeeded();
                    return;

                case BufferState.HostOnly:
                case BufferState.HostDirty:
                    // Need to copy from host to device
                    AllocateDeviceMemoryIfNeeded();
                    CopyFromHostToDevice();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.Uninitialized:
                    // Initialize with empty data on device
                    AllocateDeviceMemoryIfNeeded();
                    // Zero out device memory
                    _memoryManager.MemsetDevice(_deviceMemory, 0, SizeInBytes);
                    _state = BufferState.DeviceOnly;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown buffer state: {_state}");
            }
        }
    }

    /// <summary>
    /// Asynchronously ensures the buffer data is available on the host.
    /// </summary>
    public async ValueTask EnsureOnHostAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _asyncLock.WaitAsync();
        try
        {
            switch (_state)
            {
                case BufferState.HostOnly:
                case BufferState.Synchronized:
                case BufferState.HostDirty:
                    // Already on host or host is current
                    EnsureHostMemoryAllocated();
                    return;

                case BufferState.DeviceOnly:
                case BufferState.DeviceDirty:
                    // Need to copy from device to host
                    EnsureHostMemoryAllocated();
                    await CopyFromDeviceToHostAsync();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.Uninitialized:
                    // Initialize with empty data
                    EnsureHostMemoryAllocated();
                    _state = BufferState.HostOnly;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown buffer state: {_state}");
            }
        }
        finally
        {
            _ = _asyncLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously ensures the buffer data is available on the device.
    /// </summary>
    public async ValueTask EnsureOnDeviceAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _asyncLock.WaitAsync();
        try
        {
            switch (_state)
            {
                case BufferState.DeviceOnly:
                case BufferState.Synchronized:
                case BufferState.DeviceDirty:
                    // Already on device or device is current
                    AllocateDeviceMemoryIfNeeded();
                    return;

                case BufferState.HostOnly:
                case BufferState.HostDirty:
                    // Need to copy from host to device
                    AllocateDeviceMemoryIfNeeded();
                    await CopyFromHostToDeviceAsync();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.Uninitialized:
                    // Initialize with empty data on device
                    AllocateDeviceMemoryIfNeeded();
                    // Zero out device memory
                    await _memoryManager.MemsetDeviceAsync(_deviceMemory, 0, SizeInBytes);
                    _state = BufferState.DeviceOnly;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown buffer state: {_state}");
            }
        }
        finally
        {
            _ = _asyncLock.Release();
        }
    }

    /// <summary>
    /// Synchronizes the buffer so that both host and device have the same data.
    /// </summary>
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            switch (_state)
            {
                case BufferState.Synchronized:
                    // Already synchronized
                    return;

                case BufferState.HostDirty:
                    // Host has newer data, copy to device
                    AllocateDeviceMemoryIfNeeded();
                    CopyFromHostToDevice();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.DeviceDirty:
                    // Device has newer data, copy to host
                    EnsureHostMemoryAllocated();
                    CopyFromDeviceToHost();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.HostOnly:
                    // Copy host data to device
                    AllocateDeviceMemoryIfNeeded();
                    CopyFromHostToDevice();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.DeviceOnly:
                    // Copy device data to host
                    EnsureHostMemoryAllocated();
                    CopyFromDeviceToHost();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.Uninitialized:
                    // Initialize both host and device
                    EnsureHostMemoryAllocated();
                    AllocateDeviceMemoryIfNeeded();
                    CopyFromHostToDevice();
                    _state = BufferState.Synchronized;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown buffer state: {_state}");
            }
        }
    }

    /// <summary>
    /// Asynchronously synchronizes the buffer so that both host and device have the same data.
    /// </summary>
    public async ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _asyncLock.WaitAsync(cancellationToken);
        try
        {
            switch (_state)
            {
                case BufferState.Synchronized:
                    // Already synchronized
                    return;

                case BufferState.HostDirty:
                    // Host has newer data, copy to device
                    AllocateDeviceMemoryIfNeeded();
                    await CopyFromHostToDeviceAsync();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.DeviceDirty:
                    // Device has newer data, copy to host
                    EnsureHostMemoryAllocated();
                    await CopyFromDeviceToHostAsync();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.HostOnly:
                    // Copy host data to device
                    AllocateDeviceMemoryIfNeeded();
                    await CopyFromHostToDeviceAsync();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.DeviceOnly:
                    // Copy device data to host
                    EnsureHostMemoryAllocated();
                    await CopyFromDeviceToHostAsync();
                    _state = BufferState.Synchronized;
                    break;

                case BufferState.Uninitialized:
                    // Initialize both host and device
                    EnsureHostMemoryAllocated();
                    AllocateDeviceMemoryIfNeeded();
                    await CopyFromHostToDeviceAsync();
                    _state = BufferState.Synchronized;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown buffer state: {_state}");
            }
        }
        finally
        {
            _ = _asyncLock.Release();
        }
    }

    /// <summary>
    /// Invalidates the device copy, marking the host as the authoritative source.
    /// </summary>
    public void InvalidateDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            switch (_state)
            {
                case BufferState.HostOnly:
                case BufferState.HostDirty:
                    // Already host-only or host-dirty
                    break;

                case BufferState.Synchronized:
                case BufferState.DeviceOnly:
                case BufferState.DeviceDirty:
                    // Mark as host dirty to indicate device is invalid
                    if (_hostArray != null)
                    {
                        _state = BufferState.HostDirty;
                    }
                    else
                    {
                        // No host data, copy from device first
                        EnsureHostMemoryAllocated();
                        CopyFromDeviceToHost();
                        _state = BufferState.HostDirty;
                    }
                    break;

                case BufferState.Uninitialized:
                    // Initialize host
                    EnsureHostMemoryAllocated();
                    _state = BufferState.HostOnly;
                    break;
            }
        }
    }

    /// <summary>
    /// Invalidates the host copy, marking the device as the authoritative source.
    /// </summary>
    public void InvalidateHost()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            switch (_state)
            {
                case BufferState.DeviceOnly:
                case BufferState.DeviceDirty:
                    // Already device-only or device-dirty
                    break;

                case BufferState.Synchronized:
                case BufferState.HostOnly:
                case BufferState.HostDirty:
                    // Mark as device dirty to indicate host is invalid
                    if (_deviceMemory.Handle != IntPtr.Zero)
                    {
                        _state = BufferState.DeviceDirty;
                    }
                    else
                    {
                        // No device data, copy from host first
                        AllocateDeviceMemoryIfNeeded();
                        CopyFromHostToDevice();
                        _state = BufferState.DeviceDirty;
                    }
                    break;

                case BufferState.Uninitialized:
                    // Initialize device
                    AllocateDeviceMemoryIfNeeded();
                    _memoryManager.MemsetDevice(_deviceMemory, 0, SizeInBytes);
                    _state = BufferState.DeviceOnly;
                    break;
            }
        }
    }

    /// <summary>
    /// Copies data from host to device memory synchronously.
    /// </summary>
    private void CopyFromHostToDevice()
    {
        if (_hostArray == null || !_pinnedHandle.IsAllocated)
        {
            throw new InvalidOperationException("Host memory not allocated");
        }

        if (_deviceMemory.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Device memory not allocated");
        }

        var hostPtr = _pinnedHandle.AddrOfPinnedObject();
        _memoryManager.CopyHostToDevice(hostPtr, _deviceMemory, SizeInBytes);
    }

    /// <summary>
    /// Copies data from device to host memory synchronously.
    /// </summary>
    private void CopyFromDeviceToHost()
    {
        if (_hostArray == null || !_pinnedHandle.IsAllocated)
        {
            throw new InvalidOperationException("Host memory not allocated");
        }

        if (_deviceMemory.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Device memory not allocated");
        }

        var hostPtr = _pinnedHandle.AddrOfPinnedObject();
        _memoryManager.CopyDeviceToHost(_deviceMemory, hostPtr, SizeInBytes);
    }

    /// <summary>
    /// Copies data from host to device memory asynchronously.
    /// </summary>
    private async ValueTask CopyFromHostToDeviceAsync()
    {
        if (_hostArray == null || !_pinnedHandle.IsAllocated)
        {
            throw new InvalidOperationException("Host memory not allocated");
        }

        if (_deviceMemory.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Device memory not allocated");
        }

        var hostPtr = _pinnedHandle.AddrOfPinnedObject();
        await _memoryManager.CopyHostToDeviceAsync(hostPtr, _deviceMemory, SizeInBytes);
    }

    /// <summary>
    /// Copies data from device to host memory asynchronously.
    /// </summary>
    private async ValueTask CopyFromDeviceToHostAsync()
    {
        if (_hostArray == null || !_pinnedHandle.IsAllocated)
        {
            throw new InvalidOperationException("Host memory not allocated");
        }

        if (_deviceMemory.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Device memory not allocated");
        }

        var hostPtr = _pinnedHandle.AddrOfPinnedObject();
        await _memoryManager.CopyDeviceToHostAsync(_deviceMemory, hostPtr, SizeInBytes);
    }

    // Interface implementations for missing methods

    /// <summary>
    /// Asynchronously ensures the buffer is available on the host.
    /// </summary>
    public async ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await ValueTask.CompletedTask;
        EnsureOnHost();
    }

    /// <summary>
    /// Asynchronously ensures the buffer is available on the device.
    /// </summary>
    public async ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await ValueTask.CompletedTask;
        EnsureOnDevice();
    }

    /// <summary>
    /// Maps this buffer to host memory for direct access.
    /// </summary>
    public MappedMemory<T> Map(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return new MappedMemory<T>(_hostArray.AsMemory());
    }

    /// <summary>
    /// Maps a portion of this buffer to host memory for direct access.
    /// </summary>
    public MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);
        EnsureOnHost();
        return new MappedMemory<T>(_hostArray.AsMemory(offset, length));
    }

    /// <summary>
    /// Asynchronously maps this buffer to host memory.
    /// </summary>
    public async ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureOnHostAsync(default, cancellationToken);
        return new MappedMemory<T>(_hostArray.AsMemory());
    }
}
