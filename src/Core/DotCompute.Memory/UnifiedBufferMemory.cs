// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory;

/// <summary>
/// Memory management implementation for unified buffers.
/// Handles host and device memory allocation, deallocation, and lifecycle management.
/// </summary>
public sealed partial class UnifiedBuffer<T>
{
    /// <summary>
    /// Allocates device memory for this buffer if not already allocated.
    /// </summary>
    private void AllocateDeviceMemoryIfNeeded()
    {
        if (_deviceMemory.Handle != IntPtr.Zero)
        {
            return; // Already allocated
        }

        try
        {
            _deviceMemory = _memoryManager.AllocateDevice(SizeInBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to allocate device memory: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deallocates device memory if allocated.
    /// </summary>
    private void DeallocateDeviceMemory()
    {
        if (_deviceMemory.Handle != IntPtr.Zero)
        {
            try
            {
                _memoryManager.FreeDevice(_deviceMemory);
            }
            catch (Exception ex)
            {
                // Log but don't throw during disposal
                System.Diagnostics.Debug.WriteLine($"Warning: Failed to deallocate device memory: {ex.Message}");
            }
            finally
            {
                _deviceMemory = default;
            }
        }
    }

    /// <summary>
    /// Deallocates host memory if allocated.
    /// </summary>
    private void DeallocateHostMemory()
    {
        if (_pinnedHandle.IsAllocated)
        {
            try
            {
                _pinnedHandle.Free();
            }
            catch (Exception ex)
            {
                // Log but don't throw during disposal
                System.Diagnostics.Debug.WriteLine($"Warning: Failed to free pinned memory handle: {ex.Message}");
            }
        }

        if (_hostArray != null)
        {
            try
            {
                // Clear sensitive data
                Array.Clear(_hostArray, 0, _hostArray.Length);
            }
            catch
            {
                // Ignore errors during cleanup
            }
            finally
            {
                _hostArray = null;
            }
        }
    }

    /// <summary>
    /// Ensures host memory is properly allocated and accessible.
    /// </summary>
    private void EnsureHostMemoryAllocated()
    {
        if (_hostArray == null)
        {
            _hostArray = new T[Length];


            if (_pinnedHandle.IsAllocated)
            {
                _pinnedHandle.Free();
            }


            _pinnedHandle = GCHandle.Alloc(_hostArray, GCHandleType.Pinned);
        }
        else if (!_pinnedHandle.IsAllocated)
        {
            _pinnedHandle = GCHandle.Alloc(_hostArray, GCHandleType.Pinned);
        }
    }

    /// <summary>
    /// Validates that memory allocations are consistent and valid.
    /// </summary>
    private bool ValidateMemoryState()
    {
        try
        {
            // Check host memory consistency
            if (IsOnHost)
            {
                if (_hostArray == null || !_pinnedHandle.IsAllocated)
                {
                    return false;
                }

                if (_hostArray.Length != Length)
                {
                    return false;
                }
            }

            // Check device memory consistency
            if (IsOnDevice)
            {
                if (_deviceMemory.Handle == IntPtr.Zero)
                {
                    return false;
                }

                if (_deviceMemory.Size != SizeInBytes)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets memory usage statistics for this buffer.
    /// </summary>
    /// <returns>Memory usage information.</returns>
    public BufferMemoryInfo GetMemoryInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new BufferMemoryInfo
        {
            SizeInBytes = SizeInBytes,
            HostAllocated = _hostArray != null && _pinnedHandle.IsAllocated,
            DeviceAllocated = _deviceMemory.Handle != IntPtr.Zero,
            HostAddress = _pinnedHandle.IsAllocated ? _pinnedHandle.AddrOfPinnedObject() : IntPtr.Zero,
            DeviceAddress = _deviceMemory.Handle,
            State = _state,
            IsPinned = _pinnedHandle.IsAllocated
        };
    }

    /// <summary>
    /// Resizes the buffer to a new length. This operation will invalidate existing data.
    /// </summary>
    /// <param name="newLength">New length in elements.</param>
    public void Resize(int newLength)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (newLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newLength), "Length must be positive");
        }

        if (newLength == Length)
        {
            return; // No change needed
        }

        lock (_lock)
        {
            // Store old data if preserving
            T[]? oldData = null;
            if (_hostArray != null && _state != BufferState.Uninitialized)
            {
                oldData = new T[Math.Min(Length, newLength)];
                Array.Copy(_hostArray, oldData, oldData.Length);
            }

            // Deallocate current memory
            DeallocateDeviceMemory();
            DeallocateHostMemory();

            // Update length and recalculate size
            var newSizeInBytes = newLength * Unsafe.SizeOf<T>();

            // Use reflection to update readonly properties

            var lengthField = typeof(UnifiedBuffer<T>).GetField("<Length>k__BackingField",

                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            lengthField?.SetValue(this, newLength);


            var sizeField = typeof(UnifiedBuffer<T>).GetField("<SizeInBytes>k__BackingField",

                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sizeField?.SetValue(this, newSizeInBytes);

            // Reallocate host memory
            _hostArray = new T[newLength];
            _pinnedHandle = GCHandle.Alloc(_hostArray, GCHandleType.Pinned);

            // Restore old data if available
            if (oldData != null)
            {
                Array.Copy(oldData, _hostArray, oldData.Length);
            }

            _state = BufferState.HostOnly;
        }
    }

    /// <summary>
    /// Compacts the buffer memory by releasing device memory if not needed.
    /// </summary>
    public void Compact()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            // If buffer is synchronized or only on host, we can free device memory
            if (_state is BufferState.Synchronized or BufferState.HostOnly or BufferState.HostDirty)
            {
                DeallocateDeviceMemory();
                _state = BufferState.HostOnly;
            }
        }
    }

    /// <summary>
    /// Prefetches the buffer to device memory for optimal performance.
    /// </summary>
    public async Task PrefetchToDeviceAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _asyncLock.WaitAsync();
        try
        {
            if (!IsOnDevice)
            {
                await EnsureOnDeviceAsync();
            }
        }
        finally
        {
            _ = _asyncLock.Release();
        }
    }

    /// <summary>
    /// Prefetches the buffer to host memory for optimal performance.
    /// </summary>
    public async Task PrefetchToHostAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _asyncLock.WaitAsync();
        try
        {
            if (!IsOnHost)
            {
                await EnsureOnHostAsync();
            }
        }
        finally
        {
            _ = _asyncLock.Release();
        }
    }
}

/// <summary>
/// Information about buffer memory allocation and usage.
/// </summary>
public sealed class BufferMemoryInfo
{
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; init; }
    /// <summary>
    /// Gets or sets the host allocated.
    /// </summary>
    /// <value>The host allocated.</value>
    public bool HostAllocated { get; init; }
    /// <summary>
    /// Gets or sets the device allocated.
    /// </summary>
    /// <value>The device allocated.</value>
    public bool DeviceAllocated { get; init; }
    /// <summary>
    /// Gets or sets the host address.
    /// </summary>
    /// <value>The host address.</value>
    public IntPtr HostAddress { get; init; }
    /// <summary>
    /// Gets or sets the device address.
    /// </summary>
    /// <value>The device address.</value>
    public IntPtr DeviceAddress { get; init; }
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether pinned.
    /// </summary>
    /// <value>The is pinned.</value>
    public bool IsPinned { get; init; }
}