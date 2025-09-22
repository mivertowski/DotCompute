// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
namespace DotCompute.Linq.KernelGeneration.Memory
{
    /// <summary>
    /// Advanced GPU memory manager with pooling, unified memory support,
    /// peer-to-peer transfers, and optimized allocation strategies.
    /// </summary>
    public sealed class GpuMemoryManager : IDisposable, IAsyncDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly MemoryPool _memoryPool;
        private readonly ConcurrentDictionary<IntPtr, GpuBufferInfo> _allocatedBuffers;
        private readonly P2PTransferManager _p2pManager;
        private readonly UnifiedMemoryManager _unifiedMemoryManager;
        private readonly object _allocationLock = new();
        private bool _disposed;
        // Memory statistics
        private long _totalAllocatedBytes;
        private long _peakAllocatedBytes;
        private int _totalAllocations;
        private int _activeAllocations;
        public GpuMemoryManager(CudaContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryPool = new MemoryPool(context, logger);
            _allocatedBuffers = new ConcurrentDictionary<IntPtr, GpuBufferInfo>();
            _p2pManager = new P2PTransferManager(context, logger);
            _unifiedMemoryManager = new UnifiedMemoryManager(context, logger);
            InitializeMemoryManager();
        }
        /// <summary>
        /// Allocates memory context for kernel execution.
        /// </summary>
        public async Task<MemoryContext> AllocateForKernelAsync<T>(
            int inputSize,
            int estimatedOutputSize,
            CancellationToken cancellationToken = default)
            where T : unmanaged
        {
            _logger.LogDebug("Allocating memory for kernel: input={InputSize}, output={EstimatedOutputSize}",
                inputSize, estimatedOutputSize);
            try
            {
                var inputBuffer = await AllocateBufferAsync<T>(inputSize, BufferType.Input, cancellationToken);
                var outputBuffer = await AllocateBufferAsync<T>(estimatedOutputSize, BufferType.Output, cancellationToken);
                var outputCountBuffer = await AllocateBufferAsync<int>(1, BufferType.Counter, cancellationToken);
                var context = new MemoryContext
                {
                    InputBuffer = inputBuffer,
                    OutputBuffer = outputBuffer,
                    OutputCountBuffer = outputCountBuffer,
                    MemoryManager = this
                };
                _logger.LogDebug("Successfully allocated memory context with {TotalBytes} bytes",
                    inputBuffer.SizeInBytes + outputBuffer.SizeInBytes + outputCountBuffer.SizeInBytes);
                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to allocate memory for kernel");
                throw new MemoryAllocationException("Failed to allocate GPU memory for kernel execution", ex);
            }
        }
        /// Allocates a GPU buffer with specified type and optimization hints.
        public async Task<GpuBuffer<T>> AllocateBufferAsync<T>(
            int elementCount,
            BufferType bufferType = BufferType.General,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            var sizeInBytes = elementCount * Marshal.SizeOf<T>();
            _logger.LogDebug("Allocating GPU buffer: type={BufferType}, elements={ElementCount}, bytes={SizeInBytes}",
                bufferType, elementCount, sizeInBytes);
            try
            {
                lock (_allocationLock)
                {
                    _totalAllocations++;
                _activeAllocations++;
                _totalAllocatedBytes += sizeInBytes;
                _peakAllocatedBytes = Math.Max(_peakAllocatedBytes, _totalAllocatedBytes);
                    // Try pool allocation first for better performance
                    if (_memoryPool.TryAllocateFromPool<T>(elementCount, out var pooledBuffer))
                    {
                        _logger.LogDebug("Allocated buffer from memory pool");
                        return pooledBuffer;
                    }
                // Determine allocation strategy based on buffer type and size
                var allocationStrategy = DetermineAllocationStrategy(sizeInBytes, bufferType);
                    var buffer = allocationStrategy switch
                    {
                        AllocationStrategy.DeviceMemory => await AllocateDeviceMemoryAsync<T>(elementCount),
                        AllocationStrategy.UnifiedMemory => await AllocateUnifiedMemoryAsync<T>(elementCount),
                        AllocationStrategy.PinnedMemory => await AllocatePinnedMemoryAsync<T>(elementCount),
                        _ => await AllocateDeviceMemoryAsync<T>(elementCount)
                    };
                    // Register buffer for tracking
                    RegisterBuffer(buffer);
                    _logger.LogDebug("Successfully allocated {AllocationStrategy} buffer", allocationStrategy);
                    return buffer;
                }
            }
            catch (Exception ex)
            {
                lock (_allocationLock)
                {
                    _activeAllocations--;
                    _totalAllocatedBytes -= sizeInBytes;
                }
                _logger.LogError(ex, "Failed to allocate GPU buffer");
                throw new MemoryAllocationException($"Failed to allocate GPU buffer of size {sizeInBytes} bytes", ex);
            }
        }
        /// Copies data from host to device asynchronously.
        public async Task CopyToDeviceAsync<T>(
            T[] hostData,
            GpuBuffer<T> deviceBuffer,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(hostData);
            ArgumentNullException.ThrowIfNull(deviceBuffer);
            if (hostData.Length > deviceBuffer.Size)
                throw new ArgumentException("Host data size exceeds device buffer capacity");

            try
            {
                _logger.LogDebug("Copying {ElementCount} elements from host to device", hostData.Length);
                await Task.Run(() =>
                    var handle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
                    try
                    {
                        var hostPtr = handle.AddrOfPinnedObject();
                        var sizeInBytes = hostData.Length * Marshal.SizeOf<T>();
                        var result = CudaRuntime.cudaMemcpy(
                            deviceBuffer.DevicePointer,
                            hostPtr,
                            (UIntPtr)sizeInBytes,
                            DotCompute.Backends.CUDA.Types.Native.CudaMemcpyKind.HostToDevice);
                        if (result != CudaError.Success)
                        {
                            throw new MemoryTransferException(
                                $"Failed to copy data to device: {CudaRuntime.GetErrorString(result)}");
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }, cancellationToken);
                _logger.LogDebug("Successfully copied data to device");
            }
            catch (Exception ex) when (ex is not MemoryTransferException)
            {
                _logger.LogError(ex, "Failed to copy data to device");
                throw new MemoryTransferException("Failed to copy data to device", ex);
            }
        }
        /// Copies data from device to host asynchronously.
        public async Task<T[]> CopyFromDeviceAsync<T>(
            GpuBuffer<T> deviceBuffer,
            int elementCount,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(deviceBuffer);
            if (elementCount > deviceBuffer.Size)
                throw new ArgumentException("Element count exceeds device buffer size");

            try
            {
                _logger.LogDebug("Copying {ElementCount} elements from device to host", elementCount);
                var hostData = new T[elementCount];
                await Task.Run(() =>
                {
                    var handle = GCHandle.Alloc(hostData, GCHandleType.Pinned);
                    try
                    {
                        var hostPtr = handle.AddrOfPinnedObject();
                        var sizeInBytes = elementCount * Marshal.SizeOf<T>();
                        var result = CudaRuntime.cudaMemcpy(
                            hostPtr,
                            deviceBuffer.DevicePointer,
                            (UIntPtr)sizeInBytes,
                            DotCompute.Backends.CUDA.Types.Native.CudaMemcpyKind.DeviceToHost);
                        if (result != CudaError.Success)
                        {
                            throw new MemoryTransferException(
                                $"Failed to copy data from device: {CudaRuntime.GetErrorString(result)}");
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }, cancellationToken);
                _logger.LogDebug("Successfully copied data from device");
                return hostData;
            }
            catch (Exception ex) when (ex is not MemoryTransferException)
            {
                _logger.LogError(ex, "Failed to copy data from device");
                throw new MemoryTransferException("Failed to copy data from device", ex);
            }
        }
        /// Copies data from device buffer to another device buffer (overload for compatibility).
        public async Task CopyFromDeviceAsync<T>(
            GpuBuffer<T> sourceBuffer,
            T[] destinationArray,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);
            ArgumentNullException.ThrowIfNull(destinationArray);
            await Task.Run(() =>
            {
                var handle = GCHandle.Alloc(destinationArray, GCHandleType.Pinned);
                try
                {
                    var hostPtr = handle.AddrOfPinnedObject();
                    var sizeInBytes = Math.Min(destinationArray.Length, sourceBuffer.Size) * Marshal.SizeOf<T>();
                    var result = CudaRuntime.cudaMemcpy(
                        hostPtr,
                        sourceBuffer.DevicePointer,
                        (UIntPtr)sizeInBytes,
                        DotCompute.Backends.CUDA.Types.Native.CudaMemcpyKind.DeviceToHost);
                    if (result != CudaError.Success)
                    {
                        throw new MemoryTransferException(
                            $"Failed to copy data from device: {CudaRuntime.GetErrorString(result)}");
                    }
                finally
                {
                    handle.Free();
                }
            }, cancellationToken);
        }
            }, cancellationToken);
        /// Performs peer-to-peer transfer between GPUs.
        public async Task CopyP2PAsync<T>(
            GpuBuffer<T> sourceBuffer,
            GpuBuffer<T> destinationBuffer,
            int elementCount,
            int sourceDeviceId,
            int destinationDeviceId,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            await _p2pManager.TransferAsync(
                sourceBuffer, destinationBuffer, elementCount,
                sourceDeviceId, destinationDeviceId, cancellationToken);
        }
        /// Deallocates a GPU buffer.
        public async Task DeallocateBufferAsync<T>(GpuBuffer<T> buffer) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);

