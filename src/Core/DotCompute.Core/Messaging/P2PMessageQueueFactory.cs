// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Logging;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Messaging;

/// <summary>
/// Factory for creating P2P message queues between GPU devices.
/// </summary>
/// <remarks>
/// <para>
/// Integrates with the P2P capability detection infrastructure to automatically
/// determine the optimal transfer mode between devices. Caches P2P capability
/// information to avoid repeated detection overhead.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// var factory = new P2PMessageQueueFactory(logger, capabilityDetector);
///
/// // Initialize topology for all devices
/// await factory.InitializeTopologyAsync(devices);
///
/// // Create queue between two devices
/// var queue = await factory.CreateQueueAsync&lt;MyMessage&gt;(device0, device1);
/// </code>
/// </para>
/// </remarks>
public sealed class P2PMessageQueueFactory : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly P2PCapabilityDetector _capabilityDetector;
    private readonly ConcurrentDictionary<string, P2PConnectionInfo> _connectionCache;
    private readonly ConcurrentDictionary<string, object> _activeQueues;
    private readonly P2PMessageQueueOptions _defaultOptions;
    private bool _topologyInitialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new P2P message queue factory.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="capabilityDetector">P2P capability detector.</param>
    /// <param name="defaultOptions">Default queue options.</param>
    public P2PMessageQueueFactory(
        ILogger logger,
        P2PCapabilityDetector capabilityDetector,
        P2PMessageQueueOptions? defaultOptions = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(capabilityDetector);

        _logger = logger;
        _capabilityDetector = capabilityDetector;
        _connectionCache = new ConcurrentDictionary<string, P2PConnectionInfo>();
        _activeQueues = new ConcurrentDictionary<string, object>();
        _defaultOptions = defaultOptions ?? new P2PMessageQueueOptions();
        _defaultOptions.Validate();

        _logger.LogInfoMessage("P2P message queue factory initialized");
    }

    /// <summary>
    /// Initializes P2P topology and capability detection for a set of devices.
    /// </summary>
    /// <param name="devices">The devices to initialize P2P for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Topology initialization result.</returns>
    public async Task<P2PTopologyResult> InitializeTopologyAsync(
        IAccelerator[] devices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devices);

        if (devices.Length < 2)
        {
            _topologyInitialized = true;
            return new P2PTopologyResult
            {
                IsSuccessful = true,
                DeviceCount = devices.Length,
                P2PEnabledPairs = 0,
                Message = "Single device - no P2P connections needed"
            };
        }

        _logger.LogInfoMessage($"Initializing P2P topology for {devices.Length} devices");

        var enabledPairs = 0;
        var totalPairs = 0;

        try
        {
            // Detect P2P capability for all device pairs
            for (var i = 0; i < devices.Length; i++)
            {
                for (var j = i + 1; j < devices.Length; j++)
                {
                    var device1 = devices[i];
                    var device2 = devices[j];
                    totalPairs++;

                    var capability = await _capabilityDetector.DetectP2PCapabilityAsync(
                        device1, device2, cancellationToken);

                    // Cache both directions
                    var key1 = GetConnectionKey(device1, device2);
                    var key2 = GetConnectionKey(device2, device1);

                    var connectionInfo = new P2PConnectionInfo
                    {
                        SourceDevice = device1,
                        DestinationDevice = device2,
                        IsP2PAvailable = capability.IsSupported,
                        ConnectionType = capability.ConnectionType.ToString(),
                        EstimatedBandwidthGBps = capability.EstimatedBandwidthGBps,
                        EstimatedLatencyUs = EstimateLatencyFromBandwidth(capability.EstimatedBandwidthGBps)
                    };

                    _connectionCache[key1] = connectionInfo;
                    _connectionCache[key2] = connectionInfo with
                    {
                        SourceDevice = device2,
                        DestinationDevice = device1
                    };

                    if (capability.IsSupported)
                    {
                        enabledPairs++;
                        _logger.LogDebugMessage(
                            $"P2P enabled: {device1.Info.Name} <-> {device2.Info.Name} " +
                            $"({capability.ConnectionType}, {capability.EstimatedBandwidthGBps:F1} GB/s)");
                    }
                    else
                    {
                        _logger.LogDebugMessage(
                            $"P2P not available: {device1.Info.Name} <-> {device2.Info.Name} " +
                            $"(Reason: {capability.LimitationReason})");
                    }
                }
            }

            _topologyInitialized = true;

            _logger.LogInfoMessage(
                $"P2P topology initialized: {enabledPairs}/{totalPairs} pairs with P2P support");

            return new P2PTopologyResult
            {
                IsSuccessful = true,
                DeviceCount = devices.Length,
                P2PEnabledPairs = enabledPairs,
                TotalPairs = totalPairs,
                Message = $"{enabledPairs} P2P-enabled device pairs detected"
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "P2P topology initialization failed");
            return new P2PTopologyResult
            {
                IsSuccessful = false,
                DeviceCount = devices.Length,
                P2PEnabledPairs = enabledPairs,
                TotalPairs = totalPairs,
                Message = $"Initialization failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Creates a P2P message queue between two devices.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="sourceDevice">The source (sending) device.</param>
    /// <param name="destinationDevice">The destination (receiving) device.</param>
    /// <param name="options">Optional queue configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created P2P message queue.</returns>
    public async ValueTask<IP2PMessageQueue<T>> CreateQueueAsync<T>(
        IAccelerator sourceDevice,
        IAccelerator destinationDevice,
        P2PMessageQueueOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sourceDevice);
        ArgumentNullException.ThrowIfNull(destinationDevice);

        var effectiveOptions = options ?? _defaultOptions;
        effectiveOptions.Validate();

        // Get or detect P2P capability
        var connectionKey = GetConnectionKey(sourceDevice, destinationDevice);
        var connectionInfo = await GetOrDetectConnectionAsync(
            sourceDevice, destinationDevice, connectionKey, cancellationToken);

        // Create the queue
        var queue = new P2PMessageQueue<T>(
            sourceDevice,
            destinationDevice,
            effectiveOptions,
            _logger,
            connectionInfo.IsP2PAvailable);

        // Track active queue
        var queueKey = $"{connectionKey}:{typeof(T).Name}";
        _activeQueues[queueKey] = queue;

        _logger.LogDebugMessage(
            $"P2P queue created: {sourceDevice.Info.Name} -> {destinationDevice.Info.Name} " +
            $"(Type: {typeof(T).Name}, Mode: {queue.CurrentTransferMode})");

        return queue;
    }

    /// <summary>
    /// Creates a bidirectional P2P queue pair between two devices.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="device1">First device.</param>
    /// <param name="device2">Second device.</param>
    /// <param name="options">Optional queue configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of (device1 to device2, device2 to device1) queues.</returns>
    public async ValueTask<(IP2PMessageQueue<T> Forward, IP2PMessageQueue<T> Reverse)> CreateBidirectionalQueuesAsync<T>(
        IAccelerator device1,
        IAccelerator device2,
        P2PMessageQueueOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        var forward = await CreateQueueAsync<T>(device1, device2, options, cancellationToken);
        var reverse = await CreateQueueAsync<T>(device2, device1, options, cancellationToken);

        return (forward, reverse);
    }

    /// <summary>
    /// Creates a ring of P2P queues connecting all devices.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="devices">The devices to connect in a ring.</param>
    /// <param name="options">Optional queue configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of queues forming a ring: [0->1, 1->2, ..., N-1->0].</returns>
    public async ValueTask<IP2PMessageQueue<T>[]> CreateRingAsync<T>(
        IAccelerator[] devices,
        P2PMessageQueueOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(devices);

        if (devices.Length < 2)
        {
            throw new ArgumentException("Ring requires at least 2 devices", nameof(devices));
        }

        var queues = new IP2PMessageQueue<T>[devices.Length];

        for (var i = 0; i < devices.Length; i++)
        {
            var nextIndex = (i + 1) % devices.Length;
            queues[i] = await CreateQueueAsync<T>(devices[i], devices[nextIndex], options, cancellationToken);
        }

        _logger.LogInfoMessage($"P2P ring created with {devices.Length} devices");

        return queues;
    }

    /// <summary>
    /// Creates an all-to-all mesh of P2P queues.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="devices">The devices to connect in a mesh.</param>
    /// <param name="options">Optional queue configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of (sourceId, destId) -> queue.</returns>
    public async ValueTask<Dictionary<(string, string), IP2PMessageQueue<T>>> CreateMeshAsync<T>(
        IAccelerator[] devices,
        P2PMessageQueueOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(devices);

        var mesh = new Dictionary<(string, string), IP2PMessageQueue<T>>();

        for (var i = 0; i < devices.Length; i++)
        {
            for (var j = 0; j < devices.Length; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var queue = await CreateQueueAsync<T>(devices[i], devices[j], options, cancellationToken);
                mesh[(devices[i].Info.Id, devices[j].Info.Id)] = queue;
            }
        }

        _logger.LogInfoMessage($"P2P mesh created: {mesh.Count} queues for {devices.Length} devices");

        return mesh;
    }

    /// <summary>
    /// Gets P2P connection information between two devices.
    /// </summary>
    /// <param name="sourceDevice">The source device.</param>
    /// <param name="destinationDevice">The destination device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection information.</returns>
    public async ValueTask<P2PConnectionInfo> GetConnectionInfoAsync(
        IAccelerator sourceDevice,
        IAccelerator destinationDevice,
        CancellationToken cancellationToken = default)
    {
        var connectionKey = GetConnectionKey(sourceDevice, destinationDevice);
        return await GetOrDetectConnectionAsync(sourceDevice, destinationDevice, connectionKey, cancellationToken);
    }

    /// <summary>
    /// Gets the number of active P2P queues.
    /// </summary>
    public int ActiveQueueCount => _activeQueues.Count;

    /// <summary>
    /// Gets whether topology has been initialized.
    /// </summary>
    public bool IsTopologyInitialized => _topologyInitialized;

    private static string GetConnectionKey(IAccelerator source, IAccelerator destination)
        => $"{source.Info.Id}->{destination.Info.Id}";

    private async ValueTask<P2PConnectionInfo> GetOrDetectConnectionAsync(
        IAccelerator sourceDevice,
        IAccelerator destinationDevice,
        string connectionKey,
        CancellationToken cancellationToken)
    {
        if (_connectionCache.TryGetValue(connectionKey, out var cached))
        {
            return cached;
        }

        // Detect capability on demand
        try
        {
            var capability = await _capabilityDetector.DetectP2PCapabilityAsync(
                sourceDevice, destinationDevice, cancellationToken);

            var connectionInfo = new P2PConnectionInfo
            {
                SourceDevice = sourceDevice,
                DestinationDevice = destinationDevice,
                IsP2PAvailable = capability.IsSupported,
                ConnectionType = capability.ConnectionType.ToString(),
                EstimatedBandwidthGBps = capability.EstimatedBandwidthGBps,
                EstimatedLatencyUs = EstimateLatencyFromBandwidth(capability.EstimatedBandwidthGBps)
            };

            _connectionCache[connectionKey] = connectionInfo;
            return connectionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarningMessage(
                $"P2P capability detection failed between {sourceDevice.Info.Name} and " +
                $"{destinationDevice.Info.Name}: {ex.Message}");

            // Return a default "not available" result
            var fallbackInfo = new P2PConnectionInfo
            {
                SourceDevice = sourceDevice,
                DestinationDevice = destinationDevice,
                IsP2PAvailable = false,
                ConnectionType = "None",
                EstimatedBandwidthGBps = 0,
                EstimatedLatencyUs = 0
            };

            _connectionCache[connectionKey] = fallbackInfo;
            return fallbackInfo;
        }
    }

    private static double EstimateLatencyFromBandwidth(double bandwidthGBps)
    {
        // Estimate latency based on connection type inferred from bandwidth
        // NVLink (>200 GB/s): ~1-2 μs
        // PCIe 4.0 x16 (32 GB/s): ~5-10 μs
        // PCIe 3.0 x16 (16 GB/s): ~10-20 μs
        // Host-mediated: ~50-100 μs
        return bandwidthGBps switch
        {
            > 200 => 1.5,   // NVLink
            > 30 => 7.5,    // PCIe 4.0
            > 15 => 15.0,   // PCIe 3.0
            > 0 => 25.0,    // Slower PCIe
            _ => 75.0       // Host-mediated fallback
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose all active queues
        foreach (var kvp in _activeQueues)
        {
            if (kvp.Value is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (kvp.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _activeQueues.Clear();
        _connectionCache.Clear();

        _logger.LogInfoMessage("P2P message queue factory disposed");
    }
}

/// <summary>
/// Result of P2P topology initialization.
/// </summary>
public sealed class P2PTopologyResult
{
    /// <summary>Gets a value indicating whether initialization was successful.</summary>
    public bool IsSuccessful { get; init; }

    /// <summary>Gets the number of devices in the topology.</summary>
    public int DeviceCount { get; init; }

    /// <summary>Gets the number of P2P-enabled device pairs.</summary>
    public int P2PEnabledPairs { get; init; }

    /// <summary>Gets the total number of device pairs.</summary>
    public int TotalPairs { get; init; }

    /// <summary>Gets a descriptive message.</summary>
    public string? Message { get; init; }
}

/// <summary>
/// P2P connection information between two devices.
/// </summary>
public sealed record P2PConnectionInfo
{
    /// <summary>Gets the source device.</summary>
    public required IAccelerator SourceDevice { get; init; }

    /// <summary>Gets the destination device.</summary>
    public required IAccelerator DestinationDevice { get; init; }

    /// <summary>Gets whether P2P is available.</summary>
    public bool IsP2PAvailable { get; init; }

    /// <summary>Gets the connection type (NVLink, PCIe, etc.).</summary>
    public string? ConnectionType { get; init; }

    /// <summary>Gets the estimated bandwidth in GB/s.</summary>
    public double EstimatedBandwidthGBps { get; init; }

    /// <summary>Gets the estimated latency in microseconds.</summary>
    public double EstimatedLatencyUs { get; init; }
}

