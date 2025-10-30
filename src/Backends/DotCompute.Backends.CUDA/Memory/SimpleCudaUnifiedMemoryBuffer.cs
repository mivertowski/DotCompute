// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Extensions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Simple CUDA unified memory buffer implementation for the memory adapter.
    /// This is a lightweight version that doesn't depend on CudaUnifiedMemoryManagerProduction.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SimpleCudaUnifiedMemoryBuffer{T}"/> class.
    /// </remarks>
    public sealed class SimpleCudaUnifiedMemoryBuffer<T>(IntPtr devicePtr, int length, bool ownsMemory = true) : IUnifiedMemoryBuffer<T>, IDisposable, IAsyncDisposable where T : unmanaged
    {
        private readonly IntPtr _devicePtr = devicePtr;
        private readonly int _length = length;
        private readonly bool _ownsMemory = ownsMemory;
        private bool _disposed;

        /// <inheritdoc/>
        public IntPtr DevicePointer => _devicePtr;

        /// <inheritdoc/>
        public IntPtr HostPointer => _devicePtr; // Unified memory

        /// <inheritdoc/>
        public int Length => _length;

        /// <inheritdoc/>
        public long SizeInBytes => (long)_length * Unsafe.SizeOf<T>();

        /// <inheritdoc/>
        public bool IsUnified => true;

        /// <inheritdoc/>
        public MemoryLocation Location => MemoryLocation.Unified;

        /// <inheritdoc/>
        public MemoryOptions Options => MemoryOptions.None;

        /// <inheritdoc/>
        public bool IsDisposed => _disposed;

        /// <inheritdoc/>
        public BufferState State => _disposed ? BufferState.Disposed : BufferState.DeviceReady;

        /// <inheritdoc/>
        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            ThrowIfDisposed();
            EnsureOnHost();


            unsafe
            {
                return new ReadOnlySpan<T>(_devicePtr.ToPointer(), _length);
            }
        }

        /// <inheritdoc/>
        public Span<T> AsSpan()
        {
            ThrowIfDisposed();
            EnsureOnHost();


            unsafe
            {
                return new Span<T>(_devicePtr.ToPointer(), _length);
            }
        }

        /// <inheritdoc/>
        public Memory<T> AsMemory()
        {
            ThrowIfDisposed();
            EnsureOnHost();

            // Create a memory manager for unified memory

            return new UnmanagedMemoryManager<T>(_devicePtr, _length).Memory;
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<T> AsReadOnlyMemory() => AsMemory();

        /// <inheritdoc/>
        public void CopyTo(IUnifiedMemoryBuffer<T> destination)
        {
            ArgumentNullException.ThrowIfNull(destination);


            if (destination.Length != _length)
            {

                throw new ArgumentException("Destination buffer must have the same length");
            }


            var sizeInBytes = SizeInBytes;
            var destMemory = destination.GetDeviceMemory();
            var result = CudaRuntime.cudaMemcpy(
                destMemory.Handle,
                _devicePtr,
                (nuint)sizeInBytes,
                CudaMemcpyKind.DeviceToDevice
            );
            CudaRuntime.CheckError(result, "copying memory");
        }

        /// <inheritdoc/>
        public void CopyFrom(IUnifiedMemoryBuffer<T> source)
        {
            ArgumentNullException.ThrowIfNull(source);


            if (source.Length != _length)
            {

                throw new ArgumentException("Source buffer must have the same length");
            }


            var sizeInBytes = SizeInBytes;
            var srcMemory = source.GetDeviceMemory();
            var result = CudaRuntime.cudaMemcpy(
                _devicePtr,
                srcMemory.Handle,
                (nuint)sizeInBytes,
                CudaMemcpyKind.DeviceToDevice
            );
            CudaRuntime.CheckError(result, "copying memory");
        }

        /// <inheritdoc/>
        public void CopyTo(Span<T> destination)
        {
            if (destination.Length != _length)
            {

                throw new ArgumentException("Destination span must have the same length");
            }


            AsReadOnlySpan().CopyTo(destination);
        }

        /// <inheritdoc/>
        public void CopyFrom(ReadOnlySpan<T> source)
        {
            if (source.Length != _length)
            {

                throw new ArgumentException("Source span must have the same length");
            }


            source.CopyTo(AsSpan());
        }

        /// <inheritdoc/>
        public DeviceMemory GetDeviceMemory()
        {
            ThrowIfDisposed();
            return new DeviceMemory(_devicePtr, SizeInBytes);
        }

        /// <inheritdoc/>
        public MappedMemory<T> Map(MapMode mode)
        {
            ThrowIfDisposed();
            EnsureOnHost();
            return new MappedMemory<T>(AsMemory(), null);
        }

        /// <inheritdoc/>
        public MappedMemory<T> MapRange(int offset, int length, MapMode mode)
        {
            ThrowIfDisposed();


            if (offset < 0 || length < 0 || offset + length > _length)
            {

                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid range");
            }


            EnsureOnHost();
            return new MappedMemory<T>(AsMemory().Slice(offset, length), null);
        }

        /// <inheritdoc/>
        public async ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await Task.Run(EnsureOnHost, cancellationToken).ConfigureAwait(false);
            return Map(mode);
        }

        /// <inheritdoc/>
        public async ValueTask<MappedMemory<T>> MapRangeAsync(int offset, int length, MapMode mode, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await Task.Run(EnsureOnHost, cancellationToken).ConfigureAwait(false);
            return MapRange(offset, length, mode);
        }

        /// <inheritdoc/>
        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => Task.Run(Synchronize, cancellationToken).AsValueTaskAsync();

        /// <inheritdoc/>
        public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => Task.Run(Synchronize, cancellationToken).AsValueTaskAsync();

        /// <inheritdoc/>
        public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
        {
            ThrowIfDisposed();


            if (offset < 0 || length < 0 || offset + length > _length)
            {

                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid slice range");
            }


            var slicePtr = _devicePtr + (offset * Unsafe.SizeOf<T>());
            return new SimpleCudaUnifiedMemoryBuffer<T>(slicePtr, length, ownsMemory: false);
        }

        /// <inheritdoc/>
        public IAccelerator Accelerator => throw new NotSupportedException("Accelerator reference not available in simple buffer");

        /// <inheritdoc/>
        public bool IsOnHost => true; // Unified memory is always accessible from host

        /// <inheritdoc/>
        public bool IsOnDevice => true; // Unified memory is always accessible from device

        /// <inheritdoc/>
        public bool IsDirty => false; // Simplified - unified memory doesn't track dirty state

        /// <inheritdoc/>
        public void EnsureOnHost()
        {
            // For unified memory, ensure data is accessible on host
            Synchronize();
            // Optionally prefetch to CPU
            Prefetch(-1); // -1 represents CPU
        }

        /// <inheritdoc/>
        public void EnsureOnDevice()
            // For unified memory, data is always accessible on device

            => Synchronize();

        /// <inheritdoc/>
        public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => Task.Run(EnsureOnHost, cancellationToken).AsValueTaskAsync();

        /// <inheritdoc/>
        public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default) => Task.Run(EnsureOnDevice, cancellationToken).AsValueTaskAsync();

        /// <inheritdoc/>
        public void MarkHostDirty()
        {
            // Simplified - unified memory doesn't track dirty state
        }

        /// <inheritdoc/>
        public void MarkDeviceDirty()
        {
            // Simplified - unified memory doesn't track dirty state
        }

        /// <inheritdoc/>
        public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        {
            if (source.Length != _length)
            {

                throw new ArgumentException("Source must have the same length");
            }

            // Ensure buffer is synchronized before accessing span
            // Note: Using synchronous methods for performance with unified memory
#pragma warning disable CA1849 // Call async methods when in an async method - intentional for unified memory performance
            EnsureOnHost();
            Synchronize();
#pragma warning restore CA1849

            return Task.Run(() => source.Span.CopyTo(AsSpan()), cancellationToken).AsValueTaskAsync();
        }

        /// <inheritdoc/>
        public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        {
            if (destination.Length != _length)
            {

                throw new ArgumentException("Destination must have the same length");
            }

            // Ensure buffer is synchronized before accessing span
            // Note: Using synchronous methods for performance with unified memory
#pragma warning disable CA1849 // Call async methods when in an async method - intentional for unified memory performance
            EnsureOnHost();
            Synchronize();
#pragma warning restore CA1849

            return Task.Run(() => AsReadOnlySpan().CopyTo(destination.Span), cancellationToken).AsValueTaskAsync();
        }

        /// <inheritdoc/>
        public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);


            if (destination.Length != _length)
            {

                throw new ArgumentException("Destination buffer must have the same length");
            }


            return Task.Run(() => CopyTo(destination), cancellationToken).AsValueTaskAsync();
        }

        /// <inheritdoc/>
        public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);


            if (sourceOffset < 0 || destinationOffset < 0 || count < 0)
            {

                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Offsets and count must be non-negative");
            }


            if (sourceOffset + count > _length || destinationOffset + count > destination.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(count), "Copy range exceeds buffer bounds");
            }


            return Task.Run(() =>
            {
                unsafe
                {
                    var srcPtr = _devicePtr + (sourceOffset * Unsafe.SizeOf<T>());
                    var destMemory = destination.GetDeviceMemory();
                    var dstPtr = destMemory.Handle + (destinationOffset * Unsafe.SizeOf<T>());
                    var sizeInBytes = (nuint)(count * Unsafe.SizeOf<T>());


                    var result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, sizeInBytes, CudaMemcpyKind.DeviceToDevice);
                    CudaRuntime.CheckError(result, "partial copy");
                }
            }, cancellationToken).AsValueTaskAsync();
        }

        /// <inheritdoc/>
        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var span = AsSpan();
                span.Fill(value);
            }, cancellationToken).AsValueTaskAsync();
        }

        /// <inheritdoc/>
        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (offset < 0 || count < 0 || offset + count > _length)
            {

                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid fill range");
            }


            return Task.Run(() =>
            {
                var span = AsSpan().Slice(offset, count);
                span.Fill(value);
            }, cancellationToken).AsValueTaskAsync();
        }

        /// <inheritdoc/>
        public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
        {
            ThrowIfDisposed();


            var sizeInBytes = SizeInBytes;
            var newElementSize = Unsafe.SizeOf<TNew>();


            if (sizeInBytes % newElementSize != 0)
            {
                throw new InvalidOperationException(
                    $"Cannot reinterpret buffer of {sizeInBytes} bytes as {typeof(TNew).Name} " +
                    $"(element size {newElementSize} bytes) - size mismatch");
            }


            var newLength = (int)(sizeInBytes / newElementSize);
            return new SimpleCudaUnifiedMemoryBuffer<TNew>(_devicePtr, newLength, _ownsMemory);
        }

        /// <inheritdoc/>
        public void Synchronize()
        {
            var result = CudaRuntime.cudaDeviceSynchronize();
            CudaRuntime.CheckError(result, "synchronizing device");
        }

        /// <inheritdoc/>
        public void Prefetch(int deviceId = -1)
        {
            var device = deviceId >= 0 ? deviceId : -1; // -1 represents CPU
            var result = CudaRuntime.cudaMemPrefetch(_devicePtr, (nuint)SizeInBytes, device, IntPtr.Zero);

            // Prefetch is optional, so we don't throw on error

            if (result is not CudaError.Success and not CudaError.NotSupported)
            {
                // Log warning but don't fail
            }
        }


        /// <inheritdoc/>
        public async ValueTask CopyFromAsync<TSource>(
            ReadOnlyMemory<TSource> source,
            long offset,
            CancellationToken cancellationToken = default) where TSource : unmanaged
        {
            ThrowIfDisposed();


            var sourceSizeInBytes = source.Length * Unsafe.SizeOf<TSource>();
            var totalSizeInBytes = _length * Unsafe.SizeOf<T>();


            if (offset + sourceSizeInBytes > totalSizeInBytes)
            {

                throw new ArgumentOutOfRangeException(nameof(offset));
            }


            await Task.Run(() =>
            {
                unsafe
                {
                    var sourceSpan = source.Span;
                    fixed (TSource* srcPtr = sourceSpan)
                    {
                        var destPtr = _devicePtr + (nint)offset;
                        Buffer.MemoryCopy(srcPtr, destPtr.ToPointer(),

                            totalSizeInBytes - offset, sourceSizeInBytes);
                    }
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask CopyToAsync<TDest>(
            Memory<TDest> destination,
            long offset,
            CancellationToken cancellationToken = default) where TDest : unmanaged
        {
            ThrowIfDisposed();


            var destSizeInBytes = destination.Length * Unsafe.SizeOf<TDest>();
            var totalSizeInBytes = _length * Unsafe.SizeOf<T>();


            if (offset + destSizeInBytes > totalSizeInBytes)
            {

                throw new ArgumentOutOfRangeException(nameof(offset));
            }


            await Task.Run(() =>
            {
                unsafe
                {
                    var destSpan = destination.Span;
                    fixed (TDest* destPtr = destSpan)
                    {
                        var srcPtr = _devicePtr + (nint)offset;
                        Buffer.MemoryCopy(srcPtr.ToPointer(), destPtr, destSizeInBytes, destSizeInBytes);
                    }
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_ownsMemory && _devicePtr != IntPtr.Zero)
                {
                    var result = CudaRuntime.cudaFree(_devicePtr);
                    // Don't throw on disposal error
                    if (result != CudaError.Success)
                    {
                        // Log error but continue
                    }
                }
                _disposed = true;
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        /// <summary>
        /// Memory manager for unmanaged memory that allows Memory&lt;T&gt; creation.
        /// </summary>
        private sealed class UnmanagedMemoryManager<TElement>(IntPtr ptr, int length) : MemoryManager<TElement> where TElement : unmanaged
        {
            private readonly IntPtr _ptr = ptr;
            private readonly int _length = length;
            /// <summary>
            /// Gets the span.
            /// </summary>
            /// <returns>The span.</returns>

            public override Span<TElement> GetSpan()
            {
                unsafe
                {
                    return new Span<TElement>(_ptr.ToPointer(), _length);
                }
            }
            /// <summary>
            /// Gets pin.
            /// </summary>
            /// <param name="elementIndex">The element index.</param>
            /// <returns>The result of the operation.</returns>

            public override MemoryHandle Pin(int elementIndex = 0)
            {
                if (elementIndex < 0 || elementIndex >= _length)
                {

                    throw new ArgumentOutOfRangeException(nameof(elementIndex));
                }


                unsafe
                {
                    var ptr = _ptr + (elementIndex * Unsafe.SizeOf<TElement>());
                    return new MemoryHandle(ptr.ToPointer(), pinnable: this);
                }
            }
            /// <summary>
            /// Performs unpin.
            /// </summary>

            public override void Unpin()
            {
                // Nothing to do for unified memory
            }

            protected override void Dispose(bool disposing)
            {
                // The buffer owner handles disposal
            }
        }
    }
}
