// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Extensions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Adapter that wraps CudaMemoryManager for async operations.
    /// Bridges the CUDA memory manager with the unified memory interface.
    /// </summary>
    public sealed class CudaAsyncMemoryManagerAdapter : Abstractions.IUnifiedMemoryManager
    {
        private readonly CudaMemoryManager _memoryManager;
        private readonly ConcurrentDictionary<IUnifiedMemoryBuffer, long> _bufferSizes;
        private long _totalAllocatedBytes;
        private bool _disposed;
        private readonly string _instanceId;
        private IAccelerator? _accelerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaAsyncMemoryManagerAdapter"/> class.
        /// </summary>
        /// <param name="memoryManager">The underlying CUDA memory manager.</param>
        public CudaAsyncMemoryManagerAdapter(CudaMemoryManager memoryManager)
        {
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _bufferSizes = new ConcurrentDictionary<IUnifiedMemoryBuffer, long>();
            _instanceId = Guid.NewGuid().ToString("N")[0..8];
        }

        /// <inheritdoc/>
        public long TotalAvailableMemory => _memoryManager.TotalMemory;

        /// <inheritdoc/>
        public long CurrentAllocatedMemory => _totalAllocatedBytes;


        /// <inheritdoc/>
        public long MaxAllocationSize => _memoryManager.MaxAllocationSize;

        /// <inheritdoc/>
        public MemoryStatistics Statistics
        {
            get
            {
                // Also include memory tracked by the underlying manager
                var managerUsedMemory = _memoryManager.UsedMemory;
                var totalUsed = Math.Max(_totalAllocatedBytes, managerUsedMemory);



                return new MemoryStatistics
                {
                    TotalMemoryBytes = _memoryManager.TotalMemory,
                    UsedMemoryBytes = totalUsed,
                    AvailableMemoryBytes = _memoryManager.TotalMemory - totalUsed,
                    AllocationCount = _bufferSizes.Count,
                    TotalAllocated = totalUsed,
                    DeallocationCount = 0, // Would need separate tracking
                    PeakMemoryUsageBytes = totalUsed // Simplified - would need history tracking
                };
            }
        }

        /// <inheritdoc/>
        public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
            int count,
            MemoryOptions options = MemoryOptions.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();


            return new ValueTask<IUnifiedMemoryBuffer<T>>(Task.Run(() =>
            {
                var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                var devicePtr = IntPtr.Zero;

                // Use unified memory for better host/device interop
                // CudaMemAttachFlags.Global = 0x01

                var result = CudaRuntime.cudaMallocManaged(ref devicePtr, (nuint)sizeInBytes, 0x01);
                CudaRuntime.CheckError(result, "allocating unified memory");

                // Create simplified buffer for the adapter
                // We'll need to create a simpler version that doesn't depend on CudaUnifiedMemoryManagerProduction

                var buffer = new SimpleCudaUnifiedMemoryBuffer<T>(devicePtr, count);

                // Track allocation

                if (_bufferSizes.TryAdd(buffer, sizeInBytes))
                {
                    _ = Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);
                }
                else
                {
                }


                return (IUnifiedMemoryBuffer<T>)buffer;
            }, cancellationToken));
        }

        /// <inheritdoc/>
        public ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
        {
            if (buffer == null)
            {

                return ValueTask.CompletedTask;
            }


            return new ValueTask(Task.Run(() =>
            {
                if (_bufferSizes.TryRemove(buffer, out var size))
                {
                    _ = Interlocked.Add(ref _totalAllocatedBytes, -size);
                }


                buffer.Dispose();
            }, cancellationToken));
        }

        /// <inheritdoc/>
        public IUnifiedMemoryBuffer<T> CreateView<T>(
            IUnifiedMemoryBuffer<T> buffer,
            int offset,
            int count) where T : unmanaged
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(buffer);


            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {

                throw new ArgumentOutOfRangeException("Invalid view range");
            }

            // Create a view without allocating new memory

            unsafe
            {
                var deviceMemory = buffer.GetDeviceMemory();
                var basePtr = deviceMemory.Handle;
                var viewPtr = IntPtr.Add(basePtr, offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
                return new SimpleCudaUnifiedMemoryBuffer<T>(viewPtr, count, ownsMemory: false);
            }
        }

        /// <inheritdoc/>
        public ValueTask CopyAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);


            if (source.Length != destination.Length)
            {

                throw new ArgumentException("Source and destination buffers must have the same length");
            }


            return new ValueTask(Task.Run(() =>
            {
                var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                var result = CudaRuntime.cudaMemcpy(
                    destination.GetDeviceMemory().Handle,
                    source.GetDeviceMemory().Handle,

                    (nuint)sizeInBytes,
                    CudaMemcpyKind.DeviceToDevice
                );
                CudaRuntime.CheckError(result, "copying device memory");
            }, cancellationToken));
        }

        /// <inheritdoc/>
        public ValueTask CopyToDeviceAsync<T>(
            ReadOnlyMemory<T> source,
            IUnifiedMemoryBuffer<T> destination,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(destination);


            if (source.Length != destination.Length)
            {

                throw new ArgumentException("Source and destination must have the same length");
            }


            return new ValueTask(Task.Run(() =>
            {
                unsafe
                {
                    using var pinned = source.Pin();
                    var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                    var result = CudaRuntime.cudaMemcpy(
                        destination.GetDeviceMemory().Handle,
                        new IntPtr(pinned.Pointer),
                        (nuint)sizeInBytes,
                        CudaMemcpyKind.HostToDevice
                    );
                    CudaRuntime.CheckError(result, "copying memory to device");
                }
            }, cancellationToken));
        }

        /// <inheritdoc/>
        public ValueTask CopyFromDeviceAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            Memory<T> destination,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(source);


            if (source.Length != destination.Length)
            {

                throw new ArgumentException("Source and destination must have the same length");
            }


            return new ValueTask(Task.Run(() =>
            {
                unsafe
                {
                    using var pinned = destination.Pin();
                    var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                    var srcMemory = source.GetDeviceMemory();
                    var result = CudaRuntime.cudaMemcpy(
                        new IntPtr(pinned.Pointer),
                        srcMemory.Handle,
                        (nuint)sizeInBytes,
                        CudaMemcpyKind.DeviceToHost
                    );
                    CudaRuntime.CheckError(result, "copying memory from device");
                }
            }, cancellationToken));
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ThrowIfDisposed();

            // Free all tracked buffers

            foreach (var kvp in _bufferSizes)
            {
                kvp.Key.Dispose();
            }
            _bufferSizes.Clear();
            _totalAllocatedBytes = 0;
        }

        /// <inheritdoc/>
        public ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
            // Optimization not implemented


            => ValueTask.CompletedTask;


        /// <inheritdoc/>
        public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
            ReadOnlyMemory<T> data,
            MemoryOptions options = MemoryOptions.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();


            return Task.Run(async () =>
            {
                var buffer = await AllocateAsync<T>(data.Length, options, cancellationToken).ConfigureAwait(false);
                await CopyToDeviceAsync(data, buffer, cancellationToken).ConfigureAwait(false);
                return buffer;
            }, cancellationToken).AsValueTask();
        }


        /// <inheritdoc/>
        public ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
            long sizeInBytes,
            MemoryOptions options = MemoryOptions.None,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();


            return Task.Run(() =>
            {
                var devicePtr = IntPtr.Zero;

                // Use unified memory for better host/device interop
                // CudaMemAttachFlags.Global = 0x01

                var result = CudaRuntime.cudaMallocManaged(ref devicePtr, (nuint)sizeInBytes, 0x01);
                CudaRuntime.CheckError(result, "allocating unified memory");


                var buffer = new CudaRawMemoryBuffer(devicePtr, sizeInBytes);

                // Track allocation

                if (_bufferSizes.TryAdd(buffer, sizeInBytes))
                {
                    _ = Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);
                }


                return (IUnifiedMemoryBuffer)buffer;
            }, cancellationToken).AsValueTask();
        }


        /// <inheritdoc/>
        public ValueTask CopyAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            int sourceOffset,
            IUnifiedMemoryBuffer<T> destination,
            int destinationOffset,
            int count,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);


            if (sourceOffset < 0 || destinationOffset < 0 || count < 0)
            {

                throw new ArgumentOutOfRangeException("Offsets and count must be non-negative");
            }


            if (sourceOffset + count > source.Length || destinationOffset + count > destination.Length)
            {

                throw new ArgumentOutOfRangeException("Copy range exceeds buffer bounds");
            }


            return new ValueTask(Task.Run(() =>
            {
                unsafe
                {
                    var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                    var srcMemory = source.GetDeviceMemory();
                    var dstMemory = destination.GetDeviceMemory();
                    var srcPtr = IntPtr.Add(srcMemory.Handle, sourceOffset * elementSize);
                    var dstPtr = IntPtr.Add(dstMemory.Handle, destinationOffset * elementSize);
                    var sizeInBytes = count * elementSize;


                    var result = CudaRuntime.cudaMemcpy(
                        dstPtr,
                        srcPtr,
                        (nuint)sizeInBytes,
                        CudaMemcpyKind.DeviceToDevice
                    );
                    CudaRuntime.CheckError(result, "copying device memory with offsets");
                }
            }, cancellationToken));
        }


        /// <inheritdoc/>
        public void Free(IUnifiedMemoryBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }


            if (_bufferSizes.TryRemove(buffer, out var size))
            {
                _ = Interlocked.Add(ref _totalAllocatedBytes, -size);
            }


            buffer.Dispose();
        }


        /// <inheritdoc/>
        public IAccelerator Accelerator => _accelerator ?? throw new InvalidOperationException("Accelerator has not been set. This should be called by CudaAccelerator during initialization.");

        /// <summary>
        /// Sets the accelerator reference. This should only be called by CudaAccelerator during initialization.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        internal void SetAccelerator(IAccelerator accelerator)
        {
            _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
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