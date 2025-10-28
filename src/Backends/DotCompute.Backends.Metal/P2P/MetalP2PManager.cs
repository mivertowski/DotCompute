// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.P2P;

/// <summary>
/// Metal Peer-to-Peer manager for multi-device operations on Mac Pro multi-GPU systems.
/// Provides device-to-device buffer sharing, topology discovery, and performance tracking.
/// </summary>
public sealed class MetalP2PManager : IDisposable
{
    private readonly ILogger<MetalP2PManager> _logger;
    private readonly ConcurrentDictionary<(int, int), MetalP2PConnection> _connections;
    private readonly ConcurrentDictionary<int, MetalDeviceDescriptor> _devices;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly Timer _monitoringTimer;
    private bool _disposed;

    public MetalP2PManager(ILogger<MetalP2PManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connections = new ConcurrentDictionary<(int, int), MetalP2PConnection>();
        _devices = new ConcurrentDictionary<int, MetalDeviceDescriptor>();
        _connectionSemaphore = new SemaphoreSlim(1, 1);

        Initialize();

        _monitoringTimer = new Timer(MonitorConnections, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("Metal P2P Manager initialized for multi-GPU operations");
    }

    /// <summary>
    /// Discovers and initializes P2P capabilities between Metal devices.
    /// </summary>
    public async Task<MetalP2PTopology> DiscoverTopologyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var deviceCount = MetalNative.GetDeviceCount();
            var topology = new MetalP2PTopology { DeviceCount = deviceCount };

            // Discover all devices
            for (var i = 0; i < deviceCount; i++)
            {
                var device = await GetDeviceDescriptorAsync(i).ConfigureAwait(false);
                _devices[i] = device;
                topology.Devices.Add(device);
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
                            var connection = new MetalP2PConnection
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
            _ = _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Enables P2P access between two Metal devices.
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
            var canAccess = await TestP2PAccessAsync(sourceDevice, destinationDevice).ConfigureAwait(false);
            if (!canAccess)
            {
                return false;
            }

            connection = new MetalP2PConnection
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
            // Metal devices can share resources through MTLHeap and MTLResource peer groups
            // On macOS with unified memory, this is transparent
            connection.IsEnabled = true;
            connection.EnabledAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("Enabled P2P access: {Source} -> {Destination}", sourceDevice, destinationDevice);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable P2P access: {Source} -> {Destination}", sourceDevice, destinationDevice);
            return false;
        }
    }

    /// <summary>
    /// Disables P2P access between two Metal devices.
    /// </summary>
    public async Task<bool> DisableP2PAccessAsync(int sourceDevice, int destinationDevice,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        ThrowIfDisposed();

        var connectionKey = (sourceDevice, destinationDevice);
        if (!_connections.TryGetValue(connectionKey, out var connection) || !connection.IsEnabled)
        {
            return true; // Already disabled
        }

        try
        {
            connection.IsEnabled = false;
            connection.DisabledAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("Disabled P2P access: {Source} -> {Destination}", sourceDevice, destinationDevice);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable P2P access: {Source} -> {Destination}", sourceDevice, destinationDevice);
            return false;
        }
    }

