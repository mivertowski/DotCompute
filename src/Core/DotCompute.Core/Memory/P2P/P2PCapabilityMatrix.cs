// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Core.Logging;
using DotCompute.Core.Memory.P2P.Types;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// P2P Capability Matrix that maintains comprehensive topology information
    /// and provides fast lookups for device-to-device capabilities.
    /// </summary>
    public sealed partial class P2PCapabilityMatrix : IAsyncDisposable
    {
        #region LoggerMessage Delegates

        [LoggerMessage(EventId = 14801, Level = MsLogLevel.Trace, Message = "Checking for expired P2P capabilities")]
        private static partial void LogCheckingExpiredCapabilities(ILogger logger);

        [LoggerMessage(EventId = 14802, Level = MsLogLevel.Warning, Message = "Error during capability refresh")]
        private static partial void LogCapabilityRefreshError(ILogger logger, Exception ex);

        #endregion

        private readonly ILogger _logger;
        private readonly P2PCapabilityDetector _detector;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, P2PConnectionCapability>> _matrix;
        private readonly ConcurrentDictionary<string, DeviceTopologyInfo> _deviceTopology;
        private readonly SemaphoreSlim _matrixSemaphore;
        private readonly Timer? _refreshTimer;
        private readonly P2PMatrixStatistics _statistics;
        private bool _disposed;

        // Matrix configuration
        private const int MatrixRefreshIntervalMs = 30000; // 30 seconds
        /// <summary>
        /// Initializes a new instance of the P2PCapabilityMatrix class.
        /// </summary>
        /// <param name="logger">The logger.</param>

        public P2PCapabilityMatrix(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _detector = new P2PCapabilityDetector(logger);
            _matrix = new ConcurrentDictionary<string, ConcurrentDictionary<string, P2PConnectionCapability>>();
            _deviceTopology = new ConcurrentDictionary<string, DeviceTopologyInfo>();
            _matrixSemaphore = new SemaphoreSlim(1, 1);
            _statistics = new P2PMatrixStatistics();

            // Start periodic refresh timer
            _refreshTimer = new Timer(RefreshExpiredCapabilities, null,
                TimeSpan.FromMilliseconds(MatrixRefreshIntervalMs),
                TimeSpan.FromMilliseconds(MatrixRefreshIntervalMs));

            _logger.LogDebugMessage("P2P Capability Matrix initialized");
        }

        /// <summary>
        /// Builds the complete P2P capability matrix for the provided devices.
        /// </summary>
        public async Task BuildMatrixAsync(
            IAccelerator[] devices,

            CancellationToken cancellationToken = default)
        {
            if (devices == null || devices.Length == 0)
            {

                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }


            _logger.LogInfoMessage("Building P2P capability matrix for {devices.Length} devices");

            await _matrixSemaphore.WaitAsync(cancellationToken);
            try
            {
                var buildStartTime = DateTimeOffset.UtcNow;
                var detectionTasks = new List<Task>();
                var totalPairs = devices.Length * (devices.Length - 1) / 2;
                var completedPairs = 0;

                // Initialize device topology information
                foreach (var device in devices)
                {
                    await InitializeDeviceTopologyAsync(device, cancellationToken);
                }

                // Detect capabilities for all device pairs
                for (var i = 0; i < devices.Length; i++)
                {
                    var device1 = devices[i];

                    // Ensure device1 has an entry in the matrix

                    _ = _matrix.TryAdd(device1.Info.Id, new ConcurrentDictionary<string, P2PConnectionCapability>());

                    for (var j = 0; j < devices.Length; j++)
                    {
                        if (i == j)
                        {
                            continue; // Skip self-pairs
                        }


                        var device2 = devices[j];
                        var detectionTask = DetectAndStoreCapabilityAsync(device1, device2, cancellationToken)
                            .ContinueWith(_ =>

                            {
                                var completed = Interlocked.Increment(ref completedPairs);
                                if (completed % 5 == 0 || completed == totalPairs)
                                {
                                    _logger.LogDebugMessage("P2P capability detection progress: {Completed}/{completed, totalPairs}");
                                }
                            }, TaskScheduler.Default);

                        detectionTasks.Add(detectionTask);
                    }
                }

                await Task.WhenAll(detectionTasks);

                var buildDuration = DateTimeOffset.UtcNow - buildStartTime;

                // Calculate matrix statistics
                CalculateMatrixStatistics(devices, buildDuration);

                _logger.LogInfoMessage($"P2P capability matrix built in {buildDuration.TotalMilliseconds}ms: {_statistics.TotalConnections} connections, {_statistics.P2PEnabledConnections} P2P-enabled");
            }
            finally
            {
                _ = _matrixSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets P2P capability between two specific devices.
        /// </summary>
        public async Task<P2PConnectionCapability> GetP2PCapabilityAsync(
            IAccelerator device1,
            IAccelerator device2,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(device1);


            ArgumentNullException.ThrowIfNull(device2);


            if (device1.Info.Id == device2.Info.Id)
            {
                return new P2PConnectionCapability
                {
                    IsSupported = false,
                    ConnectionType = P2PConnectionType.None,
                    EstimatedBandwidthGBps = 0.0,
                    LimitationReason = "Same device"
                };
            }

            // Try to get from cache first
            if (_matrix.TryGetValue(device1.Info.Id, out var device1Connections) &&
                device1Connections.TryGetValue(device2.Info.Id, out var cachedCapability))
            {
                // Check if capability is still fresh
                if (IsCapabilityFresh(cachedCapability))
                {
                    return cachedCapability;
                }
            }

            // Not in cache or expired, detect capability
            var capability = await _detector.DetectP2PCapabilityAsync(device1, device2, cancellationToken);

            // Store in matrix

            await StoreCapabilityAsync(device1.Info.Id, device2.Info.Id, capability);

            return capability;
        }

        /// <summary>
        /// Gets the optimal P2P path between two devices, potentially through intermediate devices.
        /// </summary>
        public async Task<P2PPath?> FindOptimalP2PPathAsync(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            CancellationToken cancellationToken = default)
        {
            if (sourceDevice.Info.Id == targetDevice.Info.Id)
            {

                return null;
            }

            // First try direct connection

            var directCapability = await GetP2PCapabilityAsync(sourceDevice, targetDevice, cancellationToken);
            if (directCapability.IsSupported)
            {
                return new P2PPath
                {
                    SourceDevice = sourceDevice,
                    TargetDevice = targetDevice,
                    IntermediateDevices = [],
                    TotalBandwidthGBps = directCapability.EstimatedBandwidthGBps,
                    EstimatedLatencyMs = EstimateLatency(directCapability.ConnectionType),
                    Hops = 1,
                    IsDirectP2P = true
                };
            }

            // If no direct connection, try to find path through intermediate devices
            return await FindMultiHopP2PPathAsync(sourceDevice, targetDevice, cancellationToken);
        }

        /// <summary>
        /// Gets all P2P-capable devices for a specific device.
        /// </summary>
        public Task<List<P2PConnection>> GetP2PConnectionsAsync(
            IAccelerator device,
            CancellationToken cancellationToken = default)
        {
            var connections = new List<P2PConnection>();

            if (_matrix.TryGetValue(device.Info.Id, out var deviceConnections))
            {
                foreach (var kvp in deviceConnections)
                {
                    var capability = kvp.Value;
                    if (capability.IsSupported)
                    {
                        // Find the target device info (simplified - would need device registry in real implementation) - TODO
                        var connection = new P2PConnection
                        {
                            SourceDeviceId = device.Info.Id,
                            TargetDeviceId = kvp.Key,
                            Capability = capability,
                            LastValidated = GetCapabilityTimestamp(capability)
                        };
                        connections.Add(connection);
                    }
                }
            }

            return Task.FromResult(connections);
        }

        /// <summary>
        /// Gets topology analysis for the entire device matrix.
        /// </summary>
        public P2PTopologyAnalysis GetTopologyAnalysis()
        {
            var analysis = new P2PTopologyAnalysis();

            // Calculate topology metrics

            var totalDevices = _deviceTopology.Count;
            var totalConnections = _statistics.TotalConnections;
            var p2pConnections = _statistics.P2PEnabledConnections;


            analysis.TotalDevices = totalDevices;
            analysis.TotalPossibleConnections = totalDevices * (totalDevices - 1);
            analysis.P2PEnabledConnections = p2pConnections;
            analysis.P2PConnectivityRatio = totalConnections > 0 ? (double)p2pConnections / totalConnections : 0.0;

            // Identify topology clusters and bottlenecks
            var topologyClusters = IdentifyTopologyClusters();
            analysis.TopologyClusters.Clear();
            foreach (var cluster in topologyClusters)
            {
                analysis.TopologyClusters.Add(cluster);
            }

            var bandwidthBottlenecks = IdentifyBandwidthBottlenecks();
            analysis.BandwidthBottlenecks.Clear();
            foreach (var bottleneck in bandwidthBottlenecks)
            {
                analysis.BandwidthBottlenecks.Add(bottleneck);
            }

            var highPerformancePaths = IdentifyHighPerformancePaths();
            analysis.HighPerformancePaths.Clear();
            foreach (var path in highPerformancePaths)
            {
                analysis.HighPerformancePaths.Add(path);
            }

            // Calculate average bandwidth by connection type
            analysis.AverageNVLinkBandwidth = CalculateAverageConnectionBandwidth(P2PConnectionType.NVLink);
            analysis.AveragePCIeBandwidth = CalculateAverageConnectionBandwidth(P2PConnectionType.PCIe);
            analysis.AverageInfiniBandBandwidth = CalculateAverageConnectionBandwidth(P2PConnectionType.InfiniBand);

            return analysis;
        }

        /// <summary>
        /// Validates the integrity of the capability matrix.
        /// </summary>
        public async Task<P2PMatrixValidationResult> ValidateMatrixIntegrityAsync(
            CancellationToken cancellationToken = default)
        {
            var validationResult = new P2PMatrixValidationResult { IsValid = true };
            var issues = new List<string>();

            await _matrixSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Check for symmetry issues
                foreach (var device1Entry in _matrix)
                {
                    foreach (var device2Entry in device1Entry.Value)
                    {
                        var device1Id = device1Entry.Key;
                        var device2Id = device2Entry.Key;
                        var capability12 = device2Entry.Value;

                        // Check if reverse connection exists and matches
                        if (_matrix.TryGetValue(device2Id, out var reverseConnections) &&
                            reverseConnections.TryGetValue(device1Id, out var capability21))
                        {
                            if (capability12.IsSupported != capability21.IsSupported)
                            {
                                issues.Add($"Asymmetric P2P capability: {device1Id}<->{device2Id}");
                            }

                            var bandwidthDiff = Math.Abs(capability12.EstimatedBandwidthGBps - capability21.EstimatedBandwidthGBps);
                            if (bandwidthDiff > capability12.EstimatedBandwidthGBps * 0.1) // >10% difference
                            {
                                issues.Add($"Significant bandwidth asymmetry: {device1Id}<->{device2Id}");
                            }
                        }
                        else
                        {
                            issues.Add($"Missing reverse connection: {device2Id}->{device1Id}");
                        }
                    }
                }

                // Check for expired capabilities
                var expiredCapabilities = CountExpiredCapabilities();
                if (expiredCapabilities > _statistics.TotalConnections * 0.25) // >25% expired
                {
                    issues.Add($"High number of expired capabilities: {expiredCapabilities}");
                }

                validationResult.Issues.Clear();
                foreach (var issue in issues)
                {
                    validationResult.Issues.Add(issue);
                }
                validationResult.IsValid = issues.Count == 0;
                validationResult.ValidationTime = DateTimeOffset.UtcNow;

                _logger.LogDebugMessage($"Matrix validation completed: {validationResult.IsValid}, {issues.Count} issues found");

                return validationResult;
            }
            finally
            {
                _ = _matrixSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets comprehensive matrix statistics.
        /// </summary>
        public P2PMatrixStatistics GetMatrixStatistics()
        {
            lock (_statistics)
            {
                return new P2PMatrixStatistics
                {
                    TotalDevices = _statistics.TotalDevices,
                    TotalConnections = _statistics.TotalConnections,
                    P2PEnabledConnections = _statistics.P2PEnabledConnections,
                    NVLinkConnections = _statistics.NVLinkConnections,
                    PCIeConnections = _statistics.PCIeConnections,
                    InfiniBandConnections = _statistics.InfiniBandConnections,
                    AverageBandwidthGBps = _statistics.AverageBandwidthGBps,
                    PeakBandwidthGBps = _statistics.PeakBandwidthGBps,
                    LastRefreshTime = _statistics.LastRefreshTime,
                    MatrixBuildTime = _statistics.MatrixBuildTime,
                    CacheHitRatio = CalculateCacheHitRatio()
                };
            }
        }

        #region Private Implementation

        private async Task InitializeDeviceTopologyAsync(IAccelerator device, CancellationToken cancellationToken)
        {
            var capabilities = await _detector.GetDeviceCapabilitiesAsync(device, cancellationToken);


            var topologyInfo = new DeviceTopologyInfo
            {
                DeviceId = device.Info.Id,
                DeviceName = device.Info.Name,
                DeviceType = device.Info.Type.ToString(),
                SupportsP2P = capabilities.SupportsP2P,
                MemoryBandwidthGBps = capabilities.MemoryBandwidthGBps,
                MaxP2PBandwidthGBps = capabilities.P2PBandwidthGBps,
                LastUpdated = DateTimeOffset.UtcNow
            };

            _deviceTopology[device.Info.Id] = topologyInfo;
        }

        private async Task DetectAndStoreCapabilityAsync(
            IAccelerator device1,
            IAccelerator device2,
            CancellationToken cancellationToken)
        {
            var capability = await _detector.DetectP2PCapabilityAsync(device1, device2, cancellationToken);

            // Add timestamp for cache management

            var timestampedCapability = new P2PConnectionCapability
            {
                IsSupported = capability.IsSupported,
                ConnectionType = capability.ConnectionType,
                EstimatedBandwidthGBps = capability.EstimatedBandwidthGBps,
                LimitationReason = capability.LimitationReason
            };

            await StoreCapabilityAsync(device1.Info.Id, device2.Info.Id, timestampedCapability);
        }

        private async Task StoreCapabilityAsync(string device1Id, string device2Id, P2PConnectionCapability capability)
        {
            // Ensure both devices have matrix entries
            _ = _matrix.TryAdd(device1Id, new ConcurrentDictionary<string, P2PConnectionCapability>());
            _ = _matrix.TryAdd(device2Id, new ConcurrentDictionary<string, P2PConnectionCapability>());

            // Store bidirectional capability
            _matrix[device1Id][device2Id] = capability;
            _matrix[device2Id][device1Id] = capability; // Symmetric

            await Task.CompletedTask;
        }

        private Task<P2PPath?> FindMultiHopP2PPathAsync(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            CancellationToken cancellationToken)
        {
            // Simplified multi-hop pathfinding using breadth-first search
            var visited = new HashSet<string>();
            var queue = new Queue<P2PPathCandidate>();


            queue.Enqueue(new P2PPathCandidate
            {
                CurrentDevice = sourceDevice.Info.Id,
                Path = [sourceDevice.Info.Id],
                TotalBandwidth = double.MaxValue,
                TotalLatency = 0
            });

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();


                if (current.CurrentDevice != null && visited.Contains(current.CurrentDevice))
                {
                    continue;
                }


                if (current.CurrentDevice != null)
                {
                    _ = visited.Add(current.CurrentDevice);
                }

                // Check all connections from current device
                if (current.CurrentDevice != null && _matrix.TryGetValue(current.CurrentDevice, out var connections))
                {
                    foreach (var connection in connections)
                    {
                        if (!connection.Value.IsSupported || visited.Contains(connection.Key))
                        {
                            continue;
                        }

                        // Find device object for connection.Key (simplified - would need device registry)
                        // This is a placeholder - real implementation would maintain device registry - TODO


                        if (connection.Key == targetDevice.Info.Id)
                        {
                            // Found path to target
                            var intermediatePath = current.Path.Skip(1).Take(current.Path.Count - 2).ToArray();


                            return Task.FromResult<P2PPath?>(new P2PPath
                            {
                                SourceDevice = sourceDevice,
                                TargetDevice = targetDevice,
                                IntermediateDevices = intermediatePath,
                                TotalBandwidthGBps = Math.Min(current.TotalBandwidth, connection.Value.EstimatedBandwidthGBps),
                                EstimatedLatencyMs = current.TotalLatency + EstimateLatency(connection.Value.ConnectionType),
                                Hops = current.Path.Count,
                                IsDirectP2P = false
                            });
                        }

                        // Add to exploration queue (limit depth to prevent infinite loops)
                        if (current.Path.Count < 4) // Max 3 hops
                        {
                            queue.Enqueue(new P2PPathCandidate
                            {
                                CurrentDevice = current.CurrentDevice, // Placeholder - would be actual device
                                Path = [.. current.Path, .. new[] { current.CurrentDevice }],
                                TotalBandwidth = Math.Min(current.TotalBandwidth, connection.Value.EstimatedBandwidthGBps),
                                TotalLatency = current.TotalLatency + EstimateLatency(connection.Value.ConnectionType)
                            });
                        }
                    }
                }
            }

            return Task.FromResult<P2PPath?>(null); // No path found
        }

        private void CalculateMatrixStatistics(IAccelerator[] devices, TimeSpan buildDuration)
        {
            lock (_statistics)
            {
                _statistics.TotalDevices = devices.Length;
                _statistics.TotalConnections = 0;
                _statistics.P2PEnabledConnections = 0;
                _statistics.NVLinkConnections = 0;
                _statistics.PCIeConnections = 0;
                _statistics.InfiniBandConnections = 0;

                var bandwidths = new List<double>();

                foreach (var device1Connections in _matrix.Values)
                {
                    foreach (var capability in device1Connections.Values)
                    {
                        _statistics.TotalConnections++;


                        if (capability.IsSupported)
                        {
                            _statistics.P2PEnabledConnections++;
                            bandwidths.Add(capability.EstimatedBandwidthGBps);

                            switch (capability.ConnectionType)
                            {
                                case P2PConnectionType.NVLink:
                                    _statistics.NVLinkConnections++;
                                    break;
                                case P2PConnectionType.PCIe:
                                    _statistics.PCIeConnections++;
                                    break;
                                case P2PConnectionType.InfiniBand:
                                    _statistics.InfiniBandConnections++;
                                    break;
                            }
                        }
                    }
                }

                if (bandwidths.Count > 0)
                {
                    _statistics.AverageBandwidthGBps = bandwidths.Average();
                    _statistics.PeakBandwidthGBps = bandwidths.Max();
                }

                _statistics.LastRefreshTime = DateTimeOffset.UtcNow;
                _statistics.MatrixBuildTime = buildDuration;
            }
        }

        private static bool IsCapabilityFresh(P2PConnectionCapability capability)
            // In a real implementation, capabilities would have timestamps
            // For now, assume all cached capabilities are fresh - TODO




            => true;

        private static DateTimeOffset GetCapabilityTimestamp(P2PConnectionCapability capability)
            // Placeholder - would return actual timestamp in real implementation - TODO




            => DateTimeOffset.UtcNow;

        private static double EstimateLatency(P2PConnectionType connectionType)
        {
            return connectionType switch
            {
                P2PConnectionType.NVLink => 0.5, // 0.5ms
                P2PConnectionType.PCIe => 1.0,   // 1.0ms
                P2PConnectionType.InfiniBand => 0.1, // 0.1ms
                _ => 2.0 // 2.0ms fallback
            };
        }

        private List<P2PTopologyCluster> IdentifyTopologyClusters()
        {
            // Simplified cluster identification
            var clusters = new List<P2PTopologyCluster>();

            // Group devices by connection type affinity

            var nvlinkDevices = new List<string>();
            var pcieDevices = new List<string>();
            var infinibandDevices = new List<string>();

            foreach (var deviceEntry in _matrix)
            {
                var hasNVLink = deviceEntry.Value.Any(c => c.Value.ConnectionType == P2PConnectionType.NVLink && c.Value.IsSupported);
                var hasInfiniBand = deviceEntry.Value.Any(c => c.Value.ConnectionType == P2PConnectionType.InfiniBand && c.Value.IsSupported);


                if (hasNVLink)
                {
                    nvlinkDevices.Add(deviceEntry.Key);
                }
                else if (hasInfiniBand)
                {
                    infinibandDevices.Add(deviceEntry.Key);
                }
                else
                {
                    pcieDevices.Add(deviceEntry.Key);
                }
            }

            if (nvlinkDevices.Count > 1)
            {
                clusters.Add(new P2PTopologyCluster
                {
                    ClusterType = "NVLink",
                    DeviceIds = nvlinkDevices,
                    AverageBandwidth = CalculateAverageConnectionBandwidth(P2PConnectionType.NVLink),
                    InterconnectDensity = CalculateInterconnectDensity(nvlinkDevices)
                });
            }

            if (infinibandDevices.Count > 1)
            {
                clusters.Add(new P2PTopologyCluster
                {
                    ClusterType = "InfiniBand",
                    DeviceIds = infinibandDevices,
                    AverageBandwidth = CalculateAverageConnectionBandwidth(P2PConnectionType.InfiniBand),
                    InterconnectDensity = CalculateInterconnectDensity(infinibandDevices)
                });
            }

            if (pcieDevices.Count > 1)
            {
                clusters.Add(new P2PTopologyCluster
                {
                    ClusterType = "PCIe",
                    DeviceIds = pcieDevices,
                    AverageBandwidth = CalculateAverageConnectionBandwidth(P2PConnectionType.PCIe),
                    InterconnectDensity = CalculateInterconnectDensity(pcieDevices)
                });
            }

            return clusters;
        }

        private List<P2PBandwidthBottleneck> IdentifyBandwidthBottlenecks()
        {
            var bottlenecks = new List<P2PBandwidthBottleneck>();
            var allBandwidths = new List<double>();

            // Collect all P2P bandwidths
            foreach (var deviceConnections in _matrix.Values)
            {
                foreach (var capability in deviceConnections.Values)
                {
                    if (capability.IsSupported)
                    {
                        allBandwidths.Add(capability.EstimatedBandwidthGBps);
                    }
                }
            }

            if (allBandwidths.Count == 0)
            {
                return bottlenecks;
            }


            var averageBandwidth = allBandwidths.Average();
            var threshold = averageBandwidth * 0.5; // Consider connections with <50% average bandwidth as bottlenecks

            foreach (var device1Entry in _matrix)
            {
                foreach (var device2Entry in device1Entry.Value)
                {
                    var capability = device2Entry.Value;
                    if (capability.IsSupported && capability.EstimatedBandwidthGBps < threshold)
                    {
                        bottlenecks.Add(new P2PBandwidthBottleneck
                        {
                            Device1Id = device1Entry.Key,
                            Device2Id = device2Entry.Key,
                            BandwidthGBps = capability.EstimatedBandwidthGBps,
                            ExpectedBandwidthGBps = averageBandwidth,
                            ConnectionType = capability.ConnectionType
                        });
                    }
                }
            }

            return [.. bottlenecks.Take(10)]; // Return top 10 bottlenecks
        }

        private List<P2PHighPerformancePath> IdentifyHighPerformancePaths()
        {
            var highPerfPaths = new List<P2PHighPerformancePath>();
            var allBandwidths = new List<double>();

            // Collect all P2P bandwidths
            foreach (var deviceConnections in _matrix.Values)
            {
                foreach (var capability in deviceConnections.Values)
                {
                    if (capability.IsSupported)
                    {
                        allBandwidths.Add(capability.EstimatedBandwidthGBps);
                    }
                }
            }

            if (allBandwidths.Count == 0)
            {
                return highPerfPaths;
            }


            var averageBandwidth = allBandwidths.Average();
            var threshold = averageBandwidth * 1.5; // Consider connections with >150% average bandwidth as high-performance

            foreach (var device1Entry in _matrix)
            {
                foreach (var device2Entry in device1Entry.Value)
                {
                    var capability = device2Entry.Value;
                    if (capability.IsSupported && capability.EstimatedBandwidthGBps > threshold)
                    {
                        highPerfPaths.Add(new P2PHighPerformancePath
                        {
                            Device1Id = device1Entry.Key,
                            Device2Id = device2Entry.Key,
                            BandwidthGBps = capability.EstimatedBandwidthGBps,
                            ConnectionType = capability.ConnectionType,
                            PerformanceRatio = capability.EstimatedBandwidthGBps / averageBandwidth
                        });
                    }
                }
            }

            return [.. highPerfPaths.OrderByDescending(p => p.BandwidthGBps).Take(10)];
        }

        private double CalculateAverageConnectionBandwidth(P2PConnectionType connectionType)
        {
            var bandwidths = new List<double>();

            foreach (var deviceConnections in _matrix.Values)
            {
                foreach (var capability in deviceConnections.Values)
                {
                    if (capability.IsSupported && capability.ConnectionType == connectionType)
                    {
                        bandwidths.Add(capability.EstimatedBandwidthGBps);
                    }
                }
            }

            return bandwidths.Count > 0 ? bandwidths.Average() : 0.0;
        }

        private double CalculateInterconnectDensity(IReadOnlyList<string> deviceIds)
        {
            if (deviceIds.Count <= 1)
            {
                return 0.0;
            }


            var possibleConnections = deviceIds.Count * (deviceIds.Count - 1);
            var actualConnections = 0;

            foreach (var device1Id in deviceIds)
            {
                if (_matrix.TryGetValue(device1Id, out var connections))
                {
                    actualConnections += deviceIds.Count(device2Id =>

                        device1Id != device2Id &&

                        connections.TryGetValue(device2Id, out var cap) &&

                        cap.IsSupported);
                }
            }

            return possibleConnections > 0 ? (double)actualConnections / possibleConnections : 0.0;
        }

        private static int CountExpiredCapabilities()
            // Placeholder - in real implementation would check timestamps - TODO




            => 0;

        private static double CalculateCacheHitRatio()
            // Placeholder - would track cache hits/misses in real implementation - TODO




            => 0.95; // Assume 95% cache hit ratio

        private void RefreshExpiredCapabilities(object? state)
        {
            try
            {
                // Background task to refresh expired capabilities
                // In a real implementation, this would check timestamps and refresh expired entries - TODO
                LogCheckingExpiredCapabilities(_logger);
            }
            catch (Exception ex)
            {
                LogCapabilityRefreshError(_logger, ex);
            }
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        #endregion

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }


            _disposed = true;

            _refreshTimer?.Dispose();
            await _detector.DisposeAsync();
            _matrixSemaphore.Dispose();

            _matrix.Clear();
            _deviceTopology.Clear();

            _logger.LogDebugMessage("P2P Capability Matrix disposed");
        }
    }

    #region Supporting Types

    #endregion
}
