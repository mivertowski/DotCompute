// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Advanced P2P Optimizer that provides intelligent transfer path selection,
    /// bandwidth optimization, and adaptive scheduling strategies.
    /// </summary>
    public sealed class P2POptimizer : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly P2PCapabilityMatrix _capabilityMatrix;
        private readonly ConcurrentDictionary<string, P2POptimizationProfile> _optimizationProfiles;
        private readonly ConcurrentDictionary<string, P2PTransferHistory> _transferHistory;
        private readonly SemaphoreSlim _optimizerSemaphore;
        private readonly Timer? _adaptiveOptimizationTimer;
        private readonly P2POptimizationStatistics _statistics;
        private bool _disposed;

        // Optimization configuration
        private const int OptimizationHistoryLimit = 1000;
        private const double BandwidthOptimizationThreshold = 0.8; // 80% utilization threshold
        private const int AdaptiveOptimizationIntervalMs = 60000; // 1 minute
        private const double PathEfficiencyThreshold = 0.75; // 75% efficiency threshold

        public P2POptimizer(ILogger logger, P2PCapabilityMatrix capabilityMatrix)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityMatrix = capabilityMatrix ?? throw new ArgumentNullException(nameof(capabilityMatrix));
            _optimizationProfiles = new ConcurrentDictionary<string, P2POptimizationProfile>();
            _transferHistory = new ConcurrentDictionary<string, P2PTransferHistory>();
            _optimizerSemaphore = new SemaphoreSlim(1, 1);
            _statistics = new P2POptimizationStatistics();

            // Start adaptive optimization timer
            _adaptiveOptimizationTimer = new Timer(PerformAdaptiveOptimization, null,
                TimeSpan.FromMilliseconds(AdaptiveOptimizationIntervalMs),
                TimeSpan.FromMilliseconds(AdaptiveOptimizationIntervalMs));

            _logger.LogDebug("P2P Optimizer initialized with adaptive optimization");
        }

        /// <summary>
        /// Initializes topology-aware optimization with device pair analysis.
        /// </summary>
        public async Task InitializeTopologyAsync(
            List<P2PDevicePair> devicePairs,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing P2P topology optimization for {PairCount} device pairs", devicePairs.Count);

            await _optimizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                foreach (var pair in devicePairs)
                {
                    if (pair.IsEnabled && pair.Capability.IsSupported)
                    {
                        var profileKey = GetOptimizationProfileKey(pair.Device1.Info.Id, pair.Device2.Info.Id);


                        var profile = new P2POptimizationProfile
                        {
                            SourceDeviceId = pair.Device1.Info.Id,
                            TargetDeviceId = pair.Device2.Info.Id,
                            P2PCapability = pair.Capability,
                            OptimalChunkSize = CalculateOptimalChunkSize(pair.Capability),
                            OptimalPipelineDepth = CalculateOptimalPipelineDepth(pair.Capability),
                            PreferredStrategy = DeterminePreferredStrategy(pair.Capability),
                            BandwidthUtilization = 0.0,
                            EfficiencyScore = 1.0,
                            LastUpdated = DateTimeOffset.UtcNow
                        };

                        _optimizationProfiles[profileKey] = profile;

                        // Initialize transfer history

                        _transferHistory[profileKey] = new P2PTransferHistory
                        {
                            DevicePairKey = profileKey,
                            TransferRecords = [],
                            TotalTransfers = 0,
                            TotalBytesTransferred = 0,
                            AverageThroughput = 0.0
                        };
                    }
                }

                _logger.LogInformation("P2P topology optimization initialized: {ProfileCount} optimization profiles created",
                    _optimizationProfiles.Count);
            }
            finally
            {
                _ = _optimizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Creates an optimal transfer plan for a P2P operation with comprehensive optimization.
        /// </summary>
        public async Task<P2PTransferPlan> CreateOptimalTransferPlanAsync(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            long transferSize,
            P2PTransferOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sourceDevice);


            ArgumentNullException.ThrowIfNull(targetDevice);


            var profileKey = GetOptimizationProfileKey(sourceDevice.Info.Id, targetDevice.Info.Id);

            // Get P2P capability
            var capability = await _capabilityMatrix.GetP2PCapabilityAsync(sourceDevice, targetDevice, cancellationToken);

            // Get or create optimization profile
            var profile = await GetOrCreateOptimizationProfileAsync(sourceDevice, targetDevice, capability, cancellationToken);

            // Create optimal transfer plan
            var transferPlan = new P2PTransferPlan
            {
                PlanId = Guid.NewGuid().ToString(),
                SourceDevice = sourceDevice,
                TargetDevice = targetDevice,
                TransferSize = transferSize,
                Capability = capability,
                Strategy = SelectOptimalStrategy(profile, transferSize, options),
                ChunkSize = SelectOptimalChunkSize(profile, transferSize, options),
                PipelineDepth = SelectOptimalPipelineDepth(profile, transferSize, options),
                EstimatedTransferTimeMs = EstimateTransferTime(profile, transferSize),
                OptimizationScore = CalculateOptimizationScore(profile, transferSize),
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Apply adaptive optimizations based on history
            await ApplyHistoryBasedOptimizationsAsync(transferPlan, profileKey, cancellationToken);

            // Update statistics
            UpdateOptimizationStatistics(transferPlan);

            _logger.LogDebug("Optimal transfer plan created: {Strategy}, {ChunkSize} bytes chunks, {PipelineDepth} pipeline depth, estimated {EstimatedTime}ms",
                transferPlan.Strategy, transferPlan.ChunkSize, transferPlan.PipelineDepth, transferPlan.EstimatedTransferTimeMs);

            return transferPlan;
        }

        /// <summary>
        /// Creates an optimized scatter plan for multi-target P2P operations.
        /// </summary>
        public async Task<P2PScatterPlan> CreateScatterPlanAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T>[] destinationBuffers,
            P2PScatterOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);


            if (destinationBuffers == null || destinationBuffers.Length == 0)
            {

                throw new ArgumentException("At least one destination buffer is required", nameof(destinationBuffers));
            }


            var scatterPlan = new P2PScatterPlan
            {
                PlanId = Guid.NewGuid().ToString(),
                SourceBuffer = sourceBuffer,
                DestinationBuffers = destinationBuffers,
                Chunks = [],
                EstimatedTotalTimeMs = 0,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Calculate optimal chunking strategy
            var totalElements = sourceBuffer.Length;
            var elementsPerBuffer = totalElements / destinationBuffers.Length;
            var remainingElements = totalElements % destinationBuffers.Length;

            var currentOffset = 0;
            for (var i = 0; i < destinationBuffers.Length; i++)
            {
                var chunkSize = elementsPerBuffer + (i < remainingElements ? 1 : 0);


                var chunk = new P2PTransferChunk
                {
                    ChunkId = i,
                    SourceOffset = currentOffset,
                    DestinationOffset = 0,
                    ElementCount = chunkSize,
                    EstimatedTimeMs = await EstimateChunkTransferTimeAsync(
                        sourceBuffer.Accelerator, destinationBuffers[i].Accelerator,

                        chunkSize * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), cancellationToken)
                };

                scatterPlan.Chunks.Add(chunk);
                scatterPlan.EstimatedTotalTimeMs = Math.Max(scatterPlan.EstimatedTotalTimeMs, chunk.EstimatedTimeMs);
                currentOffset += chunkSize;
            }

            _logger.LogDebug("Scatter plan created: {ChunkCount} chunks, estimated {TotalTime}ms total time",
                scatterPlan.Chunks.Count, scatterPlan.EstimatedTotalTimeMs);

            return scatterPlan;
        }

        /// <summary>
        /// Creates an optimized gather plan for multi-source P2P operations.
        /// </summary>
        public async Task<P2PGatherPlan> CreateGatherPlanAsync<T>(
            IUnifiedMemoryBuffer<T>[] sourceBuffers,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            P2PGatherOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (sourceBuffers == null || sourceBuffers.Length == 0)
            {

                throw new ArgumentException("At least one source buffer is required", nameof(sourceBuffers));
            }


            ArgumentNullException.ThrowIfNull(destinationBuffer);


            var gatherPlan = new P2PGatherPlan
            {
                PlanId = Guid.NewGuid().ToString(),
                SourceBuffers = sourceBuffers,
                DestinationBuffer = destinationBuffer,
                Chunks = [],
                EstimatedTotalTimeMs = 0,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Calculate optimal gather strategy
            var currentDestOffset = 0;
            for (var i = 0; i < sourceBuffers.Length; i++)
            {
                var sourceBuffer = sourceBuffers[i];


                var chunk = new P2PTransferChunk
                {
                    ChunkId = i,
                    SourceOffset = 0,
                    DestinationOffset = currentDestOffset,
                    ElementCount = sourceBuffer.Length,
                    EstimatedTimeMs = await EstimateChunkTransferTimeAsync(
                        sourceBuffer.Accelerator, destinationBuffer.Accelerator,
                        sourceBuffer.SizeInBytes, cancellationToken)
                };

                gatherPlan.Chunks.Add(chunk);
                gatherPlan.EstimatedTotalTimeMs = Math.Max(gatherPlan.EstimatedTotalTimeMs, chunk.EstimatedTimeMs);
                currentDestOffset += sourceBuffer.Length;
            }

            _logger.LogDebug("Gather plan created: {ChunkCount} chunks, estimated {TotalTime}ms total time",
                gatherPlan.Chunks.Count, gatherPlan.EstimatedTotalTimeMs);

            return gatherPlan;
        }

        /// <summary>
        /// Records a completed transfer for optimization learning.
        /// </summary>
        public async Task RecordTransferResultAsync(
            P2PTransferPlan transferPlan,
            double actualTransferTimeMs,
            double actualThroughputGBps,
            bool wasSuccessful,
            CancellationToken cancellationToken = default)
        {
            var profileKey = GetOptimizationProfileKey(transferPlan.SourceDevice.Info.Id, transferPlan.TargetDevice.Info.Id);

            await _optimizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Update optimization profile
                if (_optimizationProfiles.TryGetValue(profileKey, out var profile))
                {
                    var efficiency = Math.Min(1.0, transferPlan.EstimatedTransferTimeMs / actualTransferTimeMs);
                    profile.EfficiencyScore = (profile.EfficiencyScore * 0.9) + (efficiency * 0.1); // Moving average
                    profile.LastUpdated = DateTimeOffset.UtcNow;

                    // Adapt strategy if performance is poor
                    if (efficiency < PathEfficiencyThreshold && wasSuccessful)
                    {
                        await AdaptOptimizationProfileAsync(profile, transferPlan, actualThroughputGBps, cancellationToken);
                    }
                }

                // Update transfer history
                if (_transferHistory.TryGetValue(profileKey, out var history))
                {
                    var record = new P2PTransferRecord
                    {
                        TransferSize = transferPlan.TransferSize,
                        Strategy = transferPlan.Strategy,
                        ChunkSize = transferPlan.ChunkSize,
                        EstimatedTimeMs = transferPlan.EstimatedTransferTimeMs,
                        ActualTimeMs = actualTransferTimeMs,
                        ThroughputGBps = actualThroughputGBps,
                        WasSuccessful = wasSuccessful,
                        Timestamp = DateTimeOffset.UtcNow
                    };

                    history.TransferRecords.Add(record);
                    history.TotalTransfers++;
                    history.TotalBytesTransferred += transferPlan.TransferSize;

                    // Maintain history limit
                    if (history.TransferRecords.Count > OptimizationHistoryLimit)
                    {
                        history.TransferRecords.RemoveAt(0);
                    }

                    // Update running statistics
                    history.AverageThroughput = history.TransferRecords
                        .Where(r => r.WasSuccessful)
                        .Average(r => r.ThroughputGBps);
                }

                _logger.LogTrace("Transfer result recorded: {TransferSize} bytes, {ActualTime:F1}ms, {Throughput:F2} GB/s, efficiency {Efficiency:P1}",
                    transferPlan.TransferSize, actualTransferTimeMs, actualThroughputGBps,

                    Math.Min(1.0, transferPlan.EstimatedTransferTimeMs / actualTransferTimeMs));
            }
            finally
            {
                _ = _optimizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets optimization recommendations for improving P2P performance.
        /// </summary>
        public async Task<P2POptimizationRecommendations> GetOptimizationRecommendationsAsync(
            CancellationToken cancellationToken = default)
        {
            await _optimizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                var recommendations = new P2POptimizationRecommendations
                {
                    GeneratedAt = DateTimeOffset.UtcNow,
                    PerformanceRecommendations = [],
                    TopologyRecommendations = [],
                    ConfigurationRecommendations = []
                };

                // Analyze profiles for performance recommendations
                foreach (var profile in _optimizationProfiles.Values)
                {
                    if (profile.EfficiencyScore < PathEfficiencyThreshold)
                    {
                        recommendations.PerformanceRecommendations.Add(new P2PPerformanceRecommendation
                        {
                            DevicePair = $"{profile.SourceDeviceId} -> {profile.TargetDeviceId}",
                            CurrentEfficiency = profile.EfficiencyScore,
                            RecommendedStrategy = SuggestAlternativeStrategy(profile),
                            ExpectedImprovement = EstimateImprovementPotential(profile),
                            Priority = profile.EfficiencyScore < 0.5 ? "High" : "Medium"
                        });
                    }
                }

                // Analyze transfer history for topology recommendations
                var underutilizedPairs = _transferHistory.Values
                    .Where(h => h.TotalTransfers < 10 && h.AverageThroughput > 0)
                    .OrderByDescending(h => h.AverageThroughput)
                    .Take(5);

                foreach (var pair in underutilizedPairs)
                {
                    recommendations.TopologyRecommendations.Add(new P2PTopologyRecommendation
                    {
                        RecommendationType = "UnderutilizedHighPerformancePath",
                        DevicePair = pair.DevicePairKey,
                        Description = $"High-performance path with {pair.AverageThroughput:F1} GB/s average throughput is underutilized",
                        Impact = "Medium"
                    });
                }

                // Configuration recommendations
                var globalEfficiency = _optimizationProfiles.Values.Average(p => p.EfficiencyScore);
                if (globalEfficiency < 0.8)
                {
                    recommendations.ConfigurationRecommendations.Add(new P2PConfigurationRecommendation
                    {
                        Category = "GlobalOptimization",
                        Setting = "EnableAdaptiveChunking",
                        RecommendedValue = "true",
                        Reason = "Low global efficiency suggests adaptive chunking could improve performance",
                        ExpectedBenefit = "10-20% throughput improvement"
                    });
                }

                _logger.LogDebug("Optimization recommendations generated: {PerfCount} performance, {TopoCount} topology, {ConfigCount} configuration",
                    recommendations.PerformanceRecommendations.Count, recommendations.TopologyRecommendations.Count,
                    recommendations.ConfigurationRecommendations.Count);

                return recommendations;
            }
            finally
            {
                _ = _optimizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets comprehensive optimization statistics.
        /// </summary>
        public P2POptimizationStatistics GetOptimizationStatistics()
        {
            lock (_statistics)
            {
                var stats = new P2POptimizationStatistics
                {
                    TotalOptimizedTransfers = _statistics.TotalOptimizedTransfers,
                    OptimizationProfilesActive = _optimizationProfiles.Count,
                    AverageOptimizationScore = _optimizationProfiles.Values.Count != 0

                        ? _optimizationProfiles.Values.Average(p => p.OptimizationScore) : 0.0,
                    AverageEfficiencyScore = _optimizationProfiles.Values.Count != 0
                        ? _optimizationProfiles.Values.Average(p => p.EfficiencyScore) : 0.0,
                    TotalTransferHistory = _transferHistory.Values.Sum(h => h.TotalTransfers),
                    AdaptiveOptimizationsApplied = _statistics.AdaptiveOptimizationsApplied,
                    LastOptimizationTime = _statistics.LastOptimizationTime
                };

                return stats;
            }
        }

        #region Private Implementation

        private async Task<P2POptimizationProfile> GetOrCreateOptimizationProfileAsync(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            P2PConnectionCapability capability,
            CancellationToken cancellationToken)
        {
            var profileKey = GetOptimizationProfileKey(sourceDevice.Info.Id, targetDevice.Info.Id);

            if (_optimizationProfiles.TryGetValue(profileKey, out var existingProfile))
            {
                return existingProfile;
            }

            var profile = new P2POptimizationProfile
            {
                SourceDeviceId = sourceDevice.Info.Id,
                TargetDeviceId = targetDevice.Info.Id,
                P2PCapability = capability,
                OptimalChunkSize = CalculateOptimalChunkSize(capability),
                OptimalPipelineDepth = CalculateOptimalPipelineDepth(capability),
                PreferredStrategy = DeterminePreferredStrategy(capability),
                BandwidthUtilization = 0.0,
                EfficiencyScore = 1.0,
                OptimizationScore = CalculateInitialOptimizationScore(capability),
                LastUpdated = DateTimeOffset.UtcNow
            };

            _optimizationProfiles[profileKey] = profile;

            // Initialize transfer history

            _transferHistory[profileKey] = new P2PTransferHistory
            {
                DevicePairKey = profileKey,
                TransferRecords = [],
                TotalTransfers = 0,
                TotalBytesTransferred = 0,
                AverageThroughput = capability.EstimatedBandwidthGBps
            };

            await Task.CompletedTask;
            return profile;
        }

        private static P2PTransferStrategy SelectOptimalStrategy(
            P2POptimizationProfile profile,
            long transferSize,
            P2PTransferOptions options)
        {
            // Start with profile's preferred strategy
            var strategy = profile.PreferredStrategy;

            // Override based on transfer size and options
            if (transferSize < 1024 * 1024) // < 1MB - direct transfer
            {
                strategy = P2PTransferStrategy.DirectP2P;
            }
            else if (transferSize > 100 * 1024 * 1024) // > 100MB - consider pipelined
            {
                if (profile.P2PCapability.EstimatedBandwidthGBps > 10.0 && options.PipelineDepth > 1)
                {
                    strategy = P2PTransferStrategy.PipelinedP2P;
                }
                else
                {
                    strategy = P2PTransferStrategy.ChunkedP2P;
                }
            }

            // Fallback to host-mediated if P2P not supported
            if (!profile.P2PCapability.IsSupported)
            {
                strategy = P2PTransferStrategy.HostMediated;
            }

            return strategy;
        }

        private static int SelectOptimalChunkSize(
            P2POptimizationProfile profile,
            long transferSize,
            P2PTransferOptions options)
        {
            var baseChunkSize = profile.OptimalChunkSize;

            // Adjust based on transfer size
            if (transferSize < baseChunkSize)
            {
                return (int)transferSize;
            }

            // Adjust based on bandwidth
            if (profile.P2PCapability.EstimatedBandwidthGBps > 50.0) // High bandwidth
            {
                baseChunkSize *= 2; // Larger chunks for high bandwidth
            }
            else if (profile.P2PCapability.EstimatedBandwidthGBps < 10.0) // Low bandwidth
            {
                baseChunkSize /= 2; // Smaller chunks for low bandwidth
            }

            return Math.Min(baseChunkSize, options.PreferredChunkSize);
        }

        private static int SelectOptimalPipelineDepth(
            P2POptimizationProfile profile,
            long transferSize,
            P2PTransferOptions options)
        {
            var basePipelineDepth = profile.OptimalPipelineDepth;

            // Adjust based on transfer size and bandwidth
            if (transferSize > 100 * 1024 * 1024 && profile.P2PCapability.EstimatedBandwidthGBps > 20.0)
            {
                basePipelineDepth = Math.Max(basePipelineDepth, 4);
            }
            else if (transferSize < 10 * 1024 * 1024)
            {
                basePipelineDepth = 1; // No pipelining for small transfers
            }

            return Math.Min(basePipelineDepth, options.PipelineDepth);
        }

        private static double EstimateTransferTime(P2POptimizationProfile profile, long transferSize)
        {
            var bandwidthGBps = profile.P2PCapability.EstimatedBandwidthGBps * profile.EfficiencyScore;
            var transferSizeGB = transferSize / (1024.0 * 1024.0 * 1024.0);


            var baseTransferTime = (transferSizeGB / bandwidthGBps) * 1000; // ms

            // Add latency overhead
            var latencyOverhead = profile.P2PCapability.ConnectionType switch
            {
                P2PConnectionType.NVLink => 0.5,
                P2PConnectionType.PCIe => 2.0,
                P2PConnectionType.InfiniBand => 0.1,
                _ => 5.0
            };

            return baseTransferTime + latencyOverhead;
        }

        private static double CalculateOptimizationScore(P2POptimizationProfile profile, long transferSize)
        {
            var bandwidthScore = Math.Min(1.0, profile.P2PCapability.EstimatedBandwidthGBps / 100.0);
            var efficiencyScore = profile.EfficiencyScore;
            var connectionScore = profile.P2PCapability.ConnectionType switch
            {
                P2PConnectionType.NVLink => 1.0,
                P2PConnectionType.PCIe => 0.8,
                P2PConnectionType.InfiniBand => 0.9,
                _ => 0.5
            };

            return (bandwidthScore + efficiencyScore + connectionScore) / 3.0;
        }

        private async Task ApplyHistoryBasedOptimizationsAsync(
            P2PTransferPlan transferPlan,
            string profileKey,
            CancellationToken cancellationToken)
        {
            if (_transferHistory.TryGetValue(profileKey, out var history) && history.TransferRecords.Count != 0)
            {
                // Find similar past transfers
                var similarTransfers = history.TransferRecords
                    .Where(r => Math.Abs(r.TransferSize - transferPlan.TransferSize) / (double)transferPlan.TransferSize < 0.2)
                    .Where(r => r.WasSuccessful)
                    .ToList();

                if (similarTransfers.Count != 0)
                {
                    // Get best performing configuration
                    var bestTransfer = similarTransfers.OrderByDescending(r => r.ThroughputGBps).First();


                    if (bestTransfer.Strategy != transferPlan.Strategy && bestTransfer.ThroughputGBps > 0)
                    {
                        transferPlan.Strategy = bestTransfer.Strategy;
                        transferPlan.ChunkSize = bestTransfer.ChunkSize;


                        _logger.LogTrace("Applied history-based optimization: strategy changed to {Strategy} based on past performance",
                            bestTransfer.Strategy);
                    }
                }
            }

            await Task.CompletedTask;
        }

        private async Task AdaptOptimizationProfileAsync(
            P2POptimizationProfile profile,
            P2PTransferPlan transferPlan,
            double actualThroughputGBps,
            CancellationToken cancellationToken)
        {
            // Adapt chunk size based on performance
            if (actualThroughputGBps < profile.P2PCapability.EstimatedBandwidthGBps * 0.5)
            {
                // Poor performance - try different chunk size
                if (profile.OptimalChunkSize > 1024 * 1024) // > 1MB
                {
                    profile.OptimalChunkSize /= 2;
                }
                else
                {
                    profile.OptimalChunkSize *= 2;
                }
            }

            // Adapt strategy preference
            if (profile.EfficiencyScore < 0.6)
            {
                profile.PreferredStrategy = profile.PreferredStrategy switch
                {
                    P2PTransferStrategy.DirectP2P => P2PTransferStrategy.ChunkedP2P,
                    P2PTransferStrategy.ChunkedP2P => P2PTransferStrategy.PipelinedP2P,
                    P2PTransferStrategy.PipelinedP2P => P2PTransferStrategy.HostMediated,
                    _ => P2PTransferStrategy.DirectP2P
                };

                lock (_statistics)
                {
                    _statistics.AdaptiveOptimizationsApplied++;
                }
            }

            await Task.CompletedTask;
        }

        private async Task<double> EstimateChunkTransferTimeAsync(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            long transferSize,
            CancellationToken cancellationToken)
        {
            var capability = await _capabilityMatrix.GetP2PCapabilityAsync(sourceDevice, targetDevice, cancellationToken);


            var transferSizeGB = transferSize / (1024.0 * 1024.0 * 1024.0);
            var baseTime = (transferSizeGB / capability.EstimatedBandwidthGBps) * 1000; // ms


            var latency = capability.ConnectionType switch
            {
                P2PConnectionType.NVLink => 0.5,
                P2PConnectionType.PCIe => 2.0,
                P2PConnectionType.InfiniBand => 0.1,
                _ => 5.0
            };

            return baseTime + latency;
        }

        private static int CalculateOptimalChunkSize(P2PConnectionCapability capability)
        {
            // Base chunk size on connection type and bandwidth
            var baseSize = capability.ConnectionType switch
            {
                P2PConnectionType.NVLink => 16 * 1024 * 1024, // 16MB for NVLink
                P2PConnectionType.PCIe => 8 * 1024 * 1024,    // 8MB for PCIe
                P2PConnectionType.InfiniBand => 32 * 1024 * 1024, // 32MB for InfiniBand
                _ => 4 * 1024 * 1024 // 4MB default
            };

            // Adjust for bandwidth
            if (capability.EstimatedBandwidthGBps > 50.0)
            {
                baseSize *= 2;
            }
            else if (capability.EstimatedBandwidthGBps < 10.0)
            {
                baseSize /= 2;
            }


            return baseSize;
        }

        private static int CalculateOptimalPipelineDepth(P2PConnectionCapability capability)
        {
            return capability.EstimatedBandwidthGBps switch
            {
                > 50.0 => 4,  // High bandwidth - deeper pipeline
                > 20.0 => 3,  // Medium-high bandwidth
                > 10.0 => 2,  // Medium bandwidth
                _ => 1        // Low bandwidth - no pipelining
            };
        }

        private static P2PTransferStrategy DeterminePreferredStrategy(P2PConnectionCapability capability)
        {
            if (!capability.IsSupported)
            {

                return P2PTransferStrategy.HostMediated;
            }


            return capability.EstimatedBandwidthGBps switch
            {
                > 30.0 => P2PTransferStrategy.PipelinedP2P, // High bandwidth - use pipelining
                > 10.0 => P2PTransferStrategy.ChunkedP2P,   // Medium bandwidth - use chunking
                _ => P2PTransferStrategy.DirectP2P          // Low bandwidth - direct transfer
            };
        }

        private static double CalculateInitialOptimizationScore(P2PConnectionCapability capability)
        {
            if (!capability.IsSupported)
            {

                return 0.0;
            }


            var bandwidthScore = Math.Min(1.0, capability.EstimatedBandwidthGBps / 100.0);
            var connectionScore = capability.ConnectionType switch
            {
                P2PConnectionType.NVLink => 1.0,
                P2PConnectionType.PCIe => 0.8,
                P2PConnectionType.InfiniBand => 0.9,
                _ => 0.5
            };

            return (bandwidthScore + connectionScore) / 2.0;
        }

        private static P2PTransferStrategy SuggestAlternativeStrategy(P2POptimizationProfile profile)
        {
            return profile.PreferredStrategy switch
            {
                P2PTransferStrategy.DirectP2P => P2PTransferStrategy.ChunkedP2P,
                P2PTransferStrategy.ChunkedP2P => P2PTransferStrategy.PipelinedP2P,
                P2PTransferStrategy.PipelinedP2P => P2PTransferStrategy.DirectP2P,
                P2PTransferStrategy.HostMediated => P2PTransferStrategy.DirectP2P,
                _ => P2PTransferStrategy.ChunkedP2P
            };
        }

        private static double EstimateImprovementPotential(P2POptimizationProfile profile) => (1.0 - profile.EfficiencyScore) * 0.5; // Up to 50% improvement potential

        private void UpdateOptimizationStatistics(P2PTransferPlan transferPlan)
        {
            lock (_statistics)
            {
                _statistics.TotalOptimizedTransfers++;
                _statistics.LastOptimizationTime = DateTimeOffset.UtcNow;
            }
        }

        private void PerformAdaptiveOptimization(object? state)
        {
            try
            {
                var profilesNeedingOptimization = _optimizationProfiles.Values
                    .Where(p => p.EfficiencyScore < PathEfficiencyThreshold)
                    .Where(p => DateTimeOffset.UtcNow - p.LastUpdated < TimeSpan.FromHours(1))
                    .ToList();

                foreach (var profile in profilesNeedingOptimization)
                {
                    // Adaptive optimization logic
                    if (profile.BandwidthUtilization > BandwidthOptimizationThreshold)
                    {
                        // High utilization - increase chunk size
                        profile.OptimalChunkSize = (int)(profile.OptimalChunkSize * 1.2);
                    }
                    else if (profile.BandwidthUtilization < 0.5)
                    {
                        // Low utilization - decrease chunk size
                        profile.OptimalChunkSize = (int)(profile.OptimalChunkSize * 0.8);
                    }
                }

                if (profilesNeedingOptimization.Count != 0)
                {
                    _logger.LogTrace("Adaptive optimization applied to {ProfileCount} optimization profiles",
                        profilesNeedingOptimization.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during adaptive optimization");
            }
        }

        private static string GetOptimizationProfileKey(string sourceDeviceId, string targetDeviceId) => $"{sourceDeviceId}_{targetDeviceId}";

        #endregion

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }


            _disposed = true;

            _adaptiveOptimizationTimer?.Dispose();
            _optimizerSemaphore.Dispose();

            _optimizationProfiles.Clear();
            _transferHistory.Clear();

            _logger.LogDebug("P2P Optimizer disposed");
            await Task.CompletedTask;
        }
    }

    #region Supporting Types

    /// <summary>
    /// P2P optimization profile for device pairs.
    /// </summary>
    internal sealed class P2POptimizationProfile
    {
        public required string SourceDeviceId { get; init; }
        public required string TargetDeviceId { get; init; }
        public required P2PConnectionCapability P2PCapability { get; init; }
        public int OptimalChunkSize { get; set; }
        public int OptimalPipelineDepth { get; set; }
        public P2PTransferStrategy PreferredStrategy { get; set; }
        public double BandwidthUtilization { get; set; }
        public double EfficiencyScore { get; set; }
        public double OptimizationScore { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }

    /// <summary>
    /// Transfer history for optimization learning.
    /// </summary>
    internal sealed class P2PTransferHistory
    {
        public required string DevicePairKey { get; init; }
        public required List<P2PTransferRecord> TransferRecords { get; init; }
        public long TotalTransfers { get; set; }
        public long TotalBytesTransferred { get; set; }
        public double AverageThroughput { get; set; }
    }

    /// <summary>
    /// Individual transfer record for history tracking.
    /// </summary>
    internal sealed class P2PTransferRecord
    {
        public required long TransferSize { get; init; }
        public required P2PTransferStrategy Strategy { get; init; }
        public required int ChunkSize { get; init; }
        public required double EstimatedTimeMs { get; init; }
        public required double ActualTimeMs { get; init; }
        public required double ThroughputGBps { get; init; }
        public required bool WasSuccessful { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }

    /// <summary>
    /// Comprehensive P2P transfer plan.
    /// </summary>
    public sealed class P2PTransferPlan
    {
        public required string PlanId { get; init; }
        public required IAccelerator SourceDevice { get; init; }
        public required IAccelerator TargetDevice { get; init; }
        public required long TransferSize { get; init; }
        public required P2PConnectionCapability Capability { get; init; }
        public P2PTransferStrategy Strategy { get; set; }
        public int ChunkSize { get; set; }
        public int PipelineDepth { get; set; }
        public required double EstimatedTransferTimeMs { get; set; }
        public required double OptimizationScore { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>
    /// P2P scatter plan for multi-target operations.
    /// </summary>
    public sealed class P2PScatterPlan
    {
        public required string PlanId { get; init; }
        public required object SourceBuffer { get; init; }
        public required object[] DestinationBuffers { get; init; }
        public required List<P2PTransferChunk> Chunks { get; init; }
        public double EstimatedTotalTimeMs { get; set; }
        public required DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>
    /// P2P gather plan for multi-source operations.
    /// </summary>
    public sealed class P2PGatherPlan
    {
        public required string PlanId { get; init; }
        public required object[] SourceBuffers { get; init; }
        public required object DestinationBuffer { get; init; }
        public required List<P2PTransferChunk> Chunks { get; init; }
        public double EstimatedTotalTimeMs { get; set; }
        public required DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>
    /// P2P transfer chunk information.
    /// </summary>
    public sealed class P2PTransferChunk
    {
        public required int ChunkId { get; init; }
        public required int SourceOffset { get; init; }
        public required int DestinationOffset { get; init; }
        public required int ElementCount { get; init; }
        public required double EstimatedTimeMs { get; set; }
    }

    /// <summary>
    /// P2P optimization recommendations.
    /// </summary>
    public sealed class P2POptimizationRecommendations
    {
        public required DateTimeOffset GeneratedAt { get; init; }
        public required List<P2PPerformanceRecommendation> PerformanceRecommendations { get; init; }
        public required List<P2PTopologyRecommendation> TopologyRecommendations { get; init; }
        public required List<P2PConfigurationRecommendation> ConfigurationRecommendations { get; init; }
    }

    /// <summary>
    /// Performance recommendation for specific device pairs.
    /// </summary>
    public sealed class P2PPerformanceRecommendation
    {
        public required string DevicePair { get; init; }
        public required double CurrentEfficiency { get; init; }
        public required P2PTransferStrategy RecommendedStrategy { get; init; }
        public required double ExpectedImprovement { get; init; }
        public required string Priority { get; init; }
    }

    /// <summary>
    /// Topology recommendation for overall system optimization.
    /// </summary>
    public sealed class P2PTopologyRecommendation
    {
        public required string RecommendationType { get; init; }
        public required string DevicePair { get; init; }
        public required string Description { get; init; }
        public required string Impact { get; init; }
    }

    /// <summary>
    /// Configuration recommendation for system settings.
    /// </summary>
    public sealed class P2PConfigurationRecommendation
    {
        public required string Category { get; init; }
        public required string Setting { get; init; }
        public required string RecommendedValue { get; init; }
        public required string Reason { get; init; }
        public required string ExpectedBenefit { get; init; }
    }

    /// <summary>
    /// P2P optimization statistics.
    /// </summary>
    public sealed class P2POptimizationStatistics
    {
        public long TotalOptimizedTransfers { get; set; }
        public int OptimizationProfilesActive { get; set; }
        public double AverageOptimizationScore { get; set; }
        public double AverageEfficiencyScore { get; set; }
        public long TotalTransferHistory { get; set; }
        public long AdaptiveOptimizationsApplied { get; set; }
        public DateTimeOffset LastOptimizationTime { get; set; }
    }

    #endregion
}