    /// <summary>
    /// Performs a direct P2P memory transfer between Metal devices.
    /// </summary>
    public async Task<MetalP2PTransferResult> TransferAsync(
        IUnifiedMemoryBuffer sourceBuffer,
        int sourceDevice,
        IUnifiedMemoryBuffer destinationBuffer,
        int destinationDevice,
        ulong sizeBytes,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var startTime = DateTimeOffset.UtcNow;
        var transferId = Guid.NewGuid();

        try
        {
            var accessEnabled = await EnableP2PAccessAsync(sourceDevice, destinationDevice, cancellationToken)
                .ConfigureAwait(false);

            if (!accessEnabled)
            {
                throw new InvalidOperationException($"P2P access not available between devices {sourceDevice} and {destinationDevice}");
            }

            var srcMetal = sourceBuffer as MetalMemoryBuffer ?? throw new ArgumentException("Source buffer must be MetalMemoryBuffer");
            var dstMetal = destinationBuffer as MetalMemoryBuffer ?? throw new ArgumentException("Destination buffer must be MetalMemoryBuffer");

            // Metal P2P transfer using blit command encoder
            var srcDevice = MetalNative.CreateDeviceAtIndex(sourceDevice);
            var dstDevice = MetalNative.CreateDeviceAtIndex(destinationDevice);

            var commandQueue = MetalNative.CreateCommandQueue(srcDevice);
            var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);

            // Copy buffer via blit encoder
            MetalNative.CopyBuffer(srcMetal.Buffer, 0, dstMetal.Buffer, 0, (long)sizeBytes);

            MetalNative.CommitCommandBuffer(commandBuffer);
            MetalNative.WaitUntilCompleted(commandBuffer);

            // Cleanup
            MetalNative.ReleaseCommandBuffer(commandBuffer);
            MetalNative.ReleaseCommandQueue(commandQueue);
            MetalNative.ReleaseDevice(dstDevice);
            MetalNative.ReleaseDevice(srcDevice);

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

            _logger.LogDebug("P2P transfer completed: {Source} -> {Destination}, {Bytes} bytes, {Bandwidth:F2} GB/s",
                sourceDevice, destinationDevice, sizeBytes, bandwidthGBps);

            return new MetalP2PTransferResult
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
            _logger.LogError(ex, "P2P transfer failed: {Source} -> {Destination}, {Bytes} bytes", sourceDevice, destinationDevice, sizeBytes);

            return new MetalP2PTransferResult
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
    /// Gets P2P statistics for monitoring.
    /// </summary>
    public MetalP2PStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var enabledConnections = _connections.Values.Count(c => c.IsEnabled);
        var totalTransfers = _connections.Values.Sum(c => (long)c.TransferCount);
        var totalBytesTransferred = _connections.Values.Sum(c => (long)c.TotalBytesTransferred);
        var avgBandwidth = _connections.Values.Where(c => c.AverageBandwidthGBps > 0)
            .Select(c => c.AverageBandwidthGBps).DefaultIfEmpty(0).Average();

        return new MetalP2PStatistics
        {
            TotalDevices = _devices.Count,
            TotalConnections = _connections.Count,
            EnabledConnections = enabledConnections,
            TotalTransfers = totalTransfers,
            TotalBytesTransferred = (ulong)Math.Max(0, totalBytesTransferred),
            AverageBandwidthGBps = avgBandwidth,
            ConnectionUtilization = _connections.Values.ToDictionary(
                c => $"{c.SourceDevice}->{c.DestinationDevice}",
                c => new MetalP2PConnectionStats
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
        var deviceCount = MetalNative.GetDeviceCount();
        _logger.LogDebug("Initializing P2P manager with {DeviceCount} Metal devices", deviceCount);
    }

    private static async Task<MetalDeviceDescriptor> GetDeviceDescriptorAsync(int deviceId)
    {
        await Task.Delay(1).ConfigureAwait(false);

        var device = MetalNative.CreateDeviceAtIndex(deviceId);
        var deviceInfo = MetalNative.GetDeviceInfo(device);

        var descriptor = new MetalDeviceDescriptor
        {
            DeviceId = deviceId,
            Name = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown Metal Device",
            RegistryID = deviceInfo.RegistryID,
            TotalMemoryBytes = (long)deviceInfo.RecommendedMaxWorkingSetSize,
            MaxThreadgroupSize = (int)deviceInfo.MaxThreadgroupSize,
            HasUnifiedMemory = deviceInfo.HasUnifiedMemory,
            IsLowPower = deviceInfo.IsLowPower,
            IsRemovable = deviceInfo.IsRemovable,
            Location = deviceInfo.Location
        };

        MetalNative.ReleaseDevice(device);
        return descriptor;
    }

    private async Task<bool> TestP2PAccessAsync(int sourceDevice, int destinationDevice)
    {
        await Task.Delay(1).ConfigureAwait(false);

        try
        {
            // Metal supports P2P if devices are on the same machine
            // For Mac Pro multi-GPU setups, this is generally true
            var srcDevice = MetalNative.CreateDeviceAtIndex(sourceDevice);
            var dstDevice = MetalNative.CreateDeviceAtIndex(destinationDevice);

            var canAccess = srcDevice != IntPtr.Zero && dstDevice != IntPtr.Zero;

            MetalNative.ReleaseDevice(dstDevice);
            MetalNative.ReleaseDevice(srcDevice);

            return canAccess;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error testing P2P access {Source} -> {Destination}", sourceDevice, destinationDevice);
            return false;
        }
    }

    private async Task<double> MeasureP2PBandwidthAsync(int sourceDevice, int destinationDevice)
    {
        const ulong testSize = 64 * 1024 * 1024; // 64MB test
        const int iterations = 5;

        try
        {
            _ = await EnableP2PAccessAsync(sourceDevice, destinationDevice).ConfigureAwait(false);

            var bandwidths = new List<double>();

            for (var i = 0; i < iterations; i++)
            {
                var srcDevice = MetalNative.CreateDeviceAtIndex(sourceDevice);
                var dstDevice = MetalNative.CreateDeviceAtIndex(destinationDevice);

                var srcBuffer = MetalNative.CreateBuffer(srcDevice, (nuint)testSize, MetalStorageMode.Shared);
                var dstBuffer = MetalNative.CreateBuffer(dstDevice, (nuint)testSize, MetalStorageMode.Shared);

                try
                {
                    // Warmup
                    MetalNative.CopyBuffer(srcBuffer, 0, dstBuffer, 0, (long)testSize);

                    // Measure bandwidth
                    var startTime = DateTimeOffset.UtcNow;
                    MetalNative.CopyBuffer(srcBuffer, 0, dstBuffer, 0, (long)testSize);
                    var endTime = DateTimeOffset.UtcNow;

                    var duration = (endTime - startTime).TotalSeconds;
                    var bandwidth = (testSize / (1024.0 * 1024.0 * 1024.0)) / duration;
                    bandwidths.Add(bandwidth);
                }
                finally
                {
                    MetalNative.ReleaseBuffer(dstBuffer);
                    MetalNative.ReleaseBuffer(srcBuffer);
                    MetalNative.ReleaseDevice(dstDevice);
                    MetalNative.ReleaseDevice(srcDevice);
                }
            }

            return bandwidths.Average();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error measuring P2P bandwidth {Source} -> {Destination}", sourceDevice, destinationDevice);
            return 0.0;
        }
    }

    private static bool IsTopologyFullyConnected(MetalP2PTopology topology)
    {
        var deviceCount = topology.DeviceCount;
        var expectedConnections = deviceCount * (deviceCount - 1);
        return topology.Connections.Count == expectedConnections;
    }

    private static Dictionary<(int, int), List<int>> CalculateOptimalPaths(MetalP2PTopology topology)
    {
        var paths = new Dictionary<(int, int), List<int>>();

        foreach (var connection in topology.Connections)
        {
            paths[(connection.SourceDevice, connection.DestinationDevice)] =
                [connection.SourceDevice, connection.DestinationDevice];
        }

        return paths;
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
            _logger.LogDebug("P2P Status: {EnabledConnections}/{TotalConnections} connections enabled, {TotalTransfers} transfers, {AvgBandwidth:F2} GB/s avg",
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
            throw new ObjectDisposedException(nameof(MetalP2PManager));
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
                    connection.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disabling P2P connection during disposal: {Source} -> {Destination}",
                        connection.SourceDevice, connection.DestinationDevice);
                }
            }

            _connectionSemaphore?.Dispose();
            _disposed = true;

            _logger.LogInformation("Metal P2P Manager disposed");
        }
    }
}

