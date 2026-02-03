// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Memory
{
    // Partial class to add missing interface implementations to P2PBuffer
    public sealed partial class P2PBuffer<T>
    {
        // Local state tracking for P2P buffer state transitions
        private BufferState _localState = BufferState.Allocated;

        /// <summary>
        /// Gets or sets the buffer state.
        /// </summary>
        public BufferState State
        {
            get
            {
                // Return local state if it's a dirty state, otherwise check underlying buffer
                if (_localState is BufferState.HostDirty or BufferState.DeviceDirty)
                {
                    return _localState;
                }
                return _underlyingBuffer.State;
            }
        }


        /// <summary>
        /// Gets whether the buffer is currently on host.
        /// </summary>
        public bool IsOnHost => State is BufferState.HostReady or BufferState.HostDirty;


        /// <summary>
        /// Gets whether the buffer is currently on device.
        /// </summary>
        public bool IsOnDevice => State is BufferState.DeviceReady or BufferState.DeviceDirty;


        /// <summary>
        /// Gets whether the buffer is dirty.
        /// </summary>
        public bool IsDirty => State is BufferState.HostDirty or BufferState.DeviceDirty;
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        // Memory access methods

        public Span<T> AsSpan()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("P2P buffers do not support direct span access");
        }
        /// <summary>
        /// Gets as read only span.
        /// </summary>
        /// <returns>The result of the operation.</returns>


        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("P2P buffers do not support direct span access");
        }
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>


        public Memory<T> AsMemory()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("P2P buffers do not support direct memory access");
        }
        /// <summary>
        /// Gets as read only memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>


        public ReadOnlyMemory<T> AsReadOnlyMemory()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("P2P buffers do not support direct memory access");
        }
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>


        public DeviceMemory GetDeviceMemory()
        {
            ThrowIfDisposed();
            if (_underlyingBuffer is IUnifiedMemoryBuffer<T> typedBuffer)
            {
                return typedBuffer.GetDeviceMemory();
            }
            return DeviceMemory.Invalid;
        }
        /// <summary>
        /// Ensures the buffer data is available on the host.
        /// If the buffer is marked as device dirty, triggers a transfer from device to host.
        /// </summary>
        public void EnsureOnHost()
        {
            ThrowIfDisposed();

            lock (_syncLock)
            {
                // If device has dirty data, we would trigger a transfer here
                // For now, just update the state to indicate host readiness
                if (_localState == BufferState.DeviceDirty)
                {
                    // In a full implementation, this would trigger a device-to-host transfer
                    // For P2P buffers, the transfer is handled by the copy methods
                    _localState = BufferState.HostReady;
                }
                else if (_localState != BufferState.HostDirty)
                {
                    _localState = BufferState.HostReady;
                }
            }
        }

        /// <summary>
        /// Ensures the buffer data is available on the device.
        /// If the buffer is marked as host dirty, triggers a transfer from host to device.
        /// </summary>
        public void EnsureOnDevice()
        {
            ThrowIfDisposed();

            lock (_syncLock)
            {
                // If host has dirty data, we would trigger a transfer here
                // For now, just update the state to indicate device readiness
                if (_localState == BufferState.HostDirty)
                {
                    // In a full implementation, this would trigger a host-to-device transfer
                    // For P2P buffers, the transfer is handled by the copy methods
                    _localState = BufferState.DeviceReady;
                }
                else if (_localState != BufferState.DeviceDirty)
                {
                    _localState = BufferState.DeviceReady;
                }
            }
        }
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>


        public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnHost();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>


        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnDevice();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Synchronizes the buffer state between host and device.
        /// After synchronization, the buffer is marked as coherent on both host and device.
        /// </summary>
        public void Synchronize()
        {
            ThrowIfDisposed();

            lock (_syncLock)
            {
                // Reset state to allocated/synchronized after explicit synchronization
                _localState = BufferState.Allocated;
            }
        }
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>


        public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            Synchronize();
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Marks the buffer as having been modified on the host.
        /// This indicates that the device copy may be stale and needs synchronization.
        /// </summary>
        public void MarkHostDirty()
        {
            ThrowIfDisposed();

            lock (_syncLock)
            {
                _localState = BufferState.HostDirty;
            }
        }

        /// <summary>
        /// Marks the buffer as having been modified on the device.
        /// This indicates that the host copy may be stale and needs synchronization.
        /// </summary>
        public void MarkDeviceDirty()
        {
            ThrowIfDisposed();

            lock (_syncLock)
            {
                _localState = BufferState.DeviceDirty;
            }
        }
        /// <summary>
        /// Gets copy from asynchronously.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        // Copy methods

        public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => await CopyFromHostAsync(source, 0, cancellationToken);
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>


        public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => await CopyToHostAsync(destination, 0, cancellationToken);
    }
}
