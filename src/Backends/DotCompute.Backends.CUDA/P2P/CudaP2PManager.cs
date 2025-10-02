// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Models;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.P2P
{

    /// <summary>
    /// Advanced CUDA Peer-to-Peer (P2P) manager for multi-GPU operations
    /// </summary>
    public sealed class CudaP2PManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<(int, int), CudaP2PConnection> _connections;
        private readonly ConcurrentDictionary<int, CudaDeviceInfo> _devices;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly Timer _monitoringTimer;
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the CudaP2PManager class.
        /// </summary>
        /// <param name="logger">The logger.</param>

        public CudaP2PManager(ILogger<CudaP2PManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connections = new ConcurrentDictionary<(int, int), CudaP2PConnection>();
            _devices = new ConcurrentDictionary<int, CudaDeviceInfo>();
            _connectionSemaphore = new SemaphoreSlim(1, 1);

            Initialize();

            // Set up monitoring timer
            _monitoringTimer = new Timer(MonitorConnections, null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger.LogInfoMessage("CUDA P2P Manager initialized for multi-GPU operations");
        }

        /// <summary>
        /// Discovers and initializes P2P capabilities between devices
        /// </summary>
        public async Task<CudaP2PTopology> DiscoverTopologyAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var deviceCount = GetDeviceCount();
                var topology = new CudaP2PTopology { DeviceCount = deviceCount };

                // Discover all devices
                for (var i = 0; i < deviceCount; i++)
                {
                    var deviceInfo = await GetDeviceInfoAsync(i).ConfigureAwait(false);
                    _devices[i] = deviceInfo;
                    topology.Devices.Add(deviceInfo);
                }

                // Test P2P connectivity between all device pairs
                for (var srcDevice = 0; srcDevice < deviceCount; srcDevice++)
                {
                    for (var dstDevice = 0; dstDevice < deviceCount; dstDevice++)
                    {
                        if (srcDevice != dstDevice)
                        {
                            var canAccess = await TestP2PAccessAsync(srcDevice, dstDevice).ConfigureAwait(false);

                            if (canAccess)
                            {
                                var connection = new CudaP2PConnection
                                {
                                    SourceDevice = srcDevice,
                                    DestinationDevice = dstDevice,
                                    IsEnabled = false,
                                    DiscoveredAt = DateTimeOffset.UtcNow
                                };

                                // Measure P2P bandwidth
                                connection.BandwidthGBps = await MeasureP2PBandwidthAsync(srcDevice, dstDevice)
                                    .ConfigureAwait(false);

                                _connections[(srcDevice, dstDevice)] = connection;
                                topology.Connections.Add(connection);

                                _logger.LogDebugMessage($"P2P connection available: {srcDevice} -> {dstDevice} ({connection.BandwidthGBps} GB/s)");
                            }
                        }
                    }
                }

                topology.IsFullyConnected = IsTopologyFullyConnected(topology);
                topology.OptimalTransferPaths = CalculateOptimalPaths(topology);

                _logger.LogInfoMessage($"Discovered P2P topology: {deviceCount} devices, {topology.Connections.Count} connections, fully connected: {topology.IsFullyConnected}");

                return topology;
            }
            finally
            {
                _ = _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Enables P2P access between two devices
        /// </summary>
        public async Task<bool> EnableP2PAccessAsync(int sourceDevice, int destinationDevice,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (sourceDevice == destinationDevice)
            {
                throw new ArgumentException("Source and destination devices cannot be the same");
            }

            var connectionKey = (sourceDevice, destinationDevice);
            if (!_connections.TryGetValue(connectionKey, out var connection))
            {
                // Test if connection is possible
                var canAccess = await TestP2PAccessAsync(sourceDevice, destinationDevice).ConfigureAwait(false);
                if (!canAccess)
                {
                    return false;
                }

                connection = new CudaP2PConnection
                {
                    SourceDevice = sourceDevice,
                    DestinationDevice = destinationDevice,
                    IsEnabled = false,
                    DiscoveredAt = DateTimeOffset.UtcNow
                };
                _connections[connectionKey] = connection;
            }

            if (connection.IsEnabled)
            {
                return true; // Already enabled
            }

            try
            {
                // Set source device as current
                var result = CudaRuntime.cudaSetDevice(sourceDevice);
                CudaRuntime.CheckError(result, $"setting device {sourceDevice}");

                // Enable peer access
                result = CudaRuntime.cudaDeviceEnablePeerAccess(destinationDevice, 0);
                if (result == CudaError.PeerAccessAlreadyEnabled)
                {
                    // Already enabled, that's fine
                    result = CudaError.Success;
                }
                CudaRuntime.CheckError(result, $"enabling P2P access from {sourceDevice} to {destinationDevice}");

                connection.IsEnabled = true;
                connection.EnabledAt = DateTimeOffset.UtcNow;

                _logger.LogInfoMessage($"Enabled P2P access: {sourceDevice} -> {destinationDevice}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to enable P2P access: {sourceDevice} -> {destinationDevice}");
                return false;
            }
        }

        /// <summary>
        /// Disables P2P access between two devices
        /// </summary>
        public async Task<bool> DisableP2PAccessAsync(int sourceDevice, int destinationDevice,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            ThrowIfDisposed();

            var connectionKey = (sourceDevice, destinationDevice);
            if (!_connections.TryGetValue(connectionKey, out var connection) || !connection.IsEnabled)
            {
                return true; // Already disabled or doesn't exist
            }

            try
            {
                var result = CudaRuntime.cudaSetDevice(sourceDevice);
                CudaRuntime.CheckError(result, $"setting device {sourceDevice}");

                result = CudaRuntime.cudaDeviceDisablePeerAccess(destinationDevice);
                CudaRuntime.CheckError(result, $"disabling P2P access from {sourceDevice} to {destinationDevice}");

                connection.IsEnabled = false;
                connection.DisabledAt = DateTimeOffset.UtcNow;

                _logger.LogInfoMessage($"Disabled P2P access: {sourceDevice} -> {destinationDevice}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to disable P2P access: {sourceDevice} -> {destinationDevice}");
                return false;
            }
        }

        /// <summary>
        /// Performs a direct P2P memory transfer between devices
        /// </summary>
        public async Task<CudaP2PTransferResult> TransferAsync(
            IUnifiedMemoryBuffer sourceBuffer,
            int sourceDevice,
            IUnifiedMemoryBuffer destinationBuffer,
            int destinationDevice,
            ulong sizeBytes,
            IntPtr stream = default,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var startTime = DateTimeOffset.UtcNow;
            var transferId = Guid.NewGuid();

            try
            {
                // Ensure P2P access is enabled
                var accessEnabled = await EnableP2PAccessAsync(sourceDevice, destinationDevice, cancellationToken)
                    .ConfigureAwait(false);

                if (!accessEnabled)
                {
                    throw new InvalidOperationException($"P2P access not available between devices {sourceDevice} and {destinationDevice}");
                }

                // Set source device as current
                var result = CudaRuntime.cudaSetDevice(sourceDevice);
                CudaRuntime.CheckError(result, $"setting source device {sourceDevice}");

                // Perform the transfer
                if (stream == IntPtr.Zero)
                {
                    // Synchronous transfer
                    var srcCuda = sourceBuffer as CudaMemoryBuffer ?? throw new ArgumentException("Source buffer must be CudaMemoryBuffer");
                    var dstCuda = destinationBuffer as CudaMemoryBuffer ?? throw new ArgumentException("Destination buffer must be CudaMemoryBuffer");
                    result = CudaRuntime.cudaMemcpy(
                        dstCuda.DevicePointer,
                        srcCuda.DevicePointer,
                        (nuint)sizeBytes,
                        CudaMemcpyKind.DeviceToDevice);
                    CudaRuntime.CheckError(result, "P2P memory transfer");
                }
                else
                {
                    // Asynchronous transfer
                    var srcCudaAsync = sourceBuffer as CudaMemoryBuffer ?? throw new ArgumentException("Source buffer must be CudaMemoryBuffer");
                    var dstCudaAsync = destinationBuffer as CudaMemoryBuffer ?? throw new ArgumentException("Destination buffer must be CudaMemoryBuffer");
                    result = CudaRuntime.cudaMemcpy(
                        dstCudaAsync.DevicePointer,
                        srcCudaAsync.DevicePointer,
                        (nuint)sizeBytes,
                        CudaMemcpyKind.DeviceToDevice,
                        stream);
                    CudaRuntime.CheckError(result, "async P2P memory transfer");

                    // Synchronize stream
                    result = CudaRuntime.cudaStreamSynchronize(stream);
                    CudaRuntime.CheckError(result, "synchronizing P2P transfer stream");
                }

                var endTime = DateTimeOffset.UtcNow;
                var duration = endTime - startTime;
                var bandwidthGBps = (sizeBytes / (1024.0 * 1024.0 * 1024.0)) / duration.TotalSeconds;

                // Update connection statistics
                if (_connections.TryGetValue((sourceDevice, destinationDevice), out var connection))
                {
                    connection.TransferCount++;
                    connection.TotalBytesTransferred += sizeBytes;
                    connection.LastTransferAt = endTime;
                    connection.AverageBandwidthGBps = (connection.AverageBandwidthGBps * (connection.TransferCount - 1) + bandwidthGBps) / connection.TransferCount;
                }

                _logger.LogDebugMessage($"P2P transfer completed: {sourceDevice} -> {destinationDevice}, {sizeBytes} bytes, {bandwidthGBps} GB/s");

                return new CudaP2PTransferResult
                {
                    TransferId = transferId,
                    Success = true,
                    SourceDevice = sourceDevice,
                    DestinationDevice = destinationDevice,
                    BytesTransferred = sizeBytes,
                    Duration = duration,
                    BandwidthGBps = bandwidthGBps,
                    StartTime = startTime,
                    EndTime = endTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"P2P transfer failed: {sourceDevice} -> {destinationDevice}, {sizeBytes} bytes");

                return new CudaP2PTransferResult
                {
                    TransferId = transferId,
                    Success = false,
                    SourceDevice = sourceDevice,
                    DestinationDevice = destinationDevice,
                    BytesTransferred = 0,
                    Duration = DateTimeOffset.UtcNow - startTime,
                    ErrorMessage = ex.Message,
                    StartTime = startTime,
                    EndTime = DateTimeOffset.UtcNow
                };
            }
        }

        /// <summary>
        /// Optimizes data placement across multiple GPUs
        /// </summary>
        public async Task<CudaP2PPlacementStrategy> OptimizeDataPlacementAsync(
            IEnumerable<CudaDataChunk> dataChunks,
            CudaP2PTopology topology,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            ThrowIfDisposed();

            var strategy = new CudaP2PPlacementStrategy();
            var chunks = dataChunks.ToList();

            // Simple load balancing strategy
            var deviceLoads = new Dictionary<int, ulong>();
            foreach (var device in topology.Devices)
            {
                deviceLoads[device.DeviceId] = 0;
            }

            foreach (var chunk in chunks)
            {
                // Find device with minimum load
                var targetDevice = deviceLoads.OrderBy(kvp => kvp.Value).First().Key;

                strategy.Placements.Add(new CudaDataPlacement
                {
                    ChunkId = chunk.Id,
                    DeviceId = targetDevice,
                    Size = chunk.Size,
                    Priority = chunk.Priority
                });

                deviceLoads[targetDevice] += chunk.Size;
            }

            // Calculate optimal transfer order
            strategy.TransferOrder = CalculateOptimalTransferOrder(strategy.Placements, topology);

            _logger.LogInfoMessage($"Optimized data placement for {chunks.Count} chunks across {topology.DeviceCount} devices");

            return strategy;
        }

        /// <summary>
        /// Gets P2P statistics for monitoring
        /// </summary>
        public CudaP2PStatistics GetStatistics()
        {
            ThrowIfDisposed();

            var enabledConnections = _connections.Values.Count(c => c.IsEnabled);
            var totalTransfers = _connections.Values.Sum(c => (long)c.TransferCount);
            var totalBytesTransferred = _connections.Values.Sum(c => (long)c.TotalBytesTransferred);
            var avgBandwidth = _connections.Values.Where(c => c.AverageBandwidthGBps > 0)
                .Select(c => c.AverageBandwidthGBps).DefaultIfEmpty(0).Average();

            return new CudaP2PStatistics
            {
                TotalDevices = _devices.Count,
                TotalConnections = _connections.Count,
                EnabledConnections = enabledConnections,
                TotalTransfers = totalTransfers,
                TotalBytesTransferred = (ulong)Math.Max(0, totalBytesTransferred),
                AverageBandwidthGBps = avgBandwidth,
                ConnectionUtilization = _connections.Values.ToDictionary(
                    c => $"{c.SourceDevice}->{c.DestinationDevice}",
                    c => new CudaP2PConnectionStats
                    {
                        TransferCount = c.TransferCount,
                        TotalBytes = c.TotalBytesTransferred,
                        AverageBandwidth = c.AverageBandwidthGBps,
                        LastTransfer = c.LastTransferAt
                    }
                )
            };
        }

        private void Initialize()
        {
            // Initialize CUDA and discover devices
            _ = GetDeviceCount();

            _logger.LogDebugMessage("Initializing P2P manager with {deviceCount} devices");
        }

        private static int GetDeviceCount()
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var count);
            CudaRuntime.CheckError(result, "getting device count");
            return count;
        }

        private static async Task<CudaDeviceInfo> GetDeviceInfoAsync(int deviceId)
        {
            await Task.Delay(1).ConfigureAwait(false);

            var result = CudaRuntime.cudaSetDevice(deviceId);
            CudaRuntime.CheckError(result, $"setting device {deviceId}");

            var props = new CudaDeviceProperties();
            result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            CudaRuntime.CheckError(result, $"getting properties for device {deviceId}");

            return new CudaDeviceInfo
            {
                DeviceId = deviceId,
                Name = props.DeviceName,
                ComputeCapability = $"{props.Major}.{props.Minor}",
                TotalMemoryBytes = (long)props.TotalGlobalMem,
                MultiprocessorCount = props.MultiProcessorCount,
                MaxThreadsPerBlock = props.MaxThreadsPerBlock,
                MaxThreadsPerMultiprocessor = props.MaxThreadsPerMultiProcessor,
                WarpSize = props.WarpSize,
                MemoryClockRate = props.MemoryClockRate,
                MemoryBusWidth = props.MemoryBusWidth,
                L2CacheSize = props.L2CacheSize,
                UnifiedAddressing = props.UnifiedAddressing != 0,
                CanMapHostMemory = props.CanMapHostMemory != 0,
                ConcurrentKernels = props.ConcurrentKernels != 0
            };
        }

        private async Task<bool> TestP2PAccessAsync(int sourceDevice, int destinationDevice)
        {
            await Task.Delay(1).ConfigureAwait(false);

            try
            {
                var canAccess = 0;
                var result = CudaRuntime.cudaDeviceCanAccessPeer(ref canAccess, sourceDevice, destinationDevice);
                CudaRuntime.CheckError(result, $"checking P2P access {sourceDevice} -> {destinationDevice}");

                return canAccess != 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error testing P2P access {Source} -> {Destination}",
                    sourceDevice, destinationDevice);
                return false;
            }
        }

        private async Task<double> MeasureP2PBandwidthAsync(int sourceDevice, int destinationDevice)
        {
            const ulong testSize = 64 * 1024 * 1024; // 64MB test
            const int iterations = 5;

            try
            {
                // Ensure P2P access
                _ = await EnableP2PAccessAsync(sourceDevice, destinationDevice).ConfigureAwait(false);

                var bandwidths = new List<double>();

                for (var i = 0; i < iterations; i++)
                {
                    // Allocate test buffers
                    var result = CudaRuntime.cudaSetDevice(sourceDevice);
                    CudaRuntime.CheckError(result, "setting source device");

                    var srcPtr = IntPtr.Zero;
                    result = CudaRuntime.cudaMalloc(ref srcPtr, testSize);
                    CudaRuntime.CheckError(result, "allocating source buffer");

                    result = CudaRuntime.cudaSetDevice(destinationDevice);
                    CudaRuntime.CheckError(result, "setting destination device");

                    var dstPtr = IntPtr.Zero;
                    result = CudaRuntime.cudaMalloc(ref dstPtr, testSize);
                    CudaRuntime.CheckError(result, "allocating destination buffer");

                    try
                    {
                        // Warm up
                        result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, (nuint)testSize, CudaMemcpyKind.DeviceToDevice);
                        CudaRuntime.CheckError(result, "warmup transfer");

                        // Measure bandwidth
                        var startTime = DateTimeOffset.UtcNow;
                        result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, (nuint)testSize, CudaMemcpyKind.DeviceToDevice);
                        CudaRuntime.CheckError(result, "bandwidth test transfer");
                        var endTime = DateTimeOffset.UtcNow;

                        var duration = (endTime - startTime).TotalSeconds;
                        var bandwidth = (testSize / (1024.0 * 1024.0 * 1024.0)) / duration;
                        bandwidths.Add(bandwidth);
                    }
                    finally
                    {
                        _ = CudaRuntime.cudaFree(srcPtr);
                        _ = CudaRuntime.cudaFree(dstPtr);
                    }
                }

                return bandwidths.Average();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error measuring P2P bandwidth {Source} -> {Destination}",
                    sourceDevice, destinationDevice);
                return 0.0;
            }
        }

        private static bool IsTopologyFullyConnected(CudaP2PTopology topology)
        {
            var deviceCount = topology.DeviceCount;
            var expectedConnections = deviceCount * (deviceCount - 1); // Bidirectional
            return topology.Connections.Count == expectedConnections;
        }

        private static Dictionary<(int, int), List<int>> CalculateOptimalPaths(CudaP2PTopology topology)
        {
            var paths = new Dictionary<(int, int), List<int>>();

            // Simple direct path calculation - could be enhanced with Floyd-Warshall for multi-hop
            foreach (var connection in topology.Connections)
            {
                paths[(connection.SourceDevice, connection.DestinationDevice)] =
                    [connection.SourceDevice, connection.DestinationDevice];
            }

            return paths;
        }

        private static List<Guid> CalculateOptimalTransferOrder(
            List<CudaDataPlacement> placements,
            CudaP2PTopology topology)
        {
            // Simple priority-based ordering - could be enhanced with dependency analysis
            return [.. placements
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Size)
            .Select(p => p.ChunkId)];
        }

        private void MonitorConnections(object? state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var stats = GetStatistics();
                _logger.LogDebugMessage($"P2P Status: {stats.EnabledConnections}/{stats.TotalConnections} connections enabled, {stats.TotalTransfers} transfers, {stats.AverageBandwidthGBps} GB/s avg");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error monitoring P2P connections");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CudaP2PManager));
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                _monitoringTimer?.Dispose();

                // Disable all enabled connections
                foreach (var connection in _connections.Values.Where(c => c.IsEnabled))
                {
                    try
                    {
                        var result = CudaRuntime.cudaSetDevice(connection.SourceDevice);
                        if (result == CudaError.Success)
                        {
                            _ = CudaRuntime.cudaDeviceDisablePeerAccess(connection.DestinationDevice);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disabling P2P connection during disposal: {Source} -> {Destination}",
                            connection.SourceDevice, connection.DestinationDevice);
                    }
                }

                _connectionSemaphore?.Dispose();
                _disposed = true;

                _logger.LogInfoMessage("CUDA P2P Manager disposed");
            }
        }
    }
    /// <summary>
    /// A class that represents cuda p2 p connection.
    /// </summary>

    // Supporting types
    public sealed class CudaP2PConnection
    {
        /// <summary>
        /// Gets or sets the source device.
        /// </summary>
        /// <value>The source device.</value>
        public int SourceDevice { get; set; }
        /// <summary>
        /// Gets or sets the destination device.
        /// </summary>
        /// <value>The destination device.</value>
        public int DestinationDevice { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether enabled.
        /// </summary>
        /// <value>The is enabled.</value>
        public bool IsEnabled { get; set; }
        /// <summary>
        /// Gets or sets the bandwidth g bps.
        /// </summary>
        /// <value>The bandwidth g bps.</value>
        public double BandwidthGBps { get; set; }
        /// <summary>
        /// Gets or sets the discovered at.
        /// </summary>
        /// <value>The discovered at.</value>
        public DateTimeOffset DiscoveredAt { get; set; }
        /// <summary>
        /// Gets or sets the enabled at.
        /// </summary>
        /// <value>The enabled at.</value>
        public DateTimeOffset? EnabledAt { get; set; }
        /// <summary>
        /// Gets or sets the disabled at.
        /// </summary>
        /// <value>The disabled at.</value>
        public DateTimeOffset? DisabledAt { get; set; }
        /// <summary>
        /// Gets or sets the last transfer at.
        /// </summary>
        /// <value>The last transfer at.</value>
        public DateTimeOffset? LastTransferAt { get; set; }
        /// <summary>
        /// Gets or sets the transfer count.
        /// </summary>
        /// <value>The transfer count.</value>
        public long TransferCount { get; set; }
        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        /// <value>The total bytes transferred.</value>
        public ulong TotalBytesTransferred { get; set; }
        /// <summary>
        /// Gets or sets the average bandwidth g bps.
        /// </summary>
        /// <value>The average bandwidth g bps.</value>
        public double AverageBandwidthGBps { get; set; }
    }
    /// <summary>
    /// A class that represents cuda p2 p topology.
    /// </summary>

    public sealed class CudaP2PTopology
    {
        /// <summary>
        /// Gets or sets the device count.
        /// </summary>
        /// <value>The device count.</value>
        public int DeviceCount { get; set; }
        /// <summary>
        /// Gets or sets the devices.
        /// </summary>
        /// <value>The devices.</value>
        public IList<CudaDeviceInfo> Devices { get; } = [];
        /// <summary>
        /// Gets or sets the connections.
        /// </summary>
        /// <value>The connections.</value>
        public IList<CudaP2PConnection> Connections { get; } = [];
        /// <summary>
        /// Gets or sets a value indicating whether fully connected.
        /// </summary>
        /// <value>The is fully connected.</value>
        public bool IsFullyConnected { get; set; }
        /// <summary>
        /// Gets or sets the optimal transfer paths.
        /// </summary>
        /// <value>The optimal transfer paths.</value>
        public Dictionary<(int, int), List<int>> OptimalTransferPaths { get; set; } = [];
    }
    /// <summary>
    /// A class that represents cuda p2 p transfer result.
    /// </summary>

    public sealed class CudaP2PTransferResult
    {
        /// <summary>
        /// Gets or sets the transfer identifier.
        /// </summary>
        /// <value>The transfer id.</value>
        public Guid TransferId { get; set; }
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        /// <value>The success.</value>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the source device.
        /// </summary>
        /// <value>The source device.</value>
        public int SourceDevice { get; set; }
        /// <summary>
        /// Gets or sets the destination device.
        /// </summary>
        /// <value>The destination device.</value>
        public int DestinationDevice { get; set; }
        /// <summary>
        /// Gets or sets the bytes transferred.
        /// </summary>
        /// <value>The bytes transferred.</value>
        public ulong BytesTransferred { get; set; }
        /// <summary>
        /// Gets or sets the duration.
        /// </summary>
        /// <value>The duration.</value>
        public TimeSpan Duration { get; set; }
        /// <summary>
        /// Gets or sets the bandwidth g bps.
        /// </summary>
        /// <value>The bandwidth g bps.</value>
        public double BandwidthGBps { get; set; }
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public DateTimeOffset StartTime { get; set; }
        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        /// <value>The end time.</value>
        public DateTimeOffset EndTime { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
    }
    /// <summary>
    /// A class that represents cuda data chunk.
    /// </summary>

    public sealed class CudaDataChunk
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public Guid Id { get; set; }
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        public ulong Size { get; set; }
        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public int Priority { get; set; }
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; } = string.Empty;
    }
    /// <summary>
    /// A class that represents cuda data placement.
    /// </summary>

    public sealed class CudaDataPlacement
    {
        /// <summary>
        /// Gets or sets the chunk identifier.
        /// </summary>
        /// <value>The chunk id.</value>
        public Guid ChunkId { get; set; }
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device id.</value>
        public int DeviceId { get; set; }
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        public ulong Size { get; set; }
        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public int Priority { get; set; }
    }
    /// <summary>
    /// A class that represents cuda p2 p placement strategy.
    /// </summary>

    public sealed class CudaP2PPlacementStrategy
    {
        /// <summary>
        /// Gets or sets the placements.
        /// </summary>
        /// <value>The placements.</value>
        public IList<CudaDataPlacement> Placements { get; } = [];
        /// <summary>
        /// Gets or sets the transfer order.
        /// </summary>
        /// <value>The transfer order.</value>
        public IList<Guid> TransferOrder { get; } = [];
        /// <summary>
        /// Gets or sets the estimated total time.
        /// </summary>
        /// <value>The estimated total time.</value>
        public double EstimatedTotalTime { get; set; }
        /// <summary>
        /// Gets or sets the device utilization.
        /// </summary>
        /// <value>The device utilization.</value>
        public Dictionary<int, double> DeviceUtilization { get; } = [];
    }
    /// <summary>
    /// A class that represents cuda p2 p statistics.
    /// </summary>

    public sealed class CudaP2PStatistics
    {
        /// <summary>
        /// Gets or sets the total devices.
        /// </summary>
        /// <value>The total devices.</value>
        public int TotalDevices { get; set; }
        /// <summary>
        /// Gets or sets the total connections.
        /// </summary>
        /// <value>The total connections.</value>
        public int TotalConnections { get; set; }
        /// <summary>
        /// Gets or sets the enabled connections.
        /// </summary>
        /// <value>The enabled connections.</value>
        public int EnabledConnections { get; set; }
        /// <summary>
        /// Gets or sets the total transfers.
        /// </summary>
        /// <value>The total transfers.</value>
        public long TotalTransfers { get; set; }
        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        /// <value>The total bytes transferred.</value>
        public ulong TotalBytesTransferred { get; set; }
        /// <summary>
        /// Gets or sets the average bandwidth g bps.
        /// </summary>
        /// <value>The average bandwidth g bps.</value>
        public double AverageBandwidthGBps { get; set; }
        /// <summary>
        /// Gets or sets the connection utilization.
        /// </summary>
        /// <value>The connection utilization.</value>
        public Dictionary<string, CudaP2PConnectionStats> ConnectionUtilization { get; } = [];
    }
    /// <summary>
    /// A class that represents cuda p2 p connection stats.
    /// </summary>

    public sealed class CudaP2PConnectionStats
    {
        /// <summary>
        /// Gets or sets the transfer count.
        /// </summary>
        /// <value>The transfer count.</value>
        public long TransferCount { get; set; }
        /// <summary>
        /// Gets or sets the total bytes.
        /// </summary>
        /// <value>The total bytes.</value>
        public ulong TotalBytes { get; set; }
        /// <summary>
        /// Gets or sets the average bandwidth.
        /// </summary>
        /// <value>The average bandwidth.</value>
        public double AverageBandwidth { get; set; }
        /// <summary>
        /// Gets or sets the last transfer.
        /// </summary>
        /// <value>The last transfer.</value>
        public DateTimeOffset? LastTransfer { get; set; }
    }
}
