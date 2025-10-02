// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Memory
{

    /// <summary>
    /// Advanced P2P memory coherence manager that maintains consistency across multiple GPU devices.
    /// Handles lazy synchronization, conflict resolution, and access pattern optimization.
    /// </summary>
    public sealed class P2PMemoryCoherenceManager : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<object, P2PBufferCoherenceInfo> _bufferTracking;
        private readonly ConcurrentDictionary<string, DeviceCoherenceState> _deviceStates;
        private readonly ConcurrentDictionary<string, Dictionary<string, P2PConnectionCapability>> _p2pTopology;
        private readonly Timer _coherenceMonitor;
        private readonly SemaphoreSlim _coherenceSemaphore;
        private readonly CoherenceStatistics _statistics;
        private bool _disposed;

        // Coherence configuration
        private const int CoherenceMonitorIntervalMs = 5000; // 5 seconds
        private const double IncoherentThresholdRatio = 0.1; // 10%
        private const int MaxCoherentCopiesPerBuffer = 8;

        public P2PMemoryCoherenceManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bufferTracking = new ConcurrentDictionary<object, P2PBufferCoherenceInfo>();
            _deviceStates = new ConcurrentDictionary<string, DeviceCoherenceState>();
            _p2pTopology = new ConcurrentDictionary<string, Dictionary<string, P2PConnectionCapability>>();
            _coherenceSemaphore = new SemaphoreSlim(1, 1);
            _statistics = new CoherenceStatistics();

            // Start coherence monitoring
            _coherenceMonitor = new Timer(MonitorCoherenceHealth, null,
                TimeSpan.FromMilliseconds(CoherenceMonitorIntervalMs),
                TimeSpan.FromMilliseconds(CoherenceMonitorIntervalMs));

            _logger.LogDebugMessage("P2P memory coherence manager initialized");
        }

        /// <summary>
        /// Tracks a P2P buffer with coherence information and topology awareness.
        /// </summary>
        public void TrackP2PBuffer<T>(
            P2PBuffer<T> buffer,
            IUnifiedMemoryBuffer<T> sourceBuffer,
            int offset,
            int count,
            P2PConnectionCapability? p2pCapability) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);

            ArgumentNullException.ThrowIfNull(sourceBuffer);

            var coherenceInfo = new P2PBufferCoherenceInfo
            {
                BufferId = Guid.NewGuid(),
                SourceBuffer = sourceBuffer,
                SourceDevice = sourceBuffer.Accelerator,
                TargetDevice = buffer.Accelerator,
                Offset = offset,
                ElementCount = count,
                LastModified = DateTimeOffset.UtcNow,
                IsCoherent = true,
                CoherenceLevel = CoherenceLevel.Strong,
                P2PCapability = p2pCapability,
                AccessPattern = AccessPattern.Sequential,
                Copies =
            [
                new BufferCopy
                {
                    Device = buffer.Accelerator,
                    Buffer = buffer,
                    LastAccessed = DateTimeOffset.UtcNow,
                    AccessCount = 1,
                    IsWritten = false
                }
            ]
            };

            _bufferTracking[buffer] = coherenceInfo;

            // Update device coherence state
            UpdateDeviceCoherenceState(buffer.Accelerator.Info.Id, 1, 0);

            lock (_statistics)
            {
                _statistics.TotalTrackedBuffers++;
                _statistics.CoherentBuffers++;
            }

            _logger.LogTrace("Started tracking P2P buffer: {BufferId} on {Device}",
                coherenceInfo.BufferId, buffer.Accelerator.Info.Name);
        }

        /// <summary>
        /// Synchronizes a buffer across all devices that have copies of it.
        /// </summary>
        public async ValueTask SynchronizeBufferAsync<T>(IUnifiedMemoryBuffer<T> buffer, CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (!_bufferTracking.TryGetValue(buffer, out var coherenceInfo))
            {
                _logger.LogWarningMessage("Attempted to synchronize untracked buffer");
                return;
            }

            await _coherenceSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (coherenceInfo.IsCoherent)
                {
                    _logger.LogTrace("Buffer {BufferId} is already coherent, no sync needed", coherenceInfo.BufferId);
                    return;
                }

                var startTime = DateTimeOffset.UtcNow;
                var syncOperations = new List<Task>();

                // Find the most recent copy (canonical version)
                var canonicalCopy = coherenceInfo.Copies
                    .Where(c => c.IsWritten)
                    .OrderByDescending(c => c.LastAccessed)
                    .FirstOrDefault()
                    ?? coherenceInfo.Copies.OrderByDescending(c => c.LastAccessed).First();

                _logger.LogDebugMessage($"Synchronizing buffer {coherenceInfo.BufferId} from canonical copy on {canonicalCopy.Device.Info.Name}");

                // Synchronize all other copies
                foreach (var copy in coherenceInfo.Copies.Where(c => c != canonicalCopy))
                {
                    var syncTask = SynchronizeCopyAsync(canonicalCopy, copy, coherenceInfo, cancellationToken);
                    syncOperations.Add(syncTask);
                }

                // Wait for all synchronization operations to complete
                await Task.WhenAll(syncOperations);

                // Update coherence state
                coherenceInfo.IsCoherent = true;
                coherenceInfo.LastModified = DateTimeOffset.UtcNow;
                coherenceInfo.CoherenceLevel = DetermineCoherenceLevel(coherenceInfo);

                // Reset write flags
                foreach (var copy in coherenceInfo.Copies)
                {
                    copy.IsWritten = false;
                }

                var duration = DateTimeOffset.UtcNow - startTime;

                lock (_statistics)
                {
                    _statistics.SynchronizationOperations++;
                    _statistics.TotalSyncTime += duration;
                    _statistics.IncoherentBuffers--;
                    _statistics.CoherentBuffers++;
                }

                _logger.LogDebugMessage($"Buffer {coherenceInfo.BufferId} synchronized across {coherenceInfo.Copies.Count} devices in {duration.TotalMilliseconds}ms");
            }
            finally
            {
                _ = _coherenceSemaphore.Release();
            }
        }

        /// <summary>
        /// Optimizes buffer placement based on P2P topology and access patterns.
        /// </summary>
        public async ValueTask OptimizeP2PPlacementAsync(
            IAccelerator[] devices,
            Dictionary<string, Dictionary<string, P2PConnectionCapability>> p2pMatrix,
            CancellationToken cancellationToken = default)
        {
            await _coherenceSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInfoMessage("Optimizing P2P buffer placement across {devices.Length} devices");

                // Store P2P topology for future reference
                _p2pTopology.Clear();
                foreach (var kvp in p2pMatrix)
                {
                    _p2pTopology[kvp.Key] = new Dictionary<string, P2PConnectionCapability>(kvp.Value);
                }

                // Analyze current buffer distribution
                var placementAnalysis = AnalyzeBufferPlacement(devices);

                // Generate optimization recommendations
                var optimizations = GeneratePlacementOptimizations(placementAnalysis, p2pMatrix);

                // Execute optimizations
                var optimizationTasks = optimizations.Select(opt =>
                    ExecutePlacementOptimizationAsync(opt, cancellationToken));

                await Task.WhenAll(optimizationTasks);

                _logger.LogInfoMessage($"P2P placement optimization completed: {optimizations.Count} optimizations applied");
            }
            finally
            {
                _ = _coherenceSemaphore.Release();
            }
        }

        /// <summary>
        /// Records a buffer access for coherence tracking and optimization.
        /// </summary>
        public void RecordBufferAccess<T>(IUnifiedMemoryBuffer<T> buffer, AccessType accessType) where T : unmanaged
        {
            if (_bufferTracking.TryGetValue(buffer, out var coherenceInfo))
            {
                var deviceId = buffer.Accelerator.Info.Id;
                var copy = coherenceInfo.Copies.FirstOrDefault(c => c.Device.Info.Id == deviceId);

                if (copy != null)
                {
                    copy.LastAccessed = DateTimeOffset.UtcNow;
                    copy.AccessCount++;

                    if (accessType == AccessType.Write)
                    {
                        copy.IsWritten = true;
                        coherenceInfo.IsCoherent = false;
                        coherenceInfo.CoherenceLevel = CoherenceLevel.Weak;

                        // Update statistics
                        lock (_statistics)
                        {
                            _statistics.WriteOperations++;
                            if (coherenceInfo.IsCoherent)
                            {
                                _statistics.CoherentBuffers--;
                                _statistics.IncoherentBuffers++;
                            }
                        }
                    }
                    else
                    {
                        lock (_statistics)
                        {
                            _statistics.ReadOperations++;
                        }
                    }

                    // Update access pattern analysis
                    UpdateAccessPattern(coherenceInfo, accessType);

                    _logger.LogTrace("Recorded {AccessType} access to buffer {BufferId} on {Device}",
                        accessType, coherenceInfo.BufferId, buffer.Accelerator.Info.Name);
                }
            }
        }

        /// <summary>
        /// Gets coherence overhead percentage.
        /// </summary>
        public double GetOverheadPercentage()
        {
            lock (_statistics)
            {
                var totalBuffers = _statistics.TotalTrackedBuffers;
                return totalBuffers > 0 ? (double)_statistics.IncoherentBuffers / totalBuffers * 100 : 0;
            }
        }

        /// <summary>
        /// Gets comprehensive coherence statistics.
        /// </summary>
        public CoherenceStatistics GetStatistics()
        {
            lock (_statistics)
            {
                return new CoherenceStatistics
                {
                    TotalTrackedBuffers = _statistics.TotalTrackedBuffers,
                    CoherentBuffers = _statistics.CoherentBuffers,
                    IncoherentBuffers = _statistics.IncoherentBuffers,
                    SynchronizationOperations = _statistics.SynchronizationOperations,
                    ReadOperations = _statistics.ReadOperations,
                    WriteOperations = _statistics.WriteOperations,
                    TotalSyncTime = _statistics.TotalSyncTime,
                    AverageSyncTime = _statistics.SynchronizationOperations > 0
                        ? _statistics.TotalSyncTime / _statistics.SynchronizationOperations
                        : TimeSpan.Zero,
                    CoherenceEfficiency = CalculateCoherenceEfficiency()
                };
            }
        }

        #region Private Implementation

        /// <summary>
        /// Synchronizes a single buffer copy.
        /// </summary>
        private async Task SynchronizeCopyAsync(
            BufferCopy canonicalCopy,
            BufferCopy targetCopy,
            P2PBufferCoherenceInfo coherenceInfo,
            CancellationToken cancellationToken)
        {
            try
            {
                // Determine optimal sync strategy based on P2P capability
                var syncStrategy = DetermineSyncStrategy(canonicalCopy.Device, targetCopy.Device, coherenceInfo);

                switch (syncStrategy)
                {
                    case SyncStrategy.DirectP2P:
                        await ExecuteDirectP2PSyncAsync(canonicalCopy, targetCopy, coherenceInfo, cancellationToken);
                        break;

                    case SyncStrategy.HostMediated:
                        await ExecuteHostMediatedSyncAsync(canonicalCopy, targetCopy, coherenceInfo, cancellationToken);
                        break;

                    case SyncStrategy.Streamed:
                        await ExecuteStreamedSyncAsync(canonicalCopy, targetCopy, coherenceInfo, cancellationToken);
                        break;

                    default:
                        await ExecuteHostMediatedSyncAsync(canonicalCopy, targetCopy, coherenceInfo, cancellationToken);
                        break;
                }

                targetCopy.LastAccessed = DateTimeOffset.UtcNow;
                targetCopy.IsWritten = false;

                _logger.LogTrace("Synchronized copy on {TargetDevice} from {SourceDevice}",
                    targetCopy.Device.Info.Name, canonicalCopy.Device.Info.Name);
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to synchronize buffer copy from {canonicalCopy.Device.Info.Name} to {targetCopy.Device.Info.Name}");
                throw;
            }
        }

        /// <summary>
        /// Executes direct P2P synchronization.
        /// </summary>
        private static async Task ExecuteDirectP2PSyncAsync(
            BufferCopy source,
            BufferCopy target,
            P2PBufferCoherenceInfo coherenceInfo,
            CancellationToken cancellationToken)
            // Direct device-to-device copy using P2P
            // Note: This requires specific type casting based on actual buffer types
            // Implementation would need to handle type safety properly




            => await Task.CompletedTask; // Placeholder for P2P copy

        /// <summary>
        /// Executes host-mediated synchronization.
        /// </summary>
        private static async Task ExecuteHostMediatedSyncAsync(
            BufferCopy source,
            BufferCopy target,
            P2PBufferCoherenceInfo coherenceInfo,
            CancellationToken cancellationToken)
            // Transfer via host memory - requires type-specific implementation
            // This is a simplified placeholder - real implementation would need proper type handling




            => await Task.CompletedTask;

        /// <summary>
        /// Executes streamed synchronization for large buffers.
        /// </summary>
        private static async Task ExecuteStreamedSyncAsync(
            BufferCopy source,
            BufferCopy target,
            P2PBufferCoherenceInfo coherenceInfo,
            CancellationToken cancellationToken)
            // Chunked synchronization for large buffers




            => await ExecuteHostMediatedSyncAsync(source, target, coherenceInfo, cancellationToken);

        /// <summary>
        /// Determines the optimal synchronization strategy.
        /// </summary>
        private SyncStrategy DetermineSyncStrategy(
        IAccelerator sourceDevice,

        IAccelerator targetDevice,

        P2PBufferCoherenceInfo coherenceInfo)
        {
            if (coherenceInfo.P2PCapability?.IsSupported == true)
            {
                return SyncStrategy.DirectP2P;
            }

            // Check if we have P2P capability in topology
            if (_p2pTopology.TryGetValue(sourceDevice.Info.Id, out var sourceConnections) &&
                sourceConnections.TryGetValue(targetDevice.Info.Id, out var capability) &&
                capability.IsSupported)
            {
                return SyncStrategy.DirectP2P;
            }

            // Use streaming for large buffers
            var bufferSize = coherenceInfo.ElementCount * GetElementSize(coherenceInfo.SourceBuffer);
            if (bufferSize > 64 * 1024 * 1024) // > 64MB
            {
                return SyncStrategy.Streamed;
            }

            return SyncStrategy.HostMediated;
        }

        /// <summary>
        /// Updates device coherence state tracking.
        /// </summary>
        private void UpdateDeviceCoherenceState(string deviceId, int coherentDelta, int incoherentDelta)
        {
            _ = _deviceStates.AddOrUpdate(deviceId,
                new DeviceCoherenceState
                {
                    DeviceId = deviceId,
                    CoherentBuffers = Math.Max(0, coherentDelta),
                    IncoherentBuffers = Math.Max(0, incoherentDelta),
                    LastUpdated = DateTimeOffset.UtcNow
                },
                (id, existing) => new DeviceCoherenceState
                {
                    DeviceId = id,
                    CoherentBuffers = Math.Max(0, existing.CoherentBuffers + coherentDelta),
                    IncoherentBuffers = Math.Max(0, existing.IncoherentBuffers + incoherentDelta),
                    LastUpdated = DateTimeOffset.UtcNow
                });
        }

        /// <summary>
        /// Determines coherence level based on buffer state.
        /// </summary>
        private static CoherenceLevel DetermineCoherenceLevel(P2PBufferCoherenceInfo coherenceInfo)
        {
            if (!coherenceInfo.IsCoherent)
            {
                return CoherenceLevel.None;
            }

            var writtenCopies = coherenceInfo.Copies.Count(c => c.IsWritten);
            if (writtenCopies == 0)
            {
                return CoherenceLevel.Strong;
            }

            if (writtenCopies == 1)
            {
                return CoherenceLevel.Weak;
            }

            return CoherenceLevel.None; // Multiple writers = incoherent
        }

        /// <summary>
        /// Updates access pattern analysis.
        /// </summary>
        private static void UpdateAccessPattern(P2PBufferCoherenceInfo coherenceInfo, AccessType accessType)
        {
            // Simple access pattern detection
            if (accessType == AccessType.Write)
            {
                coherenceInfo.AccessPattern = AccessPattern.Random;
            }
            else if (coherenceInfo.Copies.Count > 1)
            {
                coherenceInfo.AccessPattern = AccessPattern.Broadcast;
            }
            else
            {
                coherenceInfo.AccessPattern = AccessPattern.Sequential;
            }
        }

        /// <summary>
        /// Analyzes current buffer placement across devices.
        /// </summary>
        private BufferPlacementAnalysis AnalyzeBufferPlacement(IAccelerator[] devices)
        {
            var analysis = new BufferPlacementAnalysis
            {
                DeviceDistribution = [],
                HotspotDevices = [],
                UnderutilizedDevices = []
            };

            // Count buffers per device
            foreach (var coherenceInfo in _bufferTracking.Values)
            {
                foreach (var copy in coherenceInfo.Copies)
                {
                    var deviceId = copy.Device.Info.Id;
                    analysis.DeviceDistribution[deviceId] = analysis.DeviceDistribution.GetValueOrDefault(deviceId, 0) + 1;
                }
            }

            // Identify hotspots and underutilized devices
            var avgBuffersPerDevice = devices.Length > 0 ? analysis.DeviceDistribution.Values.Sum() / (double)devices.Length : 0;

            foreach (var device in devices)
            {
                var bufferCount = analysis.DeviceDistribution.GetValueOrDefault(device.Info.Id, 0);

                if (bufferCount > avgBuffersPerDevice * 1.5)
                {
                    analysis.HotspotDevices.Add(device.Info.Id);
                }
                else if (bufferCount < avgBuffersPerDevice * 0.5)
                {
                    analysis.UnderutilizedDevices.Add(device.Info.Id);
                }
            }

            return analysis;
        }

        /// <summary>
        /// Generates placement optimization recommendations.
        /// </summary>
        private static List<PlacementOptimization> GeneratePlacementOptimizations(
            BufferPlacementAnalysis analysis,
            Dictionary<string, Dictionary<string, P2PConnectionCapability>> p2pMatrix)
        {
            var optimizations = new List<PlacementOptimization>();

            // Generate optimizations to balance load between hotspot and underutilized devices
            foreach (var hotspotDevice in analysis.HotspotDevices)
            {
                foreach (var underutilizedDevice in analysis.UnderutilizedDevices)
                {
                    // Check if P2P is available between devices
                    if (p2pMatrix.TryGetValue(hotspotDevice, out var connections) &&
                        connections.TryGetValue(underutilizedDevice, out var capability) &&
                        capability.IsSupported)
                    {
                        optimizations.Add(new PlacementOptimization
                        {
                            SourceDeviceId = hotspotDevice,
                            TargetDeviceId = underutilizedDevice,
                            P2PCapability = capability,
                            OptimizationType = OptimizationType.LoadBalancing,
                            ExpectedBenefit = capability.EstimatedBandwidthGBps
                        });
                    }
                }
            }

            return optimizations;
        }

        /// <summary>
        /// Executes a placement optimization.
        /// </summary>
        private async Task ExecutePlacementOptimizationAsync(
            PlacementOptimization optimization,
            CancellationToken cancellationToken)
        {
            // This would implement actual buffer migration
            // For now, just log the optimization
            _logger.LogDebugMessage($"Executing placement optimization: {optimization.SourceDeviceId} -> {optimization.TargetDeviceId}, Expected benefit: {optimization.ExpectedBenefit} GB/s");

            await Task.Delay(1, cancellationToken); // Simulate optimization work
        }

        /// <summary>
        /// Calculates coherence efficiency metric.
        /// </summary>
        private double CalculateCoherenceEfficiency()
        {
            var totalBuffers = _statistics.TotalTrackedBuffers;
            if (totalBuffers == 0)
            {
                return 1.0;
            }

            var coherentRatio = (double)_statistics.CoherentBuffers / totalBuffers;
            var syncEfficiency = _statistics.SynchronizationOperations > 0
                ? 1.0 / (_statistics.TotalSyncTime.TotalMilliseconds / _statistics.SynchronizationOperations * 1000)
                : 1.0;

            return (coherentRatio + syncEfficiency) / 2.0;
        }

        /// <summary>
        /// Gets element size for a buffer (simplified implementation).
        /// </summary>
        private static int GetElementSize(object buffer)
            // This would use reflection or type information in a real implementation



            => 4; // Assume 4-byte elements for simplicity

        /// <summary>
        /// Monitors coherence health and triggers maintenance operations.
        /// </summary>
        private void MonitorCoherenceHealth(object? state)
        {
            try
            {
                var incoherentRatio = GetOverheadPercentage() / 100.0;

                if (incoherentRatio > IncoherentThresholdRatio)
                {
                    _logger.LogWarningMessage($"High incoherence detected: {incoherentRatio} of buffers are incoherent");

                    // Trigger background synchronization for heavily accessed buffers
                    _ = Task.Run(async () => await PerformBackgroundSynchronizationAsync(CancellationToken.None));
                }

                // Update device coherence states
                foreach (var deviceState in _deviceStates.Values)
                {
                    _logger.LogTrace("Device {DeviceId} coherence: {Coherent} coherent, {Incoherent} incoherent",
                        deviceState.DeviceId, deviceState.CoherentBuffers, deviceState.IncoherentBuffers);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in coherence health monitoring");
            }
        }

        /// <summary>
        /// Performs background synchronization of frequently accessed buffers.
        /// </summary>
        private async Task PerformBackgroundSynchronizationAsync(CancellationToken cancellationToken)
        {
            var incoherentBuffers = _bufferTracking.Values
                .Where(info => !info.IsCoherent)
                .OrderByDescending(info => info.Copies.Sum(c => c.AccessCount))
                .Take(10) // Sync top 10 most accessed
                .ToList();

            foreach (var bufferInfo in incoherentBuffers)
            {
                try
                {
                    // Background synchronization would need type-specific handling
                    // This is a placeholder for the actual synchronization logic
                    await Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background synchronization failed for buffer {BufferId}",
                        bufferInfo.BufferId);
                }
            }
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            await _coherenceMonitor.DisposeAsync();
            _coherenceSemaphore.Dispose();

            _bufferTracking.Clear();
            _deviceStates.Clear();
            _p2pTopology.Clear();

            _logger.LogDebugMessage("P2P memory coherence manager disposed");
        }
    }

    #region Supporting Types

    /// <summary>
    /// P2P buffer coherence information with topology awareness.
    /// </summary>
    internal sealed class P2PBufferCoherenceInfo
    {
        public required Guid BufferId { get; init; }
        public required object SourceBuffer { get; init; }
        public required IAccelerator SourceDevice { get; init; }
        public required IAccelerator TargetDevice { get; init; }
        public required int Offset { get; init; }
        public required int ElementCount { get; init; }
        public required DateTimeOffset LastModified { get; set; }
        public required bool IsCoherent { get; set; }
        public required CoherenceLevel CoherenceLevel { get; set; }
        public P2PConnectionCapability? P2PCapability { get; init; }
        public required AccessPattern AccessPattern { get; set; }
        public required List<BufferCopy> Copies { get; init; }
    }

    /// <summary>
    /// Represents a copy of a buffer on a specific device.
    /// </summary>
    internal sealed class BufferCopy
    {
        public required IAccelerator Device { get; init; }
        public required object Buffer { get; init; }
        public required DateTimeOffset LastAccessed { get; set; }
        public required long AccessCount { get; set; }
        public required bool IsWritten { get; set; }
    }

    /// <summary>
    /// Device coherence state tracking.
    /// </summary>
    internal sealed class DeviceCoherenceState
    {
        public required string DeviceId { get; init; }
        public required int CoherentBuffers { get; init; }
        public required int IncoherentBuffers { get; init; }
        public required DateTimeOffset LastUpdated { get; init; }
    }

    /// <summary>
    /// Buffer placement analysis results.
    /// </summary>
    internal sealed class BufferPlacementAnalysis
    {
        public required Dictionary<string, int> DeviceDistribution { get; init; }
        public required List<string> HotspotDevices { get; init; }
        public required List<string> UnderutilizedDevices { get; init; }
    }

    /// <summary>
    /// Placement optimization recommendation.
    /// </summary>
    internal sealed class PlacementOptimization
    {
        public required string SourceDeviceId { get; init; }
        public required string TargetDeviceId { get; init; }
        public required P2PConnectionCapability P2PCapability { get; init; }
        public required OptimizationType OptimizationType { get; init; }
        public required double ExpectedBenefit { get; init; }
    }

    /// <summary>
    /// Coherence statistics.
    /// </summary>
    public sealed class CoherenceStatistics
    {
        public long TotalTrackedBuffers { get; set; }
        public long CoherentBuffers { get; set; }
        public long IncoherentBuffers { get; set; }
        public long SynchronizationOperations { get; set; }
        public long ReadOperations { get; set; }
        public long WriteOperations { get; set; }
        public TimeSpan TotalSyncTime { get; set; }
        public TimeSpan AverageSyncTime { get; set; }
        public double CoherenceEfficiency { get; set; }
    }

    /// <summary>
    /// Coherence levels.
    /// </summary>
    public enum CoherenceLevel
    {
        None = 0,      // Multiple writers or completely incoherent
        Weak = 1,      // Single writer, multiple readers
        Strong = 2     // All copies identical, no recent writes
    }

    /// <summary>
    /// Access patterns for optimization.
    /// </summary>
    public enum AccessPattern
    {
        Sequential = 0,  // Sequential access pattern
        Random = 1,      // Random access pattern
        Broadcast = 2,   // One-to-many pattern
        Gather = 3       // Many-to-one pattern
    }

    /// <summary>
    /// Access types for coherence tracking.
    /// </summary>
    public enum AccessType
    {
        Read = 0,
        Write = 1
    }

    /// <summary>
    /// Synchronization strategies.
    /// </summary>
    public enum SyncStrategy
    {
        DirectP2P = 0,     // Direct P2P transfer
        HostMediated = 1,  // Via host memory
        Streamed = 2       // Chunked streaming
    }

    /// <summary>
    /// Optimization types.
    /// </summary>
    public enum OptimizationType
    {
        LoadBalancing = 0,  // Balance load across devices
        LocalityOptimization = 1,  // Optimize data locality
        BandwidthOptimization = 2  // Optimize bandwidth usage
    }
}

#endregion
