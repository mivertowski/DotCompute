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
        private bool _disposed;

        public CudaMemoryManager(CudaContext context, ILogger logger)
            : this(context, null, logger)
        {
        }

        public CudaMemoryManager(CudaContext context, CudaDevice? device, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _device = device ?? new CudaDevice(context.DeviceId, logger);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _allocations = new ConcurrentDictionary<IntPtr, long>();
            _pinnedAllocator = new CudaPinnedMemoryAllocator(context, logger);


            InitializeMemoryInfo();
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
        public long TotalAvailableMemory => _totalMemory;

        /// <summary>
        /// Gets the currently used memory on the device.
        /// </summary>
        public long UsedMemory => _totalAllocated;

        /// <summary>
        /// Gets the maximum allocation size supported.
        /// </summary>
        public long MaxAllocationSize => _maxAllocationSize;

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

        /// <summary>
        /// Asynchronously disposes the memory manager.
        /// </summary>
        /// <returns>A ValueTask representing the asynchronous dispose operation.</returns>
        public ValueTask DisposeAsync()
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

        public void Dispose()
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
    }
}