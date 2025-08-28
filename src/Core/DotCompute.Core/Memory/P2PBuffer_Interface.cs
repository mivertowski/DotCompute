// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;
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
        public bool IsOnHost => State == BufferState.HostReady || State == BufferState.HostDirty;
        
        /// <summary>
        /// Gets whether the buffer is currently on device.
        /// </summary>
        public bool IsOnDevice => State == BufferState.DeviceReady || State == BufferState.DeviceDirty;
        
        /// <summary>
        /// Gets whether the buffer is dirty.
        /// </summary>
        public bool IsDirty => State == BufferState.HostDirty || State == BufferState.DeviceDirty;
        
        // Memory access methods
        public Span<T> AsSpan()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("P2P buffers do not support direct span access");
        }
        
        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("P2P buffers do not support direct span access");
        }
        
        public Memory<T> AsMemory()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("P2P buffers do not support direct memory access");
        }
        
        public ReadOnlyMemory<T> AsReadOnlyMemory()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("P2P buffers do not support direct memory access");
        }
        
        public DeviceMemory GetDeviceMemory()
        {
            ThrowIfDisposed();
            if (_underlyingBuffer is IUnifiedMemoryBuffer<T> typedBuffer)
            {
                return typedBuffer.GetDeviceMemory();
            }
            return DeviceMemory.Invalid;
        }

        // Synchronization methods

        public void EnsureOnHost() => ThrowIfDisposed();// TODO: Implement proper state transition// State = BufferState.HostReady;

        public void EnsureOnDevice() => ThrowIfDisposed();// TODO: Implement proper state transition// State = BufferState.DeviceReady;


        public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnHost();
            return ValueTask.CompletedTask;
        }
        
        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            EnsureOnDevice();
            return ValueTask.CompletedTask;
        }


        public void Synchronize() => ThrowIfDisposed();// P2P buffers handle synchronization internally


        public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        {
            Synchronize();
            return ValueTask.CompletedTask;
        }
        
        public void MarkHostDirty() { /* TODO: Implement state tracking */ }
        public void MarkDeviceDirty() { /* TODO: Implement state tracking */ }

        // Copy methods

        public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) => await CopyFromHostAsync(source, 0, cancellationToken);


        public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default) => await CopyToHostAsync(destination, 0, cancellationToken);
    }
}