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
    /// <remarks>
    /// Initializes a new instance of the <see cref="CudaMemoryBufferView"/> class.
    /// </remarks>
    /// <param name="devicePointer">The device memory pointer for this view.</param>
    /// <param name="sizeInBytes">The size of this view in bytes.</param>
    /// <param name="parentBuffer">The parent buffer that owns the memory.</param>
    public sealed class CudaMemoryBufferView(nint devicePointer, long sizeInBytes, IUnifiedMemoryBuffer parentBuffer) : IUnifiedMemoryBuffer
    {
        private readonly nint _devicePointer = devicePointer;
        private readonly long _sizeInBytes = sizeInBytes;
        private readonly IUnifiedMemoryBuffer _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
        private bool _disposed;

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
        public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");

        /// <inheritdoc/>
        public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");

        /// <inheritdoc/>
        public void Dispose()
            // Views don't own memory, so we just mark as disposed
            // The parent buffer is responsible for the actual memory cleanup

            => _disposed = true;

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
    /// <remarks>
    /// Initializes a new instance of the <see cref="CudaMemoryBufferView{T}"/> class.
    /// </remarks>
    /// <param name="devicePointer">The device memory pointer for this view.</param>
    /// <param name="length">The number of elements in this view.</param>
    /// <param name="parentBuffer">The parent buffer that owns the memory.</param>
    public sealed class CudaMemoryBufferView<T>(nint devicePointer, int length, IUnifiedMemoryBuffer parentBuffer) : IUnifiedMemoryBuffer<T> where T : unmanaged
    {
        private readonly nint _devicePointer = devicePointer;
        private readonly int _length = length;
        private readonly IUnifiedMemoryBuffer _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
        private bool _disposed;

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
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>

        // Additional interface properties
        public IAccelerator Accelerator => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets or sets a value indicating whether on host.
        /// </summary>
        /// <value>The is on host.</value>
        public bool IsOnHost => false; // Views are always on device for CUDA
        /// <summary>
        /// Gets or sets a value indicating whether on device.
        /// </summary>
        /// <value>The is on device.</value>
        public bool IsOnDevice => true; // Views are always on device for CUDA
        /// <summary>
        /// Gets or sets a value indicating whether dirty.
        /// </summary>
        /// <value>The is dirty.</value>
        public bool IsDirty => false; // Views don't track dirty state
        /// <summary>
        /// Gets as span.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        // Basic operations that must be implemented but will throw NotImplementedException
        public Span<T> AsSpan() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets as read only span.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public ReadOnlySpan<T> AsReadOnlySpan() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets as memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public Memory<T> AsMemory() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets as read only memory.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public ReadOnlyMemory<T> AsReadOnlyMemory() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets the device memory.
        /// </summary>
        /// <returns>The device memory.</returns>
        public DeviceMemory GetDeviceMemory() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets map.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public MappedMemory<T> Map(MapMode mode) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets map range.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="count">The count.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>
        public MappedMemory<T> MapRange(int start, int count, MapMode mode) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets map asynchronously.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask EnsureOnHostAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask EnsureOnDeviceAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets ensure on host asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets ensure on device asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Performs ensure on host.
        /// </summary>
        public void EnsureOnHost() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Performs ensure on device.
        /// </summary>
        public void EnsureOnDevice() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Performs synchronize.
        /// </summary>
        public void Synchronize() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Performs mark host dirty.
        /// </summary>
        public void MarkHostDirty() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Performs mark device dirty.
        /// </summary>
        public void MarkDeviceDirty() => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets copy to asynchronously.
        /// </summary>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets fill asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets slice.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>The result of the operation.</returns>
        public IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        /// <summary>
        /// Gets as type.
        /// </summary>
        /// <typeparam name="TNew">The TNew type parameter.</typeparam>
        /// <returns>The result of the operation.</returns>
        public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");

        /// <inheritdoc/>
        public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        }

        // Base interface methods
        ValueTask IUnifiedMemoryBuffer.CopyFromAsync<TElement>(ReadOnlyMemory<TElement> source, long offset, CancellationToken cancellationToken) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");

        ValueTask IUnifiedMemoryBuffer.CopyToAsync<TElement>(Memory<TElement> destination, long offset, CancellationToken cancellationToken) => throw new NotImplementedException("Buffer view operations should be performed through the memory manager");

        /// <inheritdoc/>
        public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            throw new NotImplementedException("Buffer view operations should be performed through the memory manager");
        }

        /// <inheritdoc/>
        public void Dispose()
            // Views don't own memory, so we just mark as disposed
            // The parent buffer is responsible for the actual memory cleanup

            => _disposed = true;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}