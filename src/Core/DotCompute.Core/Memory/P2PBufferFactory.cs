// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory
{

    /// <summary>
    /// P2P-aware buffer factory that creates optimized buffers for multi-GPU scenarios.
    /// Handles direct P2P transfers, host-mediated transfers, and memory pooling.
    /// </summary>
    public sealed class P2PBufferFactory : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly P2PCapabilityDetector _capabilityDetector;
        private readonly ConcurrentDictionary<string, DeviceBufferPool> _devicePools;
        private readonly ConcurrentDictionary<string, P2PConnectionState> _activeP2PConnections;
        private readonly SemaphoreSlim _factorySemaphore;
        private bool _disposed;

        public P2PBufferFactory(
            ILogger logger,
            P2PCapabilityDetector capabilityDetector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
            _devicePools = new ConcurrentDictionary<string, DeviceBufferPool>();
            _activeP2PConnections = new ConcurrentDictionary<string, P2PConnectionState>();
            _factorySemaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Creates a P2P-optimized buffer on the target device with data from the source buffer.
        /// </summary>
        public async ValueTask<P2PBuffer<T>> CreateP2PBufferAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IAccelerator targetDevice,
            P2PBufferOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);

            ArgumentNullException.ThrowIfNull(targetDevice);

            options ??= P2PBufferOptions.Default;

            var sourceDevice = sourceBuffer.Accelerator;
            var transferStrategy = await _capabilityDetector.GetOptimalTransferStrategyAsync(
                sourceDevice, targetDevice, sourceBuffer.SizeInBytes, cancellationToken);

            var p2pBuffer = await CreateP2PBufferInternalAsync<T>(
                sourceDevice, targetDevice, BufferHelpers.GetElementCount(sourceBuffer), transferStrategy, options, cancellationToken);

            // Transfer data using the optimal strategy
            await TransferDataToP2PBufferAsync(sourceBuffer, p2pBuffer, transferStrategy, cancellationToken);

            return p2pBuffer;
        }

        /// <summary>
        /// Creates an empty P2P buffer with specified parameters.
        /// </summary>
        public async ValueTask<P2PBuffer<T>> CreateEmptyP2PBufferAsync<T>(
            IAccelerator device,
            int length,
            P2PBufferOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(device);

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

            options ??= P2PBufferOptions.Default;

            var devicePool = await GetOrCreateDevicePoolAsync(device, cancellationToken);
            var memoryBuffer = await devicePool.AllocateAsync(length * Unsafe.SizeOf<T>(), cancellationToken);

            return new P2PBuffer<T>(
                memoryBuffer,
                device,
                length,
                options.EnableP2POptimizations,
                _logger);
        }

        /// <summary>
        /// Creates a buffer slice optimized for P2P operations.
        /// </summary>
        public async ValueTask<P2PBuffer<T>> CreateP2PBufferSliceAsync<T>(
            P2PBuffer<T> sourceBuffer,
            IAccelerator targetDevice,
            int startIndex,
            int elementCount,
            P2PBufferOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);

            ArgumentNullException.ThrowIfNull(targetDevice);

            ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementCount);

            if (startIndex + elementCount > BufferHelpers.GetElementCount(sourceBuffer))
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount), "Slice extends beyond buffer bounds");
            }

            options ??= P2PBufferOptions.Default;

            // Create target buffer
            var targetBuffer = await CreateEmptyP2PBufferAsync<T>(targetDevice, elementCount, options, cancellationToken);

            // Transfer slice data
            await sourceBuffer.CopyToAsync(startIndex, targetBuffer, 0, elementCount, cancellationToken);

            return targetBuffer;
        }

        /// <summary>
        /// Enables P2P connection between two devices and manages connection state.
        /// </summary>
        public async ValueTask<bool> EstablishP2PConnectionAsync(
            IAccelerator device1,
            IAccelerator device2,
            CancellationToken cancellationToken = default)
        {
            var connectionKey = GetConnectionKey(device1.Info.Id, device2.Info.Id);

            // Check if connection already exists
            if (_activeP2PConnections.TryGetValue(connectionKey, out var existingConnection))
            {
                return existingConnection.IsActive;
            }

            await _factorySemaphore.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_activeP2PConnections.TryGetValue(connectionKey, out existingConnection))
                {
                    return existingConnection.IsActive;
                }

                var enableResult = await _capabilityDetector.EnableP2PAccessAsync(device1, device2, cancellationToken);

                var connectionState = new P2PConnectionState
                {
                    Device1Id = device1.Info.Id,
                    Device2Id = device2.Info.Id,
                    IsActive = enableResult.Success,
                    Capability = enableResult.Capability,
                    EstablishedAt = DateTime.UtcNow,
                    TransferCount = 0,
                    TotalBytesTransferred = 0
                };

                _activeP2PConnections[connectionKey] = connectionState;

                if (enableResult.Success)
                {
                    _logger.LogInformation("P2P connection established between {Device1} and {Device2}",
                        device1.Info.Name, device2.Info.Name);
                }
                else
                {
                    _logger.LogWarning("Failed to establish P2P connection between {Device1} and {Device2}: {Error}",
                        device1.Info.Name, device2.Info.Name, enableResult.ErrorMessage);
                }

                return enableResult.Success;
            }
            finally
            {
                _ = _factorySemaphore.Release();
            }
        }

        /// <summary>
        /// Gets P2P connection statistics.
        /// </summary>
        public P2PConnectionStatistics GetConnectionStatistics()
        {
            var totalConnections = _activeP2PConnections.Count;
            var activeConnections = _activeP2PConnections.Count(kvp => kvp.Value.IsActive);
            var totalTransfers = _activeP2PConnections.Values.Sum(c => c.TransferCount);
            var totalBytes = _activeP2PConnections.Values.Sum(c => c.TotalBytesTransferred);

            return new P2PConnectionStatistics
            {
                TotalConnections = totalConnections,
                ActiveConnections = activeConnections,
                TotalTransfers = totalTransfers,
                TotalBytesTransferred = totalBytes,
                AverageTransferSize = totalTransfers > 0 ? totalBytes / totalTransfers : 0,
                ConnectionDetails = _activeP2PConnections.Values.ToList()
            };
        }

        /// <summary>
        /// Gets buffer pool statistics for all devices.
        /// </summary>
        public IReadOnlyList<DeviceBufferPoolStatistics> GetBufferPoolStatistics()
        {
            return _devicePools.Values
                .Select(pool => pool.GetStatistics())
                .ToList();
        }

        /// <summary>
        /// Internal method to create P2P buffer with specific transfer strategy.
        /// </summary>
        private async ValueTask<P2PBuffer<T>> CreateP2PBufferInternalAsync<T>(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            int length,
            TransferStrategy strategy,
            P2PBufferOptions options,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var targetPool = await GetOrCreateDevicePoolAsync(targetDevice, cancellationToken);
            var sizeInBytes = length * Unsafe.SizeOf<T>();

            IMemoryBuffer memoryBuffer;

            switch (strategy.Type)
            {
                case TransferType.DirectP2P:
                    // Ensure P2P connection is established
                    _ = await EstablishP2PConnectionAsync(sourceDevice, targetDevice, cancellationToken);
                    memoryBuffer = await targetPool.AllocateAsync(sizeInBytes, cancellationToken);
                    break;

                case TransferType.HostMediated:
                    // Use standard allocation for host-mediated transfers
                    memoryBuffer = await targetPool.AllocateAsync(sizeInBytes, cancellationToken);
                    break;

                case TransferType.Streaming:
                    // Allocate with streaming-friendly options
                    memoryBuffer = await targetPool.AllocateStreamingAsync(sizeInBytes, strategy.ChunkSize, cancellationToken);
                    break;

                case TransferType.MemoryMapped:
                    // Use memory-mapped allocation for very large buffers
                    memoryBuffer = await targetPool.AllocateMemoryMappedAsync(sizeInBytes, cancellationToken);
                    break;

                default:
                    memoryBuffer = await targetPool.AllocateAsync(sizeInBytes, cancellationToken);
                    break;
            }

            return new P2PBuffer<T>(
                memoryBuffer,
                targetDevice,
                length,
                strategy.Type == TransferType.DirectP2P,
                _logger);
        }

        /// <summary>
        /// Transfers data to P2P buffer using the optimal strategy.
        /// </summary>
        private async ValueTask TransferDataToP2PBufferAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            P2PBuffer<T> targetBuffer,
            TransferStrategy strategy,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var connectionKey = GetConnectionKey(sourceBuffer.Accelerator.Info.Id, targetBuffer.Accelerator.Info.Id);

            try
            {
                var startTime = DateTime.UtcNow;

                switch (strategy.Type)
                {
                    case TransferType.DirectP2P:
                        await TransferDirectP2PAsync(sourceBuffer, targetBuffer, strategy, cancellationToken);
                        break;

                    case TransferType.HostMediated:
                        await TransferHostMediatedAsync(sourceBuffer, targetBuffer, strategy, cancellationToken);
                        break;

                    case TransferType.Streaming:
                        await TransferStreamingAsync(sourceBuffer, targetBuffer, strategy, cancellationToken);
                        break;

                    case TransferType.MemoryMapped:
                        await TransferMemoryMappedAsync(sourceBuffer, targetBuffer, strategy, cancellationToken);
                        break;

                    default:
                        await TransferHostMediatedAsync(sourceBuffer, targetBuffer, strategy, cancellationToken);
                        break;
                }

                var duration = DateTime.UtcNow - startTime;

                // Update connection statistics
                if (_activeP2PConnections.TryGetValue(connectionKey, out var connection))
                {
                    // Use reflection or direct field access since properties may be read-only
                    // This is a simplified approach - in production would use proper thread-safe update
                    connection.TransferCount++;
                    connection.TotalBytesTransferred += sourceBuffer.SizeInBytes;
                }

                _logger.LogDebug("P2P transfer completed in {Duration}ms: {Bytes} bytes from {Source} to {Target}",
                    duration.TotalMilliseconds, sourceBuffer.SizeInBytes,
                    sourceBuffer.Accelerator.Info.Name, targetBuffer.Accelerator.Info.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "P2P transfer failed from {Source} to {Target}",
                    sourceBuffer.Accelerator.Info.Name, targetBuffer.Accelerator.Info.Name);
                throw;
            }
        }

        /// <summary>
        /// Performs direct P2P transfer between GPU devices.
        /// </summary>
        private static async ValueTask TransferDirectP2PAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            P2PBuffer<T> target,
            TransferStrategy strategy,
            CancellationToken cancellationToken) where T : unmanaged
            // Direct P2P copy - this would use device-specific APIs
            // For CUDA: cudaMemcpyPeer
            // For ROCm: hipMemcpyPeer

            => await source.CopyToAsync(target, cancellationToken);

        /// <summary>
        /// Performs host-mediated transfer via CPU memory.
        /// </summary>
        private static async ValueTask TransferHostMediatedAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        P2PBuffer<T> target,
        TransferStrategy strategy,
        CancellationToken cancellationToken) where T : unmanaged
        {
            // Create intermediate host buffer
            var hostData = new T[BufferHelpers.GetElementCount(source)];

            // Copy from source to host  
            await source.CopyToHostAsync<T>(hostData, 0, cancellationToken);

            // Copy from host to target
            await target.CopyFromHostAsync<T>(hostData, 0, cancellationToken);
        }

        /// <summary>
        /// Performs streaming transfer with chunking for large datasets.
        /// </summary>
        private async ValueTask TransferStreamingAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            P2PBuffer<T> target,
            TransferStrategy strategy,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var elementSize = Unsafe.SizeOf<T>();
            var elementsPerChunk = strategy.ChunkSize / elementSize;
            var totalElements = BufferHelpers.GetElementCount(source);
            var chunks = (totalElements + elementsPerChunk - 1) / elementsPerChunk;

            var chunkTasks = new List<Task>();

            for (var chunkIndex = 0; chunkIndex < chunks; chunkIndex++)
            {
                var startElement = chunkIndex * elementsPerChunk;
                var chunkElements = Math.Min(elementsPerChunk, totalElements - startElement);

                var chunkTask = TransferChunkAsync(source, target, startElement, chunkElements, cancellationToken);
                chunkTasks.Add(chunkTask);

                // Limit concurrent chunks to avoid memory pressure
                if (chunkTasks.Count >= Environment.ProcessorCount)
                {
                    await Task.WhenAll(chunkTasks);
                    chunkTasks.Clear();
                }
            }

            if (chunkTasks.Count > 0)
            {
                await Task.WhenAll(chunkTasks);
            }
        }

        /// <summary>
        /// Transfers a single chunk of data.
        /// </summary>
        private static async Task TransferChunkAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            P2PBuffer<T> target,
            int startElement,
            int elementCount,
            CancellationToken cancellationToken) where T : unmanaged => await source.CopyToAsync(startElement, target, startElement, elementCount, cancellationToken);

        /// <summary>
        /// Performs memory-mapped transfer for very large datasets.
        /// </summary>
        private static async ValueTask TransferMemoryMappedAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        P2PBuffer<T> target,
        TransferStrategy strategy,
        CancellationToken cancellationToken) where T : unmanaged
        {
            // For very large transfers, use memory-mapped files as intermediate storage
            var tempFile = Path.GetTempFileName();
            try
            {
                // Copy source to memory-mapped file
                var hostData = new T[BufferHelpers.GetElementCount(source)];
                await source.CopyToHostAsync<T>(hostData, 0, cancellationToken);
                await File.WriteAllBytesAsync(tempFile, System.Runtime.InteropServices.MemoryMarshal.AsBytes(hostData.AsSpan()).ToArray(), cancellationToken);

                // Copy from memory-mapped file to target
                var fileData = await File.ReadAllBytesAsync(tempFile, cancellationToken);
                var targetData = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(fileData);
                await target.CopyFromHostAsync<T>(targetData.ToArray(), 0, cancellationToken);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Gets or creates a device buffer pool.
        /// </summary>
        private async ValueTask<DeviceBufferPool> GetOrCreateDevicePoolAsync(
            IAccelerator device,
            CancellationToken cancellationToken)
        {
            if (_devicePools.TryGetValue(device.Info.Id, out var existingPool))
            {
                return existingPool;
            }

            await _factorySemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_devicePools.TryGetValue(device.Info.Id, out existingPool))
                {
                    return existingPool;
                }

                var deviceCapabilities = await _capabilityDetector.GetDeviceCapabilitiesAsync(device, cancellationToken);
                var pool = new DeviceBufferPool(device, deviceCapabilities, _logger);

                _devicePools[device.Info.Id] = pool;
                return pool;
            }
            finally
            {
                _ = _factorySemaphore.Release();
            }
        }

        private static string GetConnectionKey(string device1Id, string device2Id)
        {
            var ids = new[] { device1Id, device2Id }.OrderBy(id => id).ToArray();
            return $"{ids[0]}<->{ids[1]}";
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Dispose all device pools
            var disposeTasks = _devicePools.Values.Select(pool => pool.DisposeAsync());
            foreach (var task in disposeTasks)
            {
                await task;
            }

            _devicePools.Clear();
            _activeP2PConnections.Clear();
            _factorySemaphore.Dispose();
        }
    }

    /// <summary>
    /// P2P buffer options for configuration.
    /// </summary>
    public sealed class P2PBufferOptions
    {
        public static P2PBufferOptions Default => new();

        public bool EnableP2POptimizations { get; init; } = true;
        public bool EnableMemoryPooling { get; init; } = true;
        public bool EnableAsyncTransfers { get; init; } = true;
        public int PreferredChunkSizeBytes { get; init; } = 4 * 1024 * 1024; // 4MB
    }

    /// <summary>
    /// P2P connection state tracking.
    /// </summary>
    public sealed class P2PConnectionState
    {
        public required string Device1Id { get; init; }
        public required string Device2Id { get; init; }
        public required bool IsActive { get; init; }
        public P2PConnectionCapability? Capability { get; init; }
        public DateTime EstablishedAt { get; init; }
        public long TransferCount { get; set; }
        public long TotalBytesTransferred { get; set; }
    }

    /// <summary>
    /// P2P connection statistics.
    /// </summary>
    public sealed class P2PConnectionStatistics
    {
        public int TotalConnections { get; init; }
        public int ActiveConnections { get; init; }
        public long TotalTransfers { get; init; }
        public long TotalBytesTransferred { get; init; }
        public long AverageTransferSize { get; init; }
        public IReadOnlyList<P2PConnectionState> ConnectionDetails { get; init; } = [];
    }

    /// <summary>
    /// Device buffer pool statistics.
    /// </summary>
    public sealed class DeviceBufferPoolStatistics
    {
        public required string DeviceId { get; init; }
        public required string DeviceName { get; init; }
        public long TotalAllocatedBytes { get; init; }
        public long AvailableBytes { get; init; }
        public int ActiveBuffers { get; init; }
        public long AllocationCount { get; init; }
        public long DeallocationCount { get; init; }
        public double PoolEfficiency { get; init; }
    }
}