            try
            {
                // Try to return to pool first
                if (_memoryPool.TryReturnToPool(buffer))
                {
                    _logger.LogDebug("Returned buffer to memory pool");
                    return;
                }
                // Unregister buffer
                UnregisterBuffer(buffer);
                // Free device memory
                await Task.Run(() =>
                {
                    var result = CudaRuntime.cudaFree(buffer.DevicePointer);
                    if (result != CudaError.Success)
                    {
                        _logger.LogWarning("Failed to free device memory: {Error}", CudaRuntime.GetErrorString(result));
                    }
                });
                lock (_allocationLock)
                {
                    _activeAllocations--;
                    _totalAllocatedBytes -= buffer.SizeInBytes;
                }
                _logger.LogDebug("Deallocated GPU buffer of {SizeInBytes} bytes", buffer.SizeInBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deallocate GPU buffer");
                // Don't throw in deallocation to avoid resource leaks
            }
        }
        /// Deallocates a generic GPU buffer through the interface.
        /// <param name="buffer">The buffer to deallocate.</param>
        public async Task DeallocateGenericBufferAsync(IGpuBuffer buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            try
            {
                // Unregister buffer using the device pointer
                UnregisterGenericBuffer(buffer);

                // Free device memory
                await Task.Run(() =>
                {
                    var result = CudaRuntime.cudaFree(buffer.DevicePointer);
                    if (result != CudaError.Success)
                    {
                        _logger.LogWarning("Failed to free device memory: {Error}", CudaRuntime.GetErrorString(result));
                    }
                });
                _logger.LogDebug("Deallocated generic GPU buffer of {SizeInBytes} bytes", buffer.SizeInBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deallocate generic GPU buffer");
                // Don't throw in deallocation to avoid resource leaks
            }
        }
        /// Gets current memory statistics.
        public MemoryStatistics GetMemoryStatistics()
        {
            lock (_allocationLock)
            {
                var (freeMemory, totalMemory) = GetDeviceMemoryInfo();
                return new MemoryStatistics
                {
                    TotalDeviceMemory = (long)totalMemory,
                    FreeDeviceMemory = (long)freeMemory,
                    UsedDeviceMemory = (long)(totalMemory - freeMemory),
                    TotalAllocatedBytes = _totalAllocatedBytes,
                    PeakAllocatedBytes = _peakAllocatedBytes,
                    TotalAllocations = _totalAllocations,
                    ActiveAllocations = _activeAllocations,
                    PoolStatistics = _memoryPool.GetStatistics()
                };
            }
        }
        /// Clears all memory pools and performs garbage collection.
        public async Task CollectGarbageAsync()
        {
            _logger.LogInformation("Performing GPU memory garbage collection");
            // Clear memory pools
            _memoryPool.ClearPools();
            // Force CUDA context synchronization
            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaDeviceSynchronize();
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to synchronize device during GC: {Error}",
                        CudaRuntime.GetErrorString(result));
                }
            });
            // Trigger host GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            _logger.LogInformation("GPU memory garbage collection completed");
        }
        #region Private Methods
        private void InitializeMemoryManager()
        {
            _logger.LogInformation("Initializing GPU memory manager");
            // Get initial memory info
            var (freeMemory, totalMemory) = GetDeviceMemoryInfo();
            _logger.LogInformation("Device memory: {FreeMemory}/{TotalMemory} MB available",
                freeMemory / (1024 * 1024), totalMemory / (1024 * 1024));
            // Initialize unified memory if supported
            if (SupportsUnifiedMemory())
            {
                _logger.LogInformation("Unified memory is supported and enabled");
            }
            // Initialize P2P if multiple devices are available
            _p2pManager.InitializeP2P();
        }
        private AllocationStrategy DetermineAllocationStrategy(long sizeInBytes, BufferType bufferType)
        {
            // Use unified memory for large buffers if supported
            if (sizeInBytes > 100 * 1024 * 1024 && SupportsUnifiedMemory())
                return AllocationStrategy.UnifiedMemory;
            // Use pinned memory for frequent host-device transfers
            if (bufferType == BufferType.Input || bufferType == BufferType.Output)
                return AllocationStrategy.PinnedMemory;
            // Default to device memory
            return AllocationStrategy.DeviceMemory;
        }
        private async Task<GpuBuffer<T>> AllocateDeviceMemoryAsync<T>(int elementCount) where T : unmanaged
        {
            var sizeInBytes = elementCount * Marshal.SizeOf<T>();
            return await Task.Run(() =>
            {
                var devicePointer = IntPtr.Zero;
                var result = CudaRuntime.cudaMalloc(ref devicePointer, (UIntPtr)sizeInBytes);
                if (result != CudaError.Success)
                {
                    throw new MemoryAllocationException(
                        $"Failed to allocate device memory: {CudaRuntime.GetErrorString(result)}");
                }
                return new GpuBuffer<T>(devicePointer, elementCount, MemoryType.Device);
            });
        }
        private async Task<GpuBuffer<T>> AllocateUnifiedMemoryAsync<T>(int elementCount) where T : unmanaged
        {
            return await _unifiedMemoryManager.AllocateAsync<T>(elementCount);
        }
        private async Task<GpuBuffer<T>> AllocatePinnedMemoryAsync<T>(int elementCount) where T : unmanaged
        {
            var sizeInBytes = elementCount * Marshal.SizeOf<T>();
            return await Task.Run(() =>
            {
                var hostPointer = IntPtr.Zero;
                var devicePointer = IntPtr.Zero;
                var result = CudaRuntime.cudaMallocHost(ref hostPointer, (UIntPtr)sizeInBytes);
                if (result != CudaError.Success)
                {
                    throw new MemoryAllocationException(
                        $"Failed to allocate pinned memory: {CudaRuntime.GetErrorString(result)}");
                }
                // Get device pointer for the pinned memory
                result = CudaRuntime.cudaHostGetDevicePointer(ref devicePointer, hostPointer, 0);
                if (result != CudaError.Success)
                {
                    CudaRuntime.cudaFreeHost(hostPointer);
                    throw new MemoryAllocationException(
                        $"Failed to get device pointer for pinned memory: {CudaRuntime.GetErrorString(result)}");
                }
                return new GpuBuffer<T>(devicePointer, elementCount, MemoryType.Pinned);
            });
        }
        private void RegisterBuffer<T>(GpuBuffer<T> buffer) where T : unmanaged
        {
            var bufferInfo = new GpuBufferInfo
            {
                Size = buffer.Size,
                SizeInBytes = buffer.SizeInBytes,
                MemoryType = buffer.MemoryType,
                AllocationTime = DateTime.UtcNow
            };
            _allocatedBuffers.TryAdd(buffer.DevicePointer, bufferInfo);
        }
        private void UnregisterBuffer<T>(GpuBuffer<T> buffer) where T : unmanaged
        {
            _allocatedBuffers.TryRemove(buffer.DevicePointer, out _);
        }
        private void UnregisterGenericBuffer(IGpuBuffer buffer)
        {
            _allocatedBuffers.TryRemove(buffer.DevicePointer, out _);
        }
        private (ulong freeMemory, ulong totalMemory) GetDeviceMemoryInfo()
        {
            var result = CudaRuntime.cudaMemGetInfo(out var freeMemory, out var totalMemory);
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to get device memory info: {Error}", CudaRuntime.GetErrorString(result));
                return (0, 0);
            }
            return (freeMemory, totalMemory);
        }
        private bool SupportsUnifiedMemory()
        {
            // Check if device supports unified memory
            var unifiedAddressing = 0;
            var result = CudaRuntime.cudaDeviceGetAttribute(
                ref unifiedAddressing,
                DotCompute.Backends.CUDA.Types.Native.CudaDeviceAttribute.UnifiedAddressing,
                0);
            return result == CudaError.Success && unifiedAddressing == 1;
        }
        #endregion
        public void Dispose()
        {
            if (_disposed)
                return;
            // Cleanup all allocations
            foreach (var kvp in _allocatedBuffers)
            {
                CudaRuntime.cudaFree(kvp.Key);
            }
            _allocatedBuffers.Clear();
            // Dispose managers
            _memoryPool.Dispose();
            _p2pManager.Dispose();
            _unifiedMemoryManager.Dispose();
            _disposed = true;
        }
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await CollectGarbageAsync();
                Dispose();
            }
        }
    }
    /// Memory context for kernel execution.
    public class MemoryContext : IAsyncDisposable
    {
        public required IGpuBuffer InputBuffer { get; set; }
        public required IGpuBuffer OutputBuffer { get; set; }
        public GpuBuffer<int>? OutputCountBuffer { get; set; }
        public required GpuMemoryManager MemoryManager { get; set; }

        public async ValueTask DisposeAsync()
        {
            if (InputBuffer != null)
                await MemoryManager.DeallocateGenericBufferAsync(InputBuffer);
            if (OutputBuffer != null)
                await MemoryManager.DeallocateGenericBufferAsync(OutputBuffer);
            if (OutputCountBuffer != null)
                await MemoryManager.DeallocateBufferAsync(OutputCountBuffer);
        }
    }
    /// Base interface for GPU buffers.
    public interface IGpuBuffer
    {
        IntPtr DevicePointer { get; }
        int Size { get; }
        int SizeInBytes { get; }
        MemoryType MemoryType { get; }
    }
    /// GPU buffer wrapper.
    public class GpuBuffer<T> : IGpuBuffer where T : unmanaged
    {
        public IntPtr DevicePointer { get; }
        public int Size { get; }
        public int SizeInBytes { get; }
        public MemoryType MemoryType { get; }
        public GpuBuffer(IntPtr devicePointer, int size, MemoryType memoryType)
        {
            DevicePointer = devicePointer;
            Size = size;
            SizeInBytes = size * Marshal.SizeOf<T>();
            MemoryType = memoryType;
        }
    }
    /// Buffer information for tracking.
    internal class GpuBufferInfo
    {
        public int Size { get; set; }
        public int SizeInBytes { get; set; }
        public MemoryType MemoryType { get; set; }
        public DateTime AllocationTime { get; set; }
    }
    /// Memory statistics.
    public class MemoryStatistics
    {
        public long TotalDeviceMemory { get; set; }
        public long FreeDeviceMemory { get; set; }
        public long UsedDeviceMemory { get; set; }
        public long TotalAllocatedBytes { get; set; }
        public long PeakAllocatedBytes { get; set; }
        public int TotalAllocations { get; set; }
        public int ActiveAllocations { get; set; }
        public MemoryPoolStatistics? PoolStatistics { get; set; }
    }
    /// Memory pool statistics.
    public class MemoryPoolStatistics
    {
        public int TotalPools { get; set; }
        public int ActivePools { get; set; }
        public long TotalPooledMemory { get; set; }
        public double HitRate { get; set; }
    }
    /// Memory types.
    public enum MemoryType
    {
        Device,
        Host,
        Pinned,
        Unified
    }
    /// Buffer types for optimization hints.
    public enum BufferType
    {
        General,
        Input,
        Output,
        Counter,
        Temporary
    }
    /// Allocation strategies.
    internal enum AllocationStrategy
    {
        DeviceMemory,
        UnifiedMemory,
        PinnedMemory
    }
    /// Memory allocation exception.
    public class MemoryAllocationException : Exception
    {
        public MemoryAllocationException(string message) : base(message) { }
        public MemoryAllocationException(string message, Exception innerException) : base(message, innerException) { }
    }
    /// Memory transfer exception.
    public class MemoryTransferException : Exception
    {
        public MemoryTransferException(string message) : base(message) { }
        public MemoryTransferException(string message, Exception innerException) : base(message, innerException) { }
    }
    #region Helper Classes
    /// Memory pool for efficient allocation/deallocation.
    internal class MemoryPool : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, Queue<IntPtr>> _pools;
        private bool _disposed;
        public MemoryPool(CudaContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
            _pools = new ConcurrentDictionary<int, Queue<IntPtr>>();
        }
        public bool TryAllocateFromPool<T>(int elementCount, out GpuBuffer<T> buffer) where T : unmanaged
        {
            buffer = null!;
            var sizeInBytes = elementCount * Marshal.SizeOf<T>();
            if (_pools.TryGetValue(sizeInBytes, out var pool) && pool.TryDequeue(out var pointer))
            {
                buffer = new GpuBuffer<T>(pointer, elementCount, MemoryType.Device);
                return true;
            }
            return false;
        }
        public bool TryReturnToPool<T>(GpuBuffer<T> buffer) where T : unmanaged
        {
            if (_disposed || buffer.MemoryType != MemoryType.Device)
                return false;
            var pool = _pools.GetOrAdd(buffer.SizeInBytes, _ => new Queue<IntPtr>());
            if (pool.Count < 10) // Limit pool size
            {
                pool.Enqueue(buffer.DevicePointer);
                return true;
            }
            return false;
        }
        public void ClearPools()
        {
            foreach (var pool in _pools.Values)
            {
                while (pool.TryDequeue(out var pointer))
                {
                    CudaRuntime.cudaFree(pointer);
                }
            }
            _pools.Clear();
        }
        public MemoryPoolStatistics GetStatistics()
        {
            var totalPools = _pools.Count;
            var activePools = _pools.Values.Count(p => p.Count > 0);
            var totalPooledMemory = _pools.Values.Sum(p => p.Count);
            return new MemoryPoolStatistics
            {
                TotalPools = totalPools,
                ActivePools = activePools,
                TotalPooledMemory = totalPooledMemory,
                HitRate = 0.0 // TODO: Implement hit rate tracking
            };
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                ClearPools();
                _disposed = true;
            }
        }
    }
    /// Peer-to-peer transfer manager.
    internal class P2PTransferManager : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly HashSet<(int, int)> _enabledP2PPairs;
        private bool _disposed;
        public P2PTransferManager(CudaContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
            _enabledP2PPairs = [];
        }
        public void InitializeP2P()
        {
            // Initialize P2P access between devices
            // Implementation depends on device enumeration
        }
        public async Task TransferAsync<T>(
            GpuBuffer<T> sourceBuffer,
            GpuBuffer<T> destinationBuffer,
            int elementCount,
            int sourceDeviceId,
            int destinationDeviceId,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var sizeInBytes = elementCount * Marshal.SizeOf<T>();
            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaMemcpyPeer(
                    destinationBuffer.DevicePointer,
                    destinationDeviceId,
                    sourceBuffer.DevicePointer,
                    sourceDeviceId,
                    (UIntPtr)sizeInBytes);
                if (result != CudaError.Success)
                {
                    throw new MemoryTransferException(
                        $"P2P transfer failed: {CudaRuntime.GetErrorString(result)}");
                }
            }, cancellationToken);
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                // No specific cleanup needed for P2P transfers
                _disposed = true;
            }
        }
    }
    /// Unified memory manager.
    public class UnifiedMemoryManager : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private bool _disposed;

        public UnifiedMemoryManager(CudaContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }
        public async Task<GpuBuffer<T>> AllocateAsync<T>(int elementCount) where T : unmanaged
        {
            var sizeInBytes = elementCount * Marshal.SizeOf<T>();
            return await Task.Run(() =>
            {
                var result = CudaRuntime.AllocateManaged(out var pointer, (UIntPtr)sizeInBytes, 0);
                if (result != CudaError.Success)
                {
                    throw new MemoryAllocationException(
                        $"Failed to allocate unified memory: {CudaRuntime.GetErrorString(result)}");
                }
                return new GpuBuffer<T>(pointer, elementCount, MemoryType.Unified);
            });
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                // No specific cleanup needed for unified memory
                _disposed = true;
            }
        }
    }
    #endregion
    /// CUDA device attribute enumeration.
    internal enum CudaDeviceAttribute
    {
        UnifiedAddressing = 41
    }
}
}
