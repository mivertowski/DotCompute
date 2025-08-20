// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Memory;
using Microsoft.Extensions.Logging;

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
    private readonly object _lockObject = new();
    private bool _disposed;

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

        _logger.LogInformation("CUDA P2P Manager initialized for multi-GPU operations");
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
            for (int i = 0; i < deviceCount; i++)
            {
                var deviceInfo = await GetDeviceInfoAsync(i).ConfigureAwait(false);
                _devices[i] = deviceInfo;
                topology.Devices.Add(deviceInfo);
            }

            // Test P2P connectivity between all device pairs
            for (int srcDevice = 0; srcDevice < deviceCount; srcDevice++)
            {
                for (int dstDevice = 0; dstDevice < deviceCount; dstDevice++)
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

                            _logger.LogDebug("P2P connection available: {Source} -> {Destination} ({Bandwidth:F2} GB/s)",
                                srcDevice, dstDevice, connection.BandwidthGBps);
                        }
                    }
                }
            }

            topology.IsFullyConnected = IsTopologyFullyConnected(topology);
            topology.OptimalTransferPaths = CalculateOptimalPaths(topology);

            _logger.LogInformation("Discovered P2P topology: {DeviceCount} devices, {ConnectionCount} connections, fully connected: {FullyConnected}",
                deviceCount, topology.Connections.Count, topology.IsFullyConnected);

            return topology;
        }
        finally
        {
            _connectionSemaphore.Release();
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

            _logger.LogInformation("Enabled P2P access: {Source} -> {Destination}", 
                sourceDevice, destinationDevice);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable P2P access: {Source} -> {Destination}", 
                sourceDevice, destinationDevice);
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

            _logger.LogInformation("Disabled P2P access: {Source} -> {Destination}", 
                sourceDevice, destinationDevice);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable P2P access: {Source} -> {Destination}", 
                sourceDevice, destinationDevice);
            return false;
        }
    }

    /// <summary>
    /// Performs a direct P2P memory transfer between devices
    /// </summary>
    public async Task<CudaP2PTransferResult> TransferAsync(
        CudaMemoryBuffer sourceBuffer,
        int sourceDevice,
        CudaMemoryBuffer destinationBuffer,
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
                result = CudaRuntime.cudaMemcpy(
                    destinationBuffer.DevicePointer,
                    sourceBuffer.DevicePointer,
                    sizeBytes,
                    CudaMemcpyKind.DeviceToDevice);
                CudaRuntime.CheckError(result, "P2P memory transfer");
            }
            else
            {
                // Asynchronous transfer
                result = CudaRuntime.cudaMemcpyAsync(
                    destinationBuffer.DevicePointer,
                    sourceBuffer.DevicePointer,
                    sizeBytes,
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

            _logger.LogDebug("P2P transfer completed: {Source} -> {Destination}, {Size} bytes, {Bandwidth:F2} GB/s",
                sourceDevice, destinationDevice, sizeBytes, bandwidthGBps);

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
            _logger.LogError(ex, "P2P transfer failed: {Source} -> {Destination}, {Size} bytes",
                sourceDevice, destinationDevice, sizeBytes);

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

        _logger.LogInformation("Optimized data placement for {ChunkCount} chunks across {DeviceCount} devices",
            chunks.Count, topology.DeviceCount);

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
        var deviceCount = GetDeviceCount();
        
        _logger.LogDebug("Initializing P2P manager with {DeviceCount} devices", deviceCount);
    }

    private int GetDeviceCount()
    {
        var result = CudaRuntime.cudaGetDeviceCount(out var count);
        CudaRuntime.CheckError(result, "getting device count");
        return count;
    }

    private async Task<CudaDeviceInfo> GetDeviceInfoAsync(int deviceId)
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
            Name = props.Name,
            ComputeCapability = $"{props.Major}.{props.Minor}",
            TotalMemoryBytes = props.TotalGlobalMem,
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
            await EnableP2PAccessAsync(sourceDevice, destinationDevice).ConfigureAwait(false);

            var bandwidths = new List<double>();

            for (int i = 0; i < iterations; i++)
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
                    result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, testSize, CudaMemcpyKind.DeviceToDevice);
                    CudaRuntime.CheckError(result, "warmup transfer");

                    // Measure bandwidth
                    var startTime = DateTimeOffset.UtcNow;
                    result = CudaRuntime.cudaMemcpy(dstPtr, srcPtr, testSize, CudaMemcpyKind.DeviceToDevice);
                    CudaRuntime.CheckError(result, "bandwidth test transfer");
                    var endTime = DateTimeOffset.UtcNow;

                    var duration = (endTime - startTime).TotalSeconds;
                    var bandwidth = (testSize / (1024.0 * 1024.0 * 1024.0)) / duration;
                    bandwidths.Add(bandwidth);
                }
                finally
                {
                    CudaRuntime.cudaFree(srcPtr);
                    CudaRuntime.cudaFree(dstPtr);
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

    private bool IsTopologyFullyConnected(CudaP2PTopology topology)
    {
        var deviceCount = topology.DeviceCount;
        var expectedConnections = deviceCount * (deviceCount - 1); // Bidirectional
        return topology.Connections.Count == expectedConnections;
    }

    private Dictionary<(int, int), List<int>> CalculateOptimalPaths(CudaP2PTopology topology)
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

    private List<Guid> CalculateOptimalTransferOrder(
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
            _logger.LogDebug("P2P Status: {Enabled}/{Total} connections enabled, {Transfers} transfers, {Bandwidth:F2} GB/s avg",
                stats.EnabledConnections, stats.TotalConnections, stats.TotalTransfers, stats.AverageBandwidthGBps);
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
                        CudaRuntime.cudaDeviceDisablePeerAccess(connection.DestinationDevice);
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

            _logger.LogInformation("CUDA P2P Manager disposed");
        }
    }
}

