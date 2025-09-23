// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Memory;
using DotCompute.Core.Utilities;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Manages CUDA device memory allocation and deallocation.
    /// Consolidated using BaseMemoryManager to eliminate duplicate patterns.
    /// </summary>
    public sealed class CudaMemoryManager : BaseMemoryManager
    {
        private readonly CudaContext _context;
        private readonly CudaDevice _device;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<IntPtr, long> _allocations;
        private readonly CudaPinnedMemoryAllocator _pinnedAllocator;
        private long _totalAllocated;
        private long _totalMemory;
        private long _maxAllocationSize;
        private long _deallocationCount;
        private bool _disposed;
        private IAccelerator? _accelerator;

        public CudaMemoryManager(CudaContext context, ILogger logger)
            : this(context, null, logger)
        {
        }

        public CudaMemoryManager(CudaContext context, CudaDevice? device, ILogger logger)
            : base(logger ?? throw new ArgumentNullException(nameof(logger)))
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _device = device ?? new CudaDevice(context.DeviceId, logger);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _allocations = new ConcurrentDictionary<IntPtr, long>();
            _pinnedAllocator = new CudaPinnedMemoryAllocator(context, logger);


            InitializeMemoryInfo();
        }

        /// <summary>
        /// Sets the accelerator reference for interface compatibility.
        /// This should be called by the accelerator after construction.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        internal void SetAccelerator(IAccelerator accelerator)
        {
            _accelerator = accelerator;
        }

        /// <summary>
        /// Gets the total amount of memory currently allocated.
        /// </summary>
        public long TotalAllocated => _totalAllocated;

        /// <summary>
        /// Gets the total available memory on the device.
        /// </summary>
        public long TotalMemory => _totalMemory;

        /// <summary>
        /// Gets the total available memory on the device for interface compatibility.
        /// </summary>
        public override long TotalAvailableMemory => _totalMemory;

        /// <summary>
        /// Gets the currently used memory on the device.
        /// </summary>
        public long UsedMemory => _totalAllocated;

        /// <summary>
        /// Gets the maximum allocation size supported.
        /// </summary>
        public override long MaxAllocationSize => _maxAllocationSize;

        /// <inheritdoc/>
        public override IAccelerator Accelerator => _accelerator ?? throw new InvalidOperationException("Accelerator not set. Call SetAccelerator first.");

        /// <inheritdoc/>
        public override MemoryStatistics Statistics => GetCudaMemoryStatistics();

        /// <inheritdoc/>
        public override long CurrentAllocatedMemory => _totalAllocated;

        /// <summary>
        /// Allocates device memory synchronously.
        /// </summary>
        public IUnifiedMemoryBuffer<T> Allocate<T>(long count) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var devicePtr = IntPtr.Zero;


            var result = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)sizeInBytes);
            CudaRuntime.CheckError(result, "allocating device memory");

            _allocations[devicePtr] = sizeInBytes;
            _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);

            _logger.LogDebugMessage($"Allocated {sizeInBytes} bytes at 0x{devicePtr:X}");

            return new CudaMemoryBuffer<T>(devicePtr, count, _context);
        }

        /// <summary>
        /// Allocates device memory asynchronously.
        /// </summary>
        public async Task<IUnifiedMemoryBuffer> AllocateAsync(long sizeInBytes, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return await Task.Run(() =>
            {
                var devicePtr = IntPtr.Zero;


                var result = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)sizeInBytes);
                CudaRuntime.CheckError(result, "allocating device memory");

                _allocations[devicePtr] = sizeInBytes;
                _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);

                _logger.LogDebugMessage($"Allocated {sizeInBytes} bytes at 0x{devicePtr:X}");

                // Return a non-generic buffer that implements IUnifiedMemoryBuffer
                return new CudaMemoryBuffer(_device, devicePtr, sizeInBytes);
            }, cancellationToken);
        }

        /// <summary>
        /// Allocates device memory asynchronously with specific type.
        /// </summary>
        public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged
            => await AllocateAsync<T>(count, MemoryOptions.None, cancellationToken);

        /// <summary>
        /// Allocates memory asynchronously with specific type and options.
        /// </summary>
        public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(long count, MemoryOptions options, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Handle pinned memory allocation
            if ((options & MemoryOptions.Pinned) != 0)
            {
                var flags = CudaHostAllocFlags.Default;

                // Configure flags based on options

                if ((options & MemoryOptions.Mapped) != 0)
                {
                    flags |= CudaHostAllocFlags.Mapped;
                }

                if ((options & MemoryOptions.WriteCombined) != 0)
                {
                    flags |= CudaHostAllocFlags.WriteCombined;
                }

                if ((options & MemoryOptions.Portable) != 0)
                {
                    flags |= CudaHostAllocFlags.Portable;
                }


                var pinnedBuffer = await _pinnedAllocator.AllocatePinnedAsync<T>(count, flags, cancellationToken);
                return (IUnifiedMemoryBuffer<T>)pinnedBuffer;
            }

            // Handle unified memory allocation  
            if ((options & MemoryOptions.Unified) != 0)
            {
                return await AllocateUnifiedAsync<T>(count, cancellationToken);
            }

            // Standard device memory allocation
            var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            return await Task.Run(() =>
            {
                var devicePtr = IntPtr.Zero;


                var result = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)sizeInBytes);
                CudaRuntime.CheckError(result, "allocating device memory");

                _allocations[devicePtr] = sizeInBytes;
                _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);

                _logger.LogDebugMessage($"Allocated {sizeInBytes} bytes at {devicePtr} for type {typeof(T).Name} with options {options}");

                return (IUnifiedMemoryBuffer<T>)new CudaMemoryBuffer<T>(devicePtr, count, _context);
            }, cancellationToken);
        }

        /// <summary>
        /// Allocates unified memory (accessible from both host and device).
        /// </summary>
        private async ValueTask<IUnifiedMemoryBuffer<T>> AllocateUnifiedAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            return await Task.Run(() =>
            {
                var unifiedPtr = IntPtr.Zero;


                var result = CudaRuntime.cudaMallocManaged(ref unifiedPtr, (ulong)sizeInBytes, 1); // Global attach
                CudaRuntime.CheckError(result, "allocating unified memory");

                _allocations[unifiedPtr] = sizeInBytes;
                _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);

                _logger.LogDebugMessage($"Allocated {sizeInBytes} bytes unified memory at {unifiedPtr} for type {typeof(T).Name}");

                return (IUnifiedMemoryBuffer<T>)new SimpleCudaUnifiedMemoryBuffer<T>(unifiedPtr, (int)count, true);
            }, cancellationToken);
        }

        /// <summary>
        /// Frees device memory.
        /// </summary>
        public void Free(IntPtr devicePtr)
        {
            if (_allocations.TryRemove(devicePtr, out var size))
            {
                var result = CudaRuntime.cudaFree(devicePtr);
                if (result == CudaError.Success)
                {
                    _ = Interlocked.Add(ref _totalAllocated, -size);
                    _logger.LogDebugMessage($"Freed {size} bytes at 0x{devicePtr:X}");
                }
                else
                {
                    _logger.LogWarningMessage($"Failed to free CUDA memory at 0x{devicePtr:X}: {result}");
                }
            }
        }

        /// <summary>
        /// Frees device memory asynchronously.
        /// </summary>
        public async Task FreeAsync(IntPtr devicePtr, CancellationToken cancellationToken = default) => await Task.Run(() => Free(devicePtr), cancellationToken);

        /// <summary>
        /// Initializes memory information by querying the CUDA runtime.
        /// </summary>
        private void InitializeMemoryInfo()
        {
            try
            {
                _context.MakeCurrent();
                var result = CudaRuntime.cudaMemGetInfo(out var free, out var total);
                if (result == CudaError.Success)
                {
                    _totalMemory = (long)total;
                    // Set max allocation size to 90% of total memory to leave room for other operations
                    _maxAllocationSize = (long)(total * 0.9);
                    _logger.LogDebugMessage($"Initialized CUDA memory info - Total: {_totalMemory} bytes, Max allocation: {_maxAllocationSize} bytes");
                }
                else
                {
                    _logger.LogWarningMessage("");
                    // Fallback values
                    _totalMemory = 8L * 1024 * 1024 * 1024; // 8GB default
                    _maxAllocationSize = _totalMemory / 2;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while initializing CUDA memory info");
                // Fallback values
                _totalMemory = 8L * 1024 * 1024 * 1024; // 8GB default
                _maxAllocationSize = _totalMemory / 2;
            }
        }

        /// <summary>
        /// Resets the memory manager by clearing all tracking and reinitializing.
        /// </summary>
        public void Reset()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Free all existing allocations

            foreach (var allocation in _allocations.Keys.ToList())
            {
                Free(allocation);
            }


            _allocations.Clear();
            _ = Interlocked.Exchange(ref _totalAllocated, 0);

            // Re-initialize memory info

            InitializeMemoryInfo();


            _logger.LogInfoMessage("CUDA memory manager reset completed");
        }

        /// <inheritdoc/>
        public override async ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await Task.Run(() =>
            {
                try
                {
                    _context.MakeCurrent();

                    // Force garbage collection of unused buffers
                    CleanupUnusedBuffers();

                    // Trigger CUDA memory pool optimization if available
                    // TODO: Implement memory pool optimization when CUDA memory pools are added
                    // For now, we'll just synchronize to ensure all operations complete
                    var result = CudaRuntime.cudaDeviceSynchronize();
                    if (result != CudaError.Success)
                    {
                        _logger.LogWarningMessage($"CUDA synchronization during optimization failed: {result}");
                    }

                    _logger.LogDebugMessage("CUDA memory optimization completed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during CUDA memory optimization");
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Free all existing allocations
            foreach (var allocation in _allocations.Keys.ToList())
            {
                Free(allocation);
            }

            _allocations.Clear();
            _ = Interlocked.Exchange(ref _totalAllocated, 0);
            _ = Interlocked.Exchange(ref _deallocationCount, 0);

            _logger.LogInfoMessage("CUDA memory manager cleared");
        }

        /// <inheritdoc/>
        protected override async ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(
            long sizeInBytes,
            MemoryOptions options,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

            // Handle pinned memory allocation
            if ((options & MemoryOptions.Pinned) != 0)
            {
                var flags = CudaHostAllocFlags.Default;

                if ((options & MemoryOptions.Mapped) != 0)
                {
                    flags |= CudaHostAllocFlags.Mapped;
                }

                if ((options & MemoryOptions.WriteCombined) != 0)
                {
                    flags |= CudaHostAllocFlags.WriteCombined;
                }

                if ((options & MemoryOptions.Portable) != 0)
                {
                    flags |= CudaHostAllocFlags.Portable;
                }

                // TODO: Implement proper pinned allocation with size conversion

                throw new NotImplementedException("Pinned memory allocation with raw size not yet implemented");
            }

            // Handle unified memory allocation
            if ((options & MemoryOptions.Unified) != 0)
            {
                return await Task.Run(() =>
                {
                    var unifiedPtr = IntPtr.Zero;
                    var result = CudaRuntime.cudaMallocManaged(ref unifiedPtr, (ulong)sizeInBytes, 1);
                    CudaRuntime.CheckError(result, "allocating unified memory");

                    _allocations[unifiedPtr] = sizeInBytes;
                    _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);

                    _logger.LogDebugMessage($"Allocated {sizeInBytes} bytes unified memory at {unifiedPtr}");
                    return (IUnifiedMemoryBuffer)new CudaMemoryBuffer(_device, unifiedPtr, sizeInBytes, options);
                }, cancellationToken);
            }

            // Standard device memory allocation
            return await Task.Run(() =>
            {
                var devicePtr = IntPtr.Zero;
                var result = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)sizeInBytes);
                CudaRuntime.CheckError(result, "allocating device memory");

                _allocations[devicePtr] = sizeInBytes;
                _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);

                _logger.LogDebugMessage($"Allocated {sizeInBytes} bytes at {devicePtr} with options {options}");
                return (IUnifiedMemoryBuffer)new CudaMemoryBuffer(_device, devicePtr, sizeInBytes, options);
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            await Task.Run(() =>
            {
                if (buffer is CudaMemoryBuffer cudaBuffer)
                {
                    var devicePtr = cudaBuffer.DevicePointer;
                    if (_allocations.TryRemove(devicePtr, out var size))
                    {
                        var result = CudaRuntime.cudaFree(devicePtr);
                        if (result == CudaError.Success)
                        {
                            _ = Interlocked.Add(ref _totalAllocated, -size);
                            _ = Interlocked.Increment(ref _deallocationCount);
                            _logger.LogDebugMessage($"Freed {size} bytes at 0x{devicePtr:X}");
                        }
                        else
                        {
                            _logger.LogWarningMessage($"Failed to free CUDA memory at 0x{devicePtr:X}: {result}");
                        }
                    }
                }
                else
                {
                    _logger.LogWarningMessage($"Cannot free non-CUDA buffer of type {buffer.GetType().Name}");
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask CopyAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);

            var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var minCount = Math.Min(source.Length, destination.Length);
            var bytesToCopy = minCount * elementSize;

            await CopyRawAsync(source, destination, bytesToCopy, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask CopyAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            int sourceOffset,
            IUnifiedMemoryBuffer<T> destination,
            int destinationOffset,
            int length,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
            ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

            if (sourceOffset + length > source.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(length), "Source range exceeds buffer bounds");
            }


            if (destinationOffset + length > destination.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(length), "Destination range exceeds buffer bounds");
            }


            var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var srcPtr = GetDevicePointer(source) + sourceOffset * elementSize;
            var dstPtr = GetDevicePointer(destination) + destinationOffset * elementSize;
            var bytesToCopy = length * elementSize;

            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, (nuint)bytesToCopy, CudaMemcpyKind.DeviceToDevice);
                CudaRuntime.CheckError(result, "copying between CUDA buffers with offsets");
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask CopyToDeviceAsync<T>(
            ReadOnlyMemory<T> source,
            IUnifiedMemoryBuffer<T> destination,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);

            if (source.Length > destination.Length)
            {

                throw new ArgumentException("Source data exceeds destination buffer size", nameof(source));
            }


            await Task.Run(() =>
            {
                var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                var bytesToCopy = source.Length * elementSize;
                var dstPtr = GetDevicePointer(destination);

                unsafe
                {
                    using var handle = source.Pin();
                    var result = CudaRuntime.cudaMemcpy(dstPtr, (IntPtr)handle.Pointer, (nuint)bytesToCopy, CudaMemcpyKind.HostToDevice);
                    CudaRuntime.CheckError(result, "copying from host to device");
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask CopyFromDeviceAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            Memory<T> destination,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (source.Length > destination.Length)
            {

                throw new ArgumentException("Source buffer size exceeds destination capacity", nameof(source));
            }


            await Task.Run(() =>
            {
                var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
                var bytesToCopy = Math.Min(source.Length, destination.Length) * elementSize;
                var srcPtr = GetDevicePointer(source);

                unsafe
                {
                    using var handle = destination.Pin();
                    var result = CudaRuntime.cudaMemcpy((IntPtr)handle.Pointer, srcPtr, (nuint)bytesToCopy, CudaMemcpyKind.DeviceToHost);
                    CudaRuntime.CheckError(result, "copying from device to host");
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public override IUnifiedMemoryBuffer<T> CreateView<T>(
            IUnifiedMemoryBuffer<T> buffer,
            int offset,
            int length)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

            if (offset + length > buffer.Length)
            {

                throw new ArgumentOutOfRangeException(nameof(length), "View exceeds buffer bounds");
            }


            var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var basePtr = GetDevicePointer(buffer);
            var viewPtr = basePtr + offset * elementSize;

            // Create a view buffer that doesn't own the memory
            return new CudaMemoryBufferView<T>(viewPtr, length, buffer);
        }

        /// <inheritdoc/>
        protected override IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

            if (offset + length > buffer.SizeInBytes)
            {

                throw new ArgumentOutOfRangeException(nameof(length), "View exceeds buffer bounds");
            }


            var basePtr = GetDevicePointer(buffer);
            var viewPtr = (nint)(basePtr.ToInt64() + offset);

            // Create a view buffer that doesn't own the memory
            return new CudaMemoryBufferView(viewPtr, length, buffer);
        }

        /// <summary>
        /// Asynchronously disposes the memory manager.
        /// </summary>
        /// <returns>A ValueTask representing the asynchronous dispose operation.</returns>
        public override ValueTask DisposeAsync()
        {
            try
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        public new void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Free all remaining allocations
            foreach (var allocation in _allocations.Keys)
            {
                Free(allocation);
            }

            _allocations.Clear();

            // Dispose pinned memory allocator

            _pinnedAllocator?.Dispose();


            _disposed = true;
        }

        /// <summary>
        /// Gets CUDA-specific memory statistics.
        /// </summary>
        /// <returns>Current memory statistics.</returns>
        private MemoryStatistics GetCudaMemoryStatistics()
        {
            try
            {
                _context.MakeCurrent();
                var result = CudaRuntime.cudaMemGetInfo(out var free, out var total);

                long actualFree = 0;
                var actualTotal = _totalMemory;

                if (result == CudaError.Success)
                {
                    actualFree = (long)free;
                    actualTotal = (long)total;
                }
                else
                {
                    // Fallback to estimated free memory
                    actualFree = _totalMemory - _totalAllocated;
                }

                CleanupUnusedBuffers();
                var activeBuffers = _allocations.Count;
                var allocationCount = AllocationCount;
                var averageAllocationSize = allocationCount > 0 ? (double)TotalAllocatedBytes / allocationCount : 0.0;

                return new MemoryStatistics
                {
                    TotalAllocated = TotalAllocatedBytes,
                    CurrentUsage = _totalAllocated,
                    CurrentUsed = _totalAllocated,
                    PeakUsage = PeakAllocatedBytes,
                    AllocationCount = allocationCount,
                    DeallocationCount = _deallocationCount,
                    ActiveAllocations = activeBuffers,
                    AvailableMemory = actualFree,
                    TotalCapacity = actualTotal,
                    FragmentationPercentage = 0.0, // TODO: Calculate fragmentation
                    AverageAllocationSize = averageAllocationSize,
                    TotalAllocationCount = allocationCount,
                    TotalDeallocationCount = _deallocationCount,
                    PoolHitRate = 0.0, // TODO: Implement when memory pooling is added
                    TotalMemoryBytes = actualTotal,
                    UsedMemoryBytes = _totalAllocated,
                    AvailableMemoryBytes = actualFree,
                    PeakMemoryUsageBytes = PeakAllocatedBytes,
                    TotalFreed = _deallocationCount * (allocationCount > 0 ? TotalAllocatedBytes / allocationCount : 0),
                    ActiveBuffers = activeBuffers,
                    PeakMemoryUsage = PeakAllocatedBytes,
                    TotalAvailable = actualTotal
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving CUDA memory statistics");
                return new MemoryStatistics
                {
                    TotalAllocated = TotalAllocatedBytes,
                    CurrentUsage = _totalAllocated,
                    CurrentUsed = _totalAllocated,
                    PeakUsage = PeakAllocatedBytes,
                    TotalCapacity = _totalMemory,
                    AvailableMemory = Math.Max(0, _totalMemory - _totalAllocated)
                };
            }
        }

        /// <summary>
        /// Copies data between two buffers using raw byte copying.
        /// </summary>
        private static async ValueTask CopyRawAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            long bytesToCopy,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var srcPtr = GetDevicePointer(source);
            var dstPtr = GetDevicePointer(destination);

            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, (nuint)bytesToCopy, CudaMemcpyKind.DeviceToDevice);
                CudaRuntime.CheckError(result, "copying between CUDA buffers");
            }, cancellationToken);
        }

        /// <summary>
        /// Gets the device pointer from a buffer, handling different buffer types.
        /// </summary>
        private static IntPtr GetDevicePointer<T>(IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
        {
            return buffer switch
            {
                CudaMemoryBuffer<T> cudaTypedBuffer => cudaTypedBuffer.DevicePointer,
                CudaMemoryBufferView<T> cudaViewBuffer => cudaViewBuffer.DevicePointer,
                CudaMemoryBuffer cudaBuffer => cudaBuffer.DevicePointer,
                _ => throw new ArgumentException($"Unsupported buffer type: {buffer.GetType().Name}", nameof(buffer))
            };
        }

        /// <summary>
        /// Gets the device pointer from a buffer, handling different buffer types.
        /// </summary>
        private static IntPtr GetDevicePointer(IUnifiedMemoryBuffer buffer)
        {
            return buffer switch
            {
                CudaMemoryBuffer cudaBuffer => cudaBuffer.DevicePointer,
                CudaMemoryBufferView cudaViewBuffer => cudaViewBuffer.DevicePointer,
                _ => throw new ArgumentException($"Unsupported buffer type: {buffer.GetType().Name}", nameof(buffer))
            };
        }
    }
}