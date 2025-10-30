// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Core.Logging;
using DotCompute.Core.Memory.Utilities;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory
{

    /// <summary>
    /// P2P-aware buffer factory that creates optimized buffers for multi-GPU scenarios.
    /// Handles direct P2P transfers, host-mediated transfers, and memory pooling.
    /// </summary>
    public sealed class P2PBufferFactory(
        ILogger logger,
        P2PCapabilityDetector capabilityDetector) : IAsyncDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
#pragma warning disable CA2213 // Disposable fields should be disposed - Injected dependency, not owned by this class
        private readonly P2PCapabilityDetector _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
#pragma warning restore CA2213
        private readonly ConcurrentDictionary<string, DeviceBufferPool> _devicePools = new();
        private readonly ConcurrentDictionary<string, P2PConnectionState> _activeP2PConnections = new();
        private readonly SemaphoreSlim _factorySemaphore = new(1, 1);
        private bool _disposed;

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
                memoryBuffer as IUnifiedMemoryBuffer ?? throw new InvalidCastException("Buffer is not compatible"),
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
                    _logger.LogInfoMessage($"P2P connection established between {device1.Info.Name} and {device2.Info.Name}");
                }
                else
                {
                    _logger.LogWarningMessage($"Failed to establish P2P connection between {device1.Info.Name} and {device2.Info.Name}: {enableResult.ErrorMessage}");
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

            IUnifiedMemoryBuffer<byte> memoryBuffer;

            switch (strategy.Type)
            {
                case TransferType.DirectP2P:
                    // Ensure P2P connection is established
                    _ = await EstablishP2PConnectionAsync(sourceDevice, targetDevice, cancellationToken);
                    var buffer = await targetPool.AllocateAsync(sizeInBytes, cancellationToken);
                    memoryBuffer = buffer as IUnifiedMemoryBuffer<byte> ?? throw new InvalidOperationException("Pool returned non-byte buffer");
                    break;

                case TransferType.HostMediated:
                    // Use standard allocation for host-mediated transfers
                    var hostBuffer = await targetPool.AllocateAsync(sizeInBytes, cancellationToken);
                    memoryBuffer = hostBuffer as IUnifiedMemoryBuffer<byte> ?? throw new InvalidOperationException("Pool returned non-byte buffer");
                    break;

                case TransferType.Streaming:
                    // Allocate with streaming-friendly options
                    var streamBuffer = await targetPool.AllocateStreamingAsync(sizeInBytes, strategy.ChunkSize, cancellationToken);
                    memoryBuffer = streamBuffer as IUnifiedMemoryBuffer<byte> ?? throw new InvalidOperationException("Pool returned non-byte buffer");
                    break;

                case TransferType.MemoryMapped:
                    // Use memory-mapped allocation for very large buffers
                    var mappedBuffer = await targetPool.AllocateMemoryMappedAsync(sizeInBytes, cancellationToken);
                    memoryBuffer = mappedBuffer as IUnifiedMemoryBuffer<byte> ?? throw new InvalidOperationException("Pool returned non-byte buffer");
                    break;

                default:
                    var defaultBuffer = await targetPool.AllocateAsync(sizeInBytes, cancellationToken);
                    memoryBuffer = defaultBuffer as IUnifiedMemoryBuffer<byte> ?? throw new InvalidOperationException("Pool returned non-byte buffer");
                    break;
            }

            return new P2PBuffer<T>(
                memoryBuffer as IUnifiedMemoryBuffer ?? throw new InvalidCastException("Buffer is not compatible"),
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

                _logger.LogDebugMessage($"P2P transfer completed in {duration.TotalMilliseconds}ms: {sourceBuffer.SizeInBytes} bytes from {sourceBuffer.Accelerator.Info.Name} to {targetBuffer.Accelerator.Info.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"P2P transfer failed from {sourceBuffer.Accelerator.Info.Name} to {targetBuffer.Accelerator.Info.Name}");
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
            await source.CopyToAsync(hostData.AsMemory(), cancellationToken);

            // Copy from host to target
            await target.CopyFromAsync(hostData.AsMemory(), cancellationToken);
        }

        /// <summary>
        /// Performs streaming transfer with chunking for large datasets.
        /// </summary>
        private static async ValueTask TransferStreamingAsync<T>(
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
                await source.CopyToAsync(hostData.AsMemory(), cancellationToken);
                await File.WriteAllBytesAsync(tempFile, global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(hostData.AsSpan()).ToArray(), cancellationToken);

                // Copy from memory-mapped file to target
                var fileData = await File.ReadAllBytesAsync(tempFile, cancellationToken);
                var targetData = global::System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(fileData);
                await target.CopyFromAsync(targetData.ToArray().AsMemory(), cancellationToken);
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
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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
        /// <summary>
        /// Gets or sets the default.
        /// </summary>
        /// <value>The default.</value>
        public static P2PBufferOptions Default => new();
        /// <summary>
        /// Gets or sets the enable p2 p optimizations.
        /// </summary>
        /// <value>The enable p2 p optimizations.</value>

        public bool EnableP2POptimizations { get; init; } = true;
        /// <summary>
        /// Gets or sets the enable memory pooling.
        /// </summary>
        /// <value>The enable memory pooling.</value>
        public bool EnableMemoryPooling { get; init; } = true;
        /// <summary>
        /// Gets or sets the enable async transfers.
        /// </summary>
        /// <value>The enable async transfers.</value>
        public bool EnableAsyncTransfers { get; init; } = true;
        /// <summary>
        /// Gets or sets the preferred chunk size bytes.
        /// </summary>
        /// <value>The preferred chunk size bytes.</value>
        public int PreferredChunkSizeBytes { get; init; } = 4 * 1024 * 1024; // 4MB
    }

    /// <summary>
    /// P2P connection state tracking.
    /// </summary>
    public sealed class P2PConnectionState
    {
        /// <summary>
        /// Gets or sets the device1 identifier.
        /// </summary>
        /// <value>The device1 id.</value>
        public required string Device1Id { get; init; }
        /// <summary>
        /// Gets or sets the device2 identifier.
        /// </summary>
        /// <value>The device2 id.</value>
        public required string Device2Id { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether active.
        /// </summary>
        /// <value>The is active.</value>
        public required bool IsActive { get; init; }
        /// <summary>
        /// Gets or sets the capability.
        /// </summary>
        /// <value>The capability.</value>
        public P2PConnectionCapability? Capability { get; init; }
        /// <summary>
        /// Gets or sets the established at.
        /// </summary>
        /// <value>The established at.</value>
        public DateTime EstablishedAt { get; init; }
        /// <summary>
        /// Gets or sets the transfer count.
        /// </summary>
        /// <value>The transfer count.</value>
        public long TransferCount { get; set; }
        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        /// <value>The total bytes transferred.</value>
        public long TotalBytesTransferred { get; set; }
    }

    /// <summary>
    /// P2P connection statistics.
    /// </summary>
    public sealed class P2PConnectionStatistics
    {
        /// <summary>
        /// Gets or sets the total connections.
        /// </summary>
        /// <value>The total connections.</value>
        public int TotalConnections { get; init; }
        /// <summary>
        /// Gets or sets the active connections.
        /// </summary>
        /// <value>The active connections.</value>
        public int ActiveConnections { get; init; }
        /// <summary>
        /// Gets or sets the total transfers.
        /// </summary>
        /// <value>The total transfers.</value>
        public long TotalTransfers { get; init; }
        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        /// <value>The total bytes transferred.</value>
        public long TotalBytesTransferred { get; init; }
        /// <summary>
        /// Gets or sets the average transfer size.
        /// </summary>
        /// <value>The average transfer size.</value>
        public long AverageTransferSize { get; init; }
        /// <summary>
        /// Gets or sets the connection details.
        /// </summary>
        /// <value>The connection details.</value>
        public IReadOnlyList<P2PConnectionState> ConnectionDetails { get; init; } = [];
    }

    /// <summary>
    /// Device buffer pool statistics.
    /// </summary>
    public sealed class DeviceBufferPoolStatistics
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device id.</value>
        public required string DeviceId { get; init; }
        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        /// <value>The device name.</value>
        public required string DeviceName { get; init; }
        /// <summary>
        /// Gets or sets the total allocated bytes.
        /// </summary>
        /// <value>The total allocated bytes.</value>
        public long TotalAllocatedBytes { get; init; }
        /// <summary>
        /// Gets or sets the available bytes.
        /// </summary>
        /// <value>The available bytes.</value>
        public long AvailableBytes { get; init; }
        /// <summary>
        /// Gets or sets the active buffers.
        /// </summary>
        /// <value>The active buffers.</value>
        public int ActiveBuffers { get; init; }
        /// <summary>
        /// Gets or sets the allocation count.
        /// </summary>
        /// <value>The allocation count.</value>
        public long AllocationCount { get; init; }
        /// <summary>
        /// Gets or sets the deallocation count.
        /// </summary>
        /// <value>The deallocation count.</value>
        public long DeallocationCount { get; init; }
        /// <summary>
        /// Gets or sets the pool efficiency.
        /// </summary>
        /// <value>The pool efficiency.</value>
        public double PoolEfficiency { get; init; }
    }
}