// Supporting types
public sealed class CudaP2PConnection
{
    public int SourceDevice { get; set; }
    public int DestinationDevice { get; set; }
    public bool IsEnabled { get; set; }
    public double BandwidthGBps { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; }
    public DateTimeOffset? EnabledAt { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
    public DateTimeOffset? LastTransferAt { get; set; }
    public long TransferCount { get; set; }
    public ulong TotalBytesTransferred { get; set; }
    public double AverageBandwidthGBps { get; set; }
}

public sealed class CudaP2PTopology
{
    public int DeviceCount { get; set; }
    public List<CudaDeviceInfo> Devices { get; set; } = [];
    public List<CudaP2PConnection> Connections { get; set; } = [];
    public bool IsFullyConnected { get; set; }
    public Dictionary<(int, int), List<int>> OptimalTransferPaths { get; set; } = [];
}

public sealed class CudaDeviceInfo
{
    public int DeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ComputeCapability { get; set; } = string.Empty;
    public ulong TotalMemoryBytes { get; set; }
    public int MultiprocessorCount { get; set; }
    public int MaxThreadsPerBlock { get; set; }
    public int MaxThreadsPerMultiprocessor { get; set; }
    public int WarpSize { get; set; }
    public int MemoryClockRate { get; set; }
    public int MemoryBusWidth { get; set; }
    public int L2CacheSize { get; set; }
    public bool UnifiedAddressing { get; set; }
    public bool CanMapHostMemory { get; set; }
    public bool ConcurrentKernels { get; set; }
}

public sealed class CudaP2PTransferResult
{
    public Guid TransferId { get; set; }
    public bool Success { get; set; }
    public int SourceDevice { get; set; }
    public int DestinationDevice { get; set; }
    public ulong BytesTransferred { get; set; }
    public TimeSpan Duration { get; set; }
    public double BandwidthGBps { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class CudaDataChunk
{
    public Guid Id { get; set; }
    public ulong Size { get; set; }
    public int Priority { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CudaDataPlacement
{
    public Guid ChunkId { get; set; }
    public int DeviceId { get; set; }
    public ulong Size { get; set; }
    public int Priority { get; set; }
}

public sealed class CudaP2PPlacementStrategy
{
    public List<CudaDataPlacement> Placements { get; set; } = [];
    public List<Guid> TransferOrder { get; set; } = [];
    public double EstimatedTotalTime { get; set; }
    public Dictionary<int, double> DeviceUtilization { get; set; } = [];
}

public sealed class CudaP2PStatistics
{
    public int TotalDevices { get; set; }
    public int TotalConnections { get; set; }
    public int EnabledConnections { get; set; }
    public long TotalTransfers { get; set; }
    public ulong TotalBytesTransferred { get; set; }
    public double AverageBandwidthGBps { get; set; }
    public Dictionary<string, CudaP2PConnectionStats> ConnectionUtilization { get; set; } = [];
}

public sealed class CudaP2PConnectionStats
{
    public long TransferCount { get; set; }
    public ulong TotalBytes { get; set; }
    public double AverageBandwidth { get; set; }
    public DateTimeOffset? LastTransfer { get; set; }
}}
