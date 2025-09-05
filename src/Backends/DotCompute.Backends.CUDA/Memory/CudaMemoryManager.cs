// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Manages CUDA device memory allocation and deallocation.
    /// </summary>
    public sealed class CudaMemoryManager : IDisposable
    {
        private readonly CudaContext _context;
        private readonly CudaDevice _device;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<IntPtr, long> _allocations;
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

            _logger.LogDebug("Allocated {Size} bytes at {Address:X}", sizeInBytes, devicePtr);

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

                _logger.LogDebug("Allocated {Size} bytes at {Address:X}", sizeInBytes, devicePtr);

                // Return a non-generic buffer that implements IUnifiedMemoryBuffer
                return new CudaMemoryBuffer(_device, devicePtr, sizeInBytes);
            }, cancellationToken);
        }

        /// <summary>
        /// Allocates device memory asynchronously with specific type.
        /// </summary>
        public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(long count, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

            return await Task.Run(() =>
            {
                var devicePtr = IntPtr.Zero;
                
                var result = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)sizeInBytes);
                CudaRuntime.CheckError(result, "allocating device memory");

                _allocations[devicePtr] = sizeInBytes;
                _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);

                _logger.LogDebug("Allocated {Size} bytes at {Address:X} for type {Type}", sizeInBytes, devicePtr, typeof(T).Name);

                return (IUnifiedMemoryBuffer<T>)new CudaMemoryBuffer<T>(devicePtr, count, _context);
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
                    _logger.LogDebug("Freed {Size} bytes at {Address:X}", size, devicePtr);
                }
                else
                {
                    _logger.LogWarning("Failed to free memory at {Address:X}: {Error}", devicePtr, result);
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
                    _logger.LogDebug("Initialized CUDA memory info - Total: {Total:N0} bytes, Max allocation: {Max:N0} bytes", 
                        _totalMemory, _maxAllocationSize);
                }
                else
                {
                    _logger.LogWarning("Failed to query CUDA memory info: {Error}", result);
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
            
            _logger.LogInformation("CUDA memory manager reset completed");
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
            _disposed = true;
        }
    }
}