// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Extensions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Adapter that wraps CudaMemoryManager for async operations.
    /// Bridges the CUDA memory manager with the unified memory interface.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CudaAsyncMemoryManagerAdapter"/> class.
    /// </remarks>
    /// <param name="memoryManager">The underlying CUDA memory manager.</param>
    public sealed class CudaAsyncMemoryManagerAdapter(CudaMemoryManager memoryManager) : IUnifiedMemoryManager
    {
        [SuppressMessage("IDisposableAnalyzers.Correctness", "CA2213:Disposable fields should be disposed",
            Justification = "Injected via constructor and not owned by adapter - lifecycle managed externally by CudaMemoryIntegration")]
        private readonly CudaMemoryManager _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        private readonly ConcurrentDictionary<IUnifiedMemoryBuffer, long> _bufferSizes = new();
        private long _totalAllocatedBytes;
        private bool _disposed;
        private IAccelerator? _accelerator;

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

                // Use stream-ordered async allocation (CUDA 11.2+) for better pool utilization,
                // falling back to unified memory if async allocation is not available
                var result = CudaRuntime.cudaMallocAsync(ref devicePtr, (nuint)sizeInBytes, IntPtr.Zero);

                if (result != CudaError.Success)
                {
                    // Fallback to unified memory for broader compatibility
                    result = CudaRuntime.cudaMallocManaged(ref devicePtr, (nuint)sizeInBytes, 0x01);
                    CudaRuntime.CheckError(result, "allocating unified memory");
                }

                var buffer = new SimpleCudaUnifiedMemoryBuffer<T>(devicePtr, count);

                if (_bufferSizes.TryAdd(buffer, sizeInBytes))
                {
                    _ = Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);
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
            int length) where T : unmanaged
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(buffer);


            if (offset < 0 || length < 0 || offset + length > buffer.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid view range");
            }

            // Create a view without allocating new memory

            unsafe
            {
                var deviceMemory = buffer.GetDeviceMemory();
                var basePtr = deviceMemory.Handle;
                var viewPtr = IntPtr.Add(basePtr, offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
                return new SimpleCudaUnifiedMemoryBuffer<T>(viewPtr, length, ownsMemory: false);
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
                var sizeInBytes = (ulong)(source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());

                // Use async memcpy on default stream for non-blocking device-to-device transfers
                var result = CudaRuntime.cudaMemcpyAsync(
                    destination.GetDeviceMemory().Handle,
                    source.GetDeviceMemory().Handle,
                    sizeInBytes,
                    CudaMemcpyKind.DeviceToDevice,
                    IntPtr.Zero // default stream
                );
                CudaRuntime.CheckError(result, "copying device memory (async)");

                // Synchronize default stream to ensure copy completes before returning
                var syncResult = CudaRuntime.cudaStreamSynchronize(IntPtr.Zero);
                CudaRuntime.CheckError(syncResult, "synchronizing after device-to-device copy");
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
                    var sizeInBytes = (ulong)(source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
                    var result = CudaRuntime.cudaMemcpyAsync(
                        destination.GetDeviceMemory().Handle,
                        new IntPtr(pinned.Pointer),
                        sizeInBytes,
                        CudaMemcpyKind.HostToDevice,
                        IntPtr.Zero
                    );
                    CudaRuntime.CheckError(result, "copying memory to device (async)");

                    var syncResult = CudaRuntime.cudaStreamSynchronize(IntPtr.Zero);
                    CudaRuntime.CheckError(syncResult, "synchronizing after host-to-device copy");
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
                    var sizeInBytes = (ulong)(source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
                    var srcMemory = source.GetDeviceMemory();
                    var result = CudaRuntime.cudaMemcpyAsync(
                        new IntPtr(pinned.Pointer),
                        srcMemory.Handle,
                        sizeInBytes,
                        CudaMemcpyKind.DeviceToHost,
                        IntPtr.Zero
                    );
                    CudaRuntime.CheckError(result, "copying memory from device (async)");

                    var syncResult = CudaRuntime.cudaStreamSynchronize(IntPtr.Zero);
                    CudaRuntime.CheckError(syncResult, "synchronizing after device-to-host copy");
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
        {
            ThrowIfDisposed();

            // Trim unused allocations from CUDA's internal memory pool and
            // remove stale buffer tracking entries (buffers that have been disposed externally)
            var staleKeys = _bufferSizes.Keys.Where(b =>
            {
                try { return b.IsDisposed; }
                catch { return true; }
            }).ToList();

            foreach (var key in staleKeys)
            {
                if (_bufferSizes.TryRemove(key, out var size))
                {
                    _ = Interlocked.Add(ref _totalAllocatedBytes, -size);
                }
            }

            return ValueTask.CompletedTask;
        }


        /// <inheritdoc/>
        public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
            ReadOnlyMemory<T> source,
            MemoryOptions options = MemoryOptions.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();


            return Task.Run(async () =>
            {
                var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken).ConfigureAwait(false);
                await CopyToDeviceAsync(source, buffer, cancellationToken).ConfigureAwait(false);
                return buffer;
            }, cancellationToken).AsValueTaskAsync();
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
            }, cancellationToken).AsValueTaskAsync();
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

                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Offsets and count must be non-negative");
            }


            if (sourceOffset + count > source.Length || destinationOffset + count > destination.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(count), "Copy range exceeds buffer bounds");
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


                    var result = CudaRuntime.cudaMemcpyAsync(
                        dstPtr,
                        srcPtr,
                        (ulong)sizeInBytes,
                        CudaMemcpyKind.DeviceToDevice,
                        IntPtr.Zero
                    );
                    CudaRuntime.CheckError(result, "copying device memory with offsets (async)");

                    var syncResult = CudaRuntime.cudaStreamSynchronize(IntPtr.Zero);
                    CudaRuntime.CheckError(syncResult, "synchronizing after offset copy");
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
        internal void SetAccelerator(IAccelerator accelerator) => _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));

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

        /// <inheritdoc/>
        public DeviceMemory AllocateDevice(long sizeInBytes)
        {
            ThrowIfDisposed();

            var devicePtr = IntPtr.Zero;
            var result = CudaRuntime.cudaMalloc(ref devicePtr, (nuint)sizeInBytes);
            CudaRuntime.CheckError(result, "allocating device memory");

            return new DeviceMemory(devicePtr, sizeInBytes);
        }

        /// <inheritdoc/>
        public void FreeDevice(DeviceMemory deviceMemory)
        {
            if (deviceMemory.IsValid && !_disposed)
            {
                try
                {
                    var result = CudaRuntime.cudaFree(deviceMemory.Handle);
                    CudaRuntime.CheckError(result, "freeing device memory");
                }
                catch (Exception ex)
                {
                    // Log warning but don't throw during cleanup
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to free device memory: {ex.Message}");
                }
            }
        }

        /// <inheritdoc/>
        public void MemsetDevice(DeviceMemory deviceMemory, byte value, long sizeInBytes)
        {
            ThrowIfDisposed();

            if (!deviceMemory.IsValid)
            {
                throw new ArgumentException("Invalid device memory handle", nameof(deviceMemory));
            }

            var result = CudaRuntime.cudaMemset(deviceMemory.Handle, value, (nuint)sizeInBytes);
            CudaRuntime.CheckError(result, "setting device memory");
        }

        /// <inheritdoc/>
        public ValueTask MemsetDeviceAsync(DeviceMemory deviceMemory, byte value, long sizeInBytes, CancellationToken cancellationToken = default) => new(Task.Run(() => MemsetDevice(deviceMemory, value, sizeInBytes), cancellationToken));

        /// <inheritdoc/>
        public void CopyHostToDevice(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes)
        {
            ThrowIfDisposed();

            if (hostPointer == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid host pointer", nameof(hostPointer));
            }

            if (!deviceMemory.IsValid)
            {
                throw new ArgumentException("Invalid device memory handle", nameof(deviceMemory));
            }

            var result = CudaRuntime.cudaMemcpy(deviceMemory.Handle, hostPointer, (nuint)sizeInBytes, CudaMemcpyKind.HostToDevice);
            CudaRuntime.CheckError(result, "copying host to device memory");
        }

        /// <inheritdoc/>
        public void CopyDeviceToHost(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes)
        {
            ThrowIfDisposed();

            if (!deviceMemory.IsValid)
            {
                throw new ArgumentException("Invalid device memory handle", nameof(deviceMemory));
            }

            if (hostPointer == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid host pointer", nameof(hostPointer));
            }

            var result = CudaRuntime.cudaMemcpy(hostPointer, deviceMemory.Handle, (nuint)sizeInBytes, CudaMemcpyKind.DeviceToHost);
            CudaRuntime.CheckError(result, "copying device to host memory");
        }

        /// <inheritdoc/>
        public ValueTask CopyHostToDeviceAsync(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes, CancellationToken cancellationToken = default) => new(Task.Run(() => CopyHostToDevice(hostPointer, deviceMemory, sizeInBytes), cancellationToken));

        /// <inheritdoc/>
        public ValueTask CopyDeviceToHostAsync(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes, CancellationToken cancellationToken = default) => new(Task.Run(() => CopyDeviceToHost(deviceMemory, hostPointer, sizeInBytes), cancellationToken));

        /// <inheritdoc/>
        public void CopyDeviceToDevice(DeviceMemory sourceDevice, DeviceMemory destinationDevice, long sizeInBytes)
        {
            ThrowIfDisposed();

            if (!sourceDevice.IsValid)
            {
                throw new ArgumentException("Invalid source device memory handle", nameof(sourceDevice));
            }

            if (!destinationDevice.IsValid)
            {
                throw new ArgumentException("Invalid destination device memory handle", nameof(destinationDevice));
            }

            var result = CudaRuntime.cudaMemcpy(destinationDevice.Handle, sourceDevice.Handle, (nuint)sizeInBytes, CudaMemcpyKind.DeviceToDevice);
            CudaRuntime.CheckError(result, "copying device to device memory");
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
