// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Raw untyped CUDA memory buffer for byte-level operations.
    /// </summary>
    public sealed class CudaRawMemoryBuffer : IUnifiedMemoryBuffer, IDisposable, IAsyncDisposable
    {
        private readonly IntPtr _devicePtr;
        private readonly long _sizeInBytes;
        private readonly bool _ownsMemory;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaRawMemoryBuffer"/> class.
        /// </summary>
        /// <param name="devicePtr">The device memory pointer.</param>
        /// <param name="sizeInBytes">The size in bytes.</param>
        /// <param name="ownsMemory">Whether this buffer owns the memory.</param>
        public CudaRawMemoryBuffer(IntPtr devicePtr, long sizeInBytes, bool ownsMemory = true)
        {
            _devicePtr = devicePtr;
            _sizeInBytes = sizeInBytes;
            _ownsMemory = ownsMemory;
        }

        /// <inheritdoc/>
        public IntPtr DevicePointer => _devicePtr;

        /// <inheritdoc/>
        public IntPtr HostPointer => _devicePtr; // Unified memory

        /// <inheritdoc/>
        public long SizeInBytes => _sizeInBytes;

        /// <inheritdoc/>
        public static bool IsUnified => true; // CUDA unified memory

        /// <inheritdoc/>
        public static MemoryLocation Location => MemoryLocation.Unified;

        /// <inheritdoc/>
        public MemoryOptions Options => MemoryOptions.None;

        /// <inheritdoc/>
        public bool IsDisposed => _disposed;

        /// <inheritdoc/>
        public BufferState State => _disposed ? BufferState.Disposed : BufferState.DeviceReady;

        /// <inheritdoc/>
        public ReadOnlySpan<byte> AsReadOnlySpan()
        {
            ThrowIfDisposed();
            EnsureOnHost();


            unsafe
            {
                return new ReadOnlySpan<byte>(_devicePtr.ToPointer(), checked((int)_sizeInBytes));
            }
        }

        /// <inheritdoc/>
        public Span<byte> AsSpan()
        {
            ThrowIfDisposed();
            EnsureOnHost();


            unsafe
            {
                return new Span<byte>(_devicePtr.ToPointer(), checked((int)_sizeInBytes));
            }
        }

        /// <inheritdoc/>
        public Memory<byte> AsMemory()
        {
            ThrowIfDisposed();
            throw new NotSupportedException("Memory<T> is not supported for raw CUDA buffers. Use AsSpan() instead.");
        }

        /// <inheritdoc/>
        public void CopyTo(IUnifiedMemoryBuffer destination)
        {
            ArgumentNullException.ThrowIfNull(destination);


            if (destination.SizeInBytes != _sizeInBytes)
            {

                throw new ArgumentException("Destination buffer must have the same size");
            }

            // For raw buffer copy, we need to copy via host memory since we don't have device pointer access

            unsafe
            {
                var tempBuffer = new byte[_sizeInBytes];
                fixed (byte* tempPtr = tempBuffer)
                {
                    // Copy from device to temp
                    var result = CudaRuntime.cudaMemcpy(
                        new IntPtr(tempPtr),
                        _devicePtr,
                        (nuint)_sizeInBytes,
                        CudaMemcpyKind.DeviceToHost
                    );
                    CudaRuntime.CheckError(result, "copying to temp buffer");

                    // Copy from temp to destination

                    destination.CopyFromAsync<byte>(tempBuffer, 0).GetAwaiter().GetResult();
                }
            }
        }

        /// <inheritdoc/>
        public void CopyFrom(IUnifiedMemoryBuffer source)
        {
            ArgumentNullException.ThrowIfNull(source);


            if (source.SizeInBytes != _sizeInBytes)
            {

                throw new ArgumentException("Source buffer must have the same size");
            }

            // For raw buffer copy, we need to copy via host memory since we don't have device pointer access

            unsafe
            {
                var tempBuffer = new byte[_sizeInBytes];

                // Copy from source to temp

                source.CopyToAsync<byte>(tempBuffer, 0).GetAwaiter().GetResult();


                fixed (byte* tempPtr = tempBuffer)
                {
                    // Copy from temp to device
                    var result = CudaRuntime.cudaMemcpy(
                        _devicePtr,
                        new IntPtr(tempPtr),
                        (nuint)_sizeInBytes,
                        CudaMemcpyKind.HostToDevice
                    );
                    CudaRuntime.CheckError(result, "copying from temp buffer");
                }
            }
        }

        /// <inheritdoc/>
        public static void Synchronize()
        {
            var result = CudaRuntime.cudaDeviceSynchronize();
            CudaRuntime.CheckError(result, "synchronizing device");
        }

        /// <inheritdoc/>
        public void Prefetch(int deviceId = -1)
        {
            var device = deviceId >= 0 ? deviceId : -1; // -1 represents CPU
            var result = CudaRuntime.cudaMemPrefetchAsync(_devicePtr, (nuint)_sizeInBytes, device, IntPtr.Zero);

            // Prefetch is optional, so we don't throw on error

            if (result != CudaError.Success && result != CudaError.NotSupported)
            {
                // Log warning but don't fail
            }
        }

        private static void EnsureOnHost()
        {
            // For unified memory, ensure data is accessible on host
            Synchronize();
        }

        /// <inheritdoc/>
        public async ValueTask CopyFromAsync<TSource>(
            ReadOnlyMemory<TSource> source,
            long destinationOffset,
            CancellationToken cancellationToken = default) where TSource : unmanaged
        {
            ThrowIfDisposed();


            var sourceSizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<TSource>();


            if (destinationOffset + sourceSizeInBytes > _sizeInBytes)
            {

                throw new ArgumentOutOfRangeException(nameof(destinationOffset));
            }


            await Task.Run(() =>
            {
                unsafe
                {
                    var sourceSpan = source.Span;
                    fixed (TSource* srcPtr = sourceSpan)
                    {
                        var destPtr = _devicePtr + (nint)destinationOffset;
                        Buffer.MemoryCopy(srcPtr, destPtr.ToPointer(),

                            _sizeInBytes - destinationOffset, sourceSizeInBytes);
                    }
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask CopyToAsync<TDest>(
            Memory<TDest> destination,
            long sourceOffset,
            CancellationToken cancellationToken = default) where TDest : unmanaged
        {
            ThrowIfDisposed();


            var destSizeInBytes = destination.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<TDest>();


            if (sourceOffset + destSizeInBytes > _sizeInBytes)
            {

                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }


            await Task.Run(() =>
            {
                unsafe
                {
                    var destSpan = destination.Span;
                    fixed (TDest* destPtr = destSpan)
                    {
                        var srcPtr = _devicePtr + (nint)sourceOffset;
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

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}