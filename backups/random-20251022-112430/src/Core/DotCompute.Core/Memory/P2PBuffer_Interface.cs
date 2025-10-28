// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Memory
{
    // Partial class to add missing interface implementations to P2PBuffer
    public sealed partial class P2PBuffer<T>
    {
        /// <summary>
        /// Gets or sets the buffer state.
        /// </summary>
        public BufferState State

        => _underlyingBuffer.State;


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
        /// Performs ensure on host.
        /// </summary>

        // Synchronization methods

        public void EnsureOnHost() => ThrowIfDisposed();// TODO: Implement proper state transition// State = BufferState.HostReady;
        /// <summary>
        /// Performs ensure on device.
        /// </summary>

        public void EnsureOnDevice() => ThrowIfDisposed();// TODO: Implement proper state transition// State = BufferState.DeviceReady;
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
        /// Performs synchronize.
        /// </summary>


        public void Synchronize() => ThrowIfDisposed();// P2P buffers handle synchronization internally
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
        /// Performs mark host dirty.
        /// </summary>


        public void MarkHostDirty() { /* TODO: Implement state tracking */ }
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>
        public void MarkDeviceDirty() { /* TODO: Implement state tracking */ }
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