// Supporting types
public sealed class MetalP2PConnection
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

public sealed class MetalP2PTopology
{
    public int DeviceCount { get; set; }
    public List<MetalDeviceDescriptor> Devices { get; set; } = [];
    public List<MetalP2PConnection> Connections { get; set; } = [];
    public bool IsFullyConnected { get; set; }
    public Dictionary<(int, int), List<int>> OptimalTransferPaths { get; set; } = [];
}

public sealed class MetalDeviceDescriptor
{
    public int DeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ulong RegistryID { get; set; }
    public long TotalMemoryBytes { get; set; }
    public int MaxThreadgroupSize { get; set; }
    public bool HasUnifiedMemory { get; set; }
    public bool IsLowPower { get; set; }
    public bool IsRemovable { get; set; }
    public MetalDeviceLocation Location { get; set; }
}

public sealed class MetalP2PTransferResult
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

public sealed class MetalP2PStatistics
{
    public int TotalDevices { get; set; }
    public int TotalConnections { get; set; }
    public int EnabledConnections { get; set; }
    public long TotalTransfers { get; set; }
    public ulong TotalBytesTransferred { get; set; }
    public double AverageBandwidthGBps { get; set; }
    public Dictionary<string, MetalP2PConnectionStats> ConnectionUtilization { get; set; } = [];
}

public sealed class MetalP2PConnectionStats
{
    public long TransferCount { get; set; }
    public ulong TotalBytes { get; set; }
    public double AverageBandwidth { get; set; }
    public DateTimeOffset? LastTransfer { get; set; }
}
