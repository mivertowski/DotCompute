// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Represents a view into a CUDA memory buffer that doesn't own the underlying memory.
    /// This is used for creating buffer slices without additional allocations.
    /// </summary>
    public sealed class CudaMemoryBufferView : IUnifiedMemoryBuffer
    {
        private readonly nint _devicePointer;
        private readonly long _sizeInBytes;
        private readonly IUnifiedMemoryBuffer _parentBuffer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaMemoryBufferView"/> class.
        /// </summary>
        /// <param name="devicePointer">The device memory pointer for this view.</param>
        /// <param name="sizeInBytes">The size of this view in bytes.</param>
        /// <param name="parentBuffer">The parent buffer that owns the memory.</param>
        public CudaMemoryBufferView(nint devicePointer, long sizeInBytes, IUnifiedMemoryBuffer parentBuffer)
        {
            _devicePointer = devicePointer;
            _sizeInBytes = sizeInBytes;
            _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
        }

        /// <summary>
        /// Gets the device memory pointer for this view.
        /// </summary>
        public nint DevicePointer => _devicePointer;

        /// <inheritdoc/>
        public long SizeInBytes => _sizeInBytes;

        /// <inheritdoc/>
        public BufferState State => _disposed ? BufferState.Disposed : _parentBuffer.State;

        /// <inheritdoc/>
        public MemoryOptions Options => _parentBuffer.Options;

        /// <inheritdoc/>
        public bool IsDisposed => _disposed || _parentBuffer.IsDisposed;

        /// <inheritdoc/>
        public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        {
            throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        }

        /// <inheritdoc/>
        public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        {
            throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Views don't own memory, so we just mark as disposed
            // The parent buffer is responsible for the actual memory cleanup
            _disposed = true;
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Represents a typed view into a CUDA memory buffer that doesn't own the underlying memory.
    /// This is used for creating typed buffer slices without additional allocations.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer.</typeparam>
    public sealed class CudaMemoryBufferView<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
    {
        private readonly nint _devicePointer;
        private readonly int _length;
        private readonly IUnifiedMemoryBuffer _parentBuffer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaMemoryBufferView{T}"/> class.
        /// </summary>
        /// <param name="devicePointer">The device memory pointer for this view.</param>
        /// <param name="length">The number of elements in this view.</param>
        /// <param name="parentBuffer">The parent buffer that owns the memory.</param>
        public CudaMemoryBufferView(nint devicePointer, int length, IUnifiedMemoryBuffer parentBuffer)
        {
            _devicePointer = devicePointer;
            _length = length;
            _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
        }

        /// <summary>
        /// Gets the device memory pointer for this view.
        /// </summary>
        public nint DevicePointer => _devicePointer;

        /// <inheritdoc/>
        public int Length => _length;

        /// <inheritdoc/>
        public long SizeInBytes => _length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        /// <inheritdoc/>
        public BufferState State => _disposed ? BufferState.Disposed : _parentBuffer.State;

        /// <inheritdoc/>
        public MemoryOptions Options => _parentBuffer.Options;

        /// <inheritdoc/>
        public bool IsDisposed => _disposed || _parentBuffer.IsDisposed;

        // Additional interface properties
        public IAccelerator Accelerator => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public bool IsOnHost => false; // Views are always on device for CUDA
        public bool IsOnDevice => true; // Views are always on device for CUDA
        public bool IsDirty => false; // Views don't track dirty state

        // Basic operations that must be implemented but will throw NotImplementedException
        public Span<T> AsSpan() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ReadOnlySpan<T> AsReadOnlySpan() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public Memory<T> AsMemory() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ReadOnlyMemory<T> AsReadOnlyMemory() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public DeviceMemory GetDeviceMemory() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public MappedMemory<T> Map(MapMode mode) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public MappedMemory<T> MapRange(int start, int count, MapMode mode) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask EnsureOnHost(CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask EnsureOnDevice(CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public void EnsureOnHost() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public void EnsureOnDevice() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public void Synchronize() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public void MarkHostDirty() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public void MarkDeviceDirty() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");

        /// <inheritdoc/>
        public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        }

        // Base interface methods
        ValueTask IUnifiedMemoryBuffer.CopyFromAsync<TElement>(ReadOnlyMemory<TElement> source, long offset, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        }

        ValueTask IUnifiedMemoryBuffer.CopyToAsync<TElement>(Memory<TElement> destination, long offset, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        }

        /// <inheritdoc/>
        public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Views don't own memory, so we just mark as disposed
            // The parent buffer is responsible for the actual memory cleanup
            _disposed = true;
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}