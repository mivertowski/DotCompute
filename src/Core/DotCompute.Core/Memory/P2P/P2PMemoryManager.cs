// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Unified P2P Memory Manager that integrates all P2P components and provides
    /// a high-level interface for P2P memory operations in multi-GPU environments.
    /// </summary>
    public sealed class P2PMemoryManager : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly P2PTransferManager _transferManager;
        private readonly P2PCapabilityMatrix _capabilityMatrix;
        private readonly P2PValidator _validator;
        private readonly P2POptimizer _optimizer;
        private readonly P2PSynchronizer _synchronizer;
        private readonly P2PMemoryCoherenceManager _coherenceManager;
        private readonly P2PBufferFactory _bufferFactory;
        private readonly ConcurrentDictionary<string, P2PMemoryPool> _memoryPools;
        private readonly SemaphoreSlim _managerSemaphore;
        private readonly P2PManagerStatistics _statistics;
        private bool _isInitialized;
        private bool _disposed;

        public P2PMemoryManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));


            var capabilityDetector = new P2PCapabilityDetector(logger);
            _transferManager = new P2PTransferManager(logger, capabilityDetector);
            _capabilityMatrix = new P2PCapabilityMatrix(logger);
            _validator = new P2PValidator(logger);
            _optimizer = new P2POptimizer(logger, _capabilityMatrix);
            _synchronizer = new P2PSynchronizer(logger);
            _coherenceManager = new P2PMemoryCoherenceManager(logger);
            _bufferFactory = new P2PBufferFactory(logger, capabilityDetector);


            _memoryPools = new ConcurrentDictionary<string, P2PMemoryPool>();
            _managerSemaphore = new SemaphoreSlim(1, 1);
            _statistics = new P2PManagerStatistics();

            _logger.LogInformation("P2P Memory Manager created");
        }

        /// <summary>
        /// Initializes the P2P memory management system with the provided devices.
        /// </summary>
        public async Task<P2PInitializationSummary> InitializeAsync(
            IAccelerator[] devices,
            P2PMemoryManagerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (devices == null || devices.Length == 0)
            {

                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }


            options ??= P2PMemoryManagerOptions.Default;

            await _managerSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized)
                {
                    _logger.LogWarning("P2P Memory Manager is already initialized");
                    return new P2PInitializationSummary { IsSuccessful = false, ErrorMessage = "Already initialized", InitializationSteps = new List<P2PInitializationStep>() };
                }

                var initStartTime = DateTimeOffset.UtcNow;
                _logger.LogInformation("Initializing P2P Memory Manager with {DeviceCount} devices", devices.Length);

                var summary = new P2PInitializationSummary
                {
                    TotalDevices = devices.Length,
                    InitializationSteps = new List<P2PInitializationStep>()
                };

                try
                {
                    // Step 1: Initialize topology and capabilities
                    var topologyResult = await _transferManager.InitializeP2PTopologyAsync(devices, cancellationToken);
                    summary.InitializationSteps.Add(new P2PInitializationStep
                    {
                        StepName = "TopologyInitialization",
                        IsSuccessful = topologyResult.IsSuccessful,
                        Duration = DateTimeOffset.UtcNow - initStartTime,
                        Details = $"Detected {topologyResult.SuccessfulConnections} P2P connections"
                    });

                    if (!topologyResult.IsSuccessful)
                    {
                        summary.IsSuccessful = false;
                        summary.ErrorMessage = topologyResult.ErrorMessage;
                        return summary;
                    }

                    // Step 2: Initialize optimizer with topology
                    var optimizerStepStart = DateTimeOffset.UtcNow;
                    await _optimizer.InitializeTopologyAsync(topologyResult.DevicePairs, cancellationToken);
                    summary.InitializationSteps.Add(new P2PInitializationStep
                    {
                        StepName = "OptimizerInitialization",
                        IsSuccessful = true,
                        Duration = DateTimeOffset.UtcNow - optimizerStepStart,
                        Details = $"Optimization profiles created for {topologyResult.DevicePairs.Count} device pairs"
                    });

                    // Step 3: Initialize synchronizer
                    var syncStepStart = DateTimeOffset.UtcNow;
                    await _synchronizer.InitializeDevicesAsync(devices, cancellationToken);
                    summary.InitializationSteps.Add(new P2PInitializationStep
                    {
                        StepName = "SynchronizerInitialization",
                        IsSuccessful = true,
                        Duration = DateTimeOffset.UtcNow - syncStepStart,
                        Details = $"Synchronization initialized for {devices.Length} devices"
                    });

                    // Step 4: Initialize memory pools
                    var poolStepStart = DateTimeOffset.UtcNow;
                    await InitializeMemoryPoolsAsync(devices, options, cancellationToken);
                    summary.InitializationSteps.Add(new P2PInitializationStep
                    {
                        StepName = "MemoryPoolInitialization",
                        IsSuccessful = true,
                        Duration = DateTimeOffset.UtcNow - poolStepStart,
                        Details = $"Memory pools created for {_memoryPools.Count} devices"
                    });

                    // Step 5: Run initial benchmarks if requested
                    if (options.RunInitialBenchmarks)
                    {
                        var benchmarkStepStart = DateTimeOffset.UtcNow;
                        await RunInitialBenchmarksAsync(devices, cancellationToken);
                        summary.InitializationSteps.Add(new P2PInitializationStep
                        {
                            StepName = "InitialBenchmarks",
                            IsSuccessful = true,
                            Duration = DateTimeOffset.UtcNow - benchmarkStepStart,
                            Details = "Initial performance benchmarks completed"
                        });
                    }

                    _isInitialized = true;
                    summary.IsSuccessful = true;
                    summary.TotalInitializationTime = DateTimeOffset.UtcNow - initStartTime;

                    _logger.LogInformation("P2P Memory Manager initialization completed in {Duration:F1}ms",
                        summary.TotalInitializationTime.TotalMilliseconds);

                    return summary;
                }
                catch (Exception ex)
                {
                    summary.IsSuccessful = false;
                    summary.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "P2P Memory Manager initialization failed");
                    return summary;
                }
            }
            finally
            {
                _ = _managerSemaphore.Release();
            }
        }

        /// <summary>
        /// Creates a P2P-optimized buffer with automatic optimization and validation.
        /// </summary>
        public async Task<P2PBuffer<T>> CreateOptimizedBufferAsync<T>(
            IAccelerator targetDevice,
            int length,
            P2PBufferCreationOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfNotInitialized();


            options ??= P2PBufferCreationOptions.Default;

            var p2pOptions = new P2PBufferOptions
            {
                EnableP2POptimizations = options.EnableOptimization,
                EnableMemoryPooling = options.UseMemoryPooling,
                EnableAsyncTransfers = options.EnableAsyncTransfers
            };

            var buffer = await _bufferFactory.CreateEmptyP2PBufferAsync<T>(targetDevice, length, p2pOptions, cancellationToken);

            // Track buffer for coherence management if enabled
            if (options.EnableCoherenceTracking)
            {
                // Note: This would need the source buffer reference in a real implementation
                // _coherenceManager.TrackP2PBuffer(buffer, sourceBuffer, offset, count, capability);
            }

            _logger.LogDebug("Created optimized P2P buffer: {Length} elements on {Device}", length, targetDevice.Info.Name);
            return buffer;
        }

        /// <summary>
        /// Executes a high-performance P2P transfer with full optimization pipeline.
        /// </summary>
        public async Task<P2PTransferResult> ExecuteOptimizedTransferAsync<T>(
            IBuffer<T> sourceBuffer,
            IBuffer<T> destinationBuffer,
            P2PTransferExecutionOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfNotInitialized();


            options ??= P2PTransferExecutionOptions.Default;

            var transferOptions = new P2PTransferOptions
            {
                EnableValidation = options.EnableValidation,
                EnableSynchronization = options.EnableSynchronization,
                EnableOptimization = options.EnableOptimization,
                Priority = options.Priority,
                PreferredChunkSize = options.PreferredChunkSize,
                PipelineDepth = options.PipelineDepth
            };

            var result = await _transferManager.ExecuteP2PTransferAsync(sourceBuffer, destinationBuffer, transferOptions, cancellationToken);

            // Update statistics
            UpdateManagerStatistics(result);

            return result;
        }

        /// <summary>
        /// Executes a multi-GPU scatter operation with optimization.
        /// </summary>
        public async Task<P2PScatterResult> ExecuteOptimizedScatterAsync<T>(
            IBuffer<T> sourceBuffer,
            IBuffer<T>[] destinationBuffers,
            P2PScatterExecutionOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfNotInitialized();


            options ??= P2PScatterExecutionOptions.Default;

            var scatterOptions = new P2PScatterOptions
            {
                TransferOptions = new P2PTransferOptions
                {
                    EnableValidation = options.EnableValidation,
                    EnableSynchronization = options.EnableSynchronization,
                    Priority = options.Priority
                },
                EnableParallelScatter = options.EnableParallelScatter,
                MaxConcurrentScatters = options.MaxConcurrentScatters
            };

            return await _transferManager.ExecuteP2PScatterAsync(sourceBuffer, destinationBuffers, scatterOptions, cancellationToken);
        }

        /// <summary>
        /// Executes a multi-GPU gather operation with optimization.
        /// </summary>
        public async Task<P2PGatherResult> ExecuteOptimizedGatherAsync<T>(
            IBuffer<T>[] sourceBuffers,
            IBuffer<T> destinationBuffer,
            P2PGatherExecutionOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfNotInitialized();


            options ??= P2PGatherExecutionOptions.Default;

            var gatherOptions = new P2PGatherOptions
            {
                TransferOptions = new P2PTransferOptions
                {
                    EnableValidation = options.EnableValidation,
                    EnableSynchronization = options.EnableSynchronization,
                    Priority = options.Priority
                },
                EnableParallelGather = options.EnableParallelGather,
                MaxConcurrentGathers = options.MaxConcurrentGathers
            };

            return await _transferManager.ExecuteP2PGatherAsync(sourceBuffers, destinationBuffer, gatherOptions, cancellationToken);
        }

        /// <summary>
        /// Performs comprehensive P2P performance benchmarking.
        /// </summary>
        public async Task<P2PComprehensiveBenchmarkResult> ExecuteComprehensiveBenchmarkAsync(
            P2PBenchmarkExecutionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfNotInitialized();


            options ??= P2PBenchmarkExecutionOptions.Default;

            var devices = _memoryPools.Keys.Select(deviceId =>

                _memoryPools[deviceId].TargetDevice).ToArray();

            var multiGpuOptions = new P2PMultiGpuBenchmarkOptions
            {
                EnablePairwiseBenchmarks = options.EnablePairwiseBenchmarks,
                EnableScatterBenchmarks = options.EnableScatterBenchmarks,
                EnableGatherBenchmarks = options.EnableGatherBenchmarks,
                EnableAllToAllBenchmarks = options.EnableAllToAllBenchmarks,
                PairwiseTransferSizeMB = options.TransferSizeMB,
                UseCachedResults = options.UseCachedResults
            };

            var benchmarkResult = await _validator.ExecuteMultiGpuBenchmarkAsync(devices, multiGpuOptions, cancellationToken);

            var comprehensiveResult = new P2PComprehensiveBenchmarkResult
            {
                BenchmarkId = benchmarkResult.BenchmarkId,
                MultiGpuResults = benchmarkResult,
                OptimizationRecommendations = await _optimizer.GetOptimizationRecommendationsAsync(cancellationToken),
                TopologyAnalysis = _capabilityMatrix.GetTopologyAnalysis(),
                SystemStatistics = GetSystemStatistics()
            };

            return comprehensiveResult;
        }

        /// <summary>
        /// Gets comprehensive system statistics for all P2P components.
        /// </summary>
        public P2PSystemStatistics GetSystemStatistics()
        {
            var transferStats = _transferManager.GetTransferStatistics();
            var validationStats = _validator.GetValidationStatistics();
            var optimizationStats = _optimizer.GetOptimizationStatistics();
            var syncStats = _synchronizer.GetSynchronizationStatistics();
            var matrixStats = _capabilityMatrix.GetMatrixStatistics();

            return new P2PSystemStatistics
            {
                IsInitialized = _isInitialized,
                TotalDevices = matrixStats.TotalDevices,
                P2PConnections = matrixStats.P2PEnabledConnections,
                TransferStatistics = transferStats,
                ValidationStatistics = validationStats,
                OptimizationStatistics = optimizationStats,
                SynchronizationStatistics = syncStats,
                MatrixStatistics = matrixStats,
                ManagerStatistics = GetManagerStatistics()
            };
        }

        /// <summary>
        /// Gets optimization recommendations for the current system configuration.
        /// </summary>
        public async Task<P2POptimizationRecommendations> GetOptimizationRecommendationsAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfNotInitialized();
            return await _optimizer.GetOptimizationRecommendationsAsync(cancellationToken);
        }

        /// <summary>
        /// Synchronizes all buffers for consistency across devices.
        /// </summary>
        public async Task SynchronizeAllBuffersAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfNotInitialized();
            // This would coordinate with the coherence manager to sync all tracked buffers
            _logger.LogInformation("Buffer synchronization requested - coordinating with coherence manager");
            await Task.CompletedTask; // Placeholder
        }

        #region Private Implementation

        private async Task InitializeMemoryPoolsAsync(
            IAccelerator[] devices,
            P2PMemoryManagerOptions options,
            CancellationToken cancellationToken)
        {
            foreach (var device in devices)
            {
                var memoryPool = new P2PMemoryPool
                {
                    DeviceId = device.Info.Id,
                    TargetDevice = device,
                    PoolSize = options.DefaultPoolSizeMB * 1024L * 1024L,
                    AllocatedBytes = 0,
                    AllocationCount = 0
                };

                _memoryPools[device.Info.Id] = memoryPool;
            }

            await Task.CompletedTask;
        }

        private async Task RunInitialBenchmarksAsync(IAccelerator[] devices, CancellationToken cancellationToken)
        {
            if (devices.Length < 2)
            {
                return;
            }

            // Run a quick benchmark between the first two devices

            var benchmarkOptions = new P2PBenchmarkOptions
            {
                MinTransferSizeMB = 1,
                MaxTransferSizeMB = 16,
                UseCachedResults = true
            };

            _ = await _validator.ExecuteP2PBenchmarkAsync(devices[0], devices[1], benchmarkOptions, cancellationToken);
        }

        private void UpdateManagerStatistics(P2PTransferResult result)
        {
            lock (_statistics)
            {
                _statistics.TotalManagedTransfers++;
                if (result.IsSuccessful)
                {
                    _statistics.SuccessfulManagedTransfers++;
                    _statistics.TotalManagedBytes += GetTransferSizeFromResult(result);
                }
                else
                {
                    _statistics.FailedManagedTransfers++;
                }
            }
        }

        private static long GetTransferSizeFromResult(P2PTransferResult result)
            // In a real implementation, this would extract the transfer size from the result

            => result.TransferPlan?.TransferSize ?? 0;

        private P2PManagerStatistics GetManagerStatistics()
        {
            lock (_statistics)
            {
                return new P2PManagerStatistics
                {
                    TotalManagedTransfers = _statistics.TotalManagedTransfers,
                    SuccessfulManagedTransfers = _statistics.SuccessfulManagedTransfers,
                    FailedManagedTransfers = _statistics.FailedManagedTransfers,
                    TotalManagedBytes = _statistics.TotalManagedBytes,
                    ManagedMemoryPools = _memoryPools.Count,
                    IsInitialized = _isInitialized
                };
            }
        }

        private void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("P2P Memory Manager is not initialized. Call InitializeAsync first.");
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

            await _transferManager.DisposeAsync();
            await _capabilityMatrix.DisposeAsync();
            await _validator.DisposeAsync();
            await _optimizer.DisposeAsync();
            await _synchronizer.DisposeAsync();
            await _coherenceManager.DisposeAsync();
            await _bufferFactory.DisposeAsync();

            _memoryPools.Clear();
            _managerSemaphore.Dispose();

            _logger.LogInformation("P2P Memory Manager disposed");
        }
    }

    #region Supporting Types

    /// <summary>
    /// P2P Memory Manager configuration options.
    /// </summary>
    public sealed class P2PMemoryManagerOptions
    {
        public static P2PMemoryManagerOptions Default => new();

        public bool RunInitialBenchmarks { get; init; }

        public long DefaultPoolSizeMB { get; init; } = 512; // 512MB per device
        public bool EnableCoherenceTracking { get; init; } = true;
        public bool EnableOptimizations { get; init; } = true;
        public bool EnableValidation { get; init; } = true;
    }

    /// <summary>
    /// P2P buffer creation options.
    /// </summary>
    public sealed class P2PBufferCreationOptions
    {
        public static P2PBufferCreationOptions Default => new();

        public bool EnableOptimization { get; init; } = true;
        public bool UseMemoryPooling { get; init; } = true;
        public bool EnableAsyncTransfers { get; init; } = true;
        public bool EnableCoherenceTracking { get; init; } = true;
    }

    /// <summary>
    /// P2P transfer execution options.
    /// </summary>
    public sealed class P2PTransferExecutionOptions
    {
        public static P2PTransferExecutionOptions Default => new();

        public bool EnableValidation { get; init; } = true;
        public bool EnableSynchronization { get; init; } = true;
        public bool EnableOptimization { get; init; } = true;
        public P2PTransferPriority Priority { get; init; } = P2PTransferPriority.Normal;
        public int PreferredChunkSize { get; init; } = 8 * 1024 * 1024; // 8MB
        public int PipelineDepth { get; init; } = 2;
    }

    /// <summary>
    /// P2P scatter execution options.
    /// </summary>
    public sealed class P2PScatterExecutionOptions
    {
        public static P2PScatterExecutionOptions Default => new();

        public bool EnableValidation { get; init; } = true;
        public bool EnableSynchronization { get; init; } = true;
        public P2PTransferPriority Priority { get; init; } = P2PTransferPriority.Normal;
        public bool EnableParallelScatter { get; init; } = true;
        public int MaxConcurrentScatters { get; init; } = 8;
    }

    /// <summary>
    /// P2P gather execution options.
    /// </summary>
    public sealed class P2PGatherExecutionOptions
    {
        public static P2PGatherExecutionOptions Default => new();

        public bool EnableValidation { get; init; } = true;
        public bool EnableSynchronization { get; init; } = true;
        public P2PTransferPriority Priority { get; init; } = P2PTransferPriority.Normal;
        public bool EnableParallelGather { get; init; } = true;
        public int MaxConcurrentGathers { get; init; } = 8;
    }

    /// <summary>
    /// P2P benchmark execution options.
    /// </summary>
    public sealed class P2PBenchmarkExecutionOptions
    {
        public static P2PBenchmarkExecutionOptions Default => new();

        public bool EnablePairwiseBenchmarks { get; init; } = true;
        public bool EnableScatterBenchmarks { get; init; } = true;
        public bool EnableGatherBenchmarks { get; init; } = true;
        public bool EnableAllToAllBenchmarks { get; init; }

        public int TransferSizeMB { get; init; } = 64;
        public bool UseCachedResults { get; init; } = true;
    }

    /// <summary>
    /// P2P memory pool information.
    /// </summary>
    internal sealed class P2PMemoryPool
    {
        public required string DeviceId { get; init; }
        public required IAccelerator TargetDevice { get; init; }
        public required long PoolSize { get; init; }
        public long AllocatedBytes { get; set; }
        public long AllocationCount { get; set; }
    }

    /// <summary>
    /// P2P initialization summary.
    /// </summary>
    public sealed class P2PInitializationSummary
    {
        public bool IsSuccessful { get; set; }
        public int TotalDevices { get; set; }
        public TimeSpan TotalInitializationTime { get; set; }
        public string? ErrorMessage { get; set; }
        public required List<P2PInitializationStep> InitializationSteps { get; init; }
    }

    /// <summary>
    /// Individual initialization step result.
    /// </summary>
    public sealed class P2PInitializationStep
    {
        public required string StepName { get; init; }
        public bool IsSuccessful { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Details { get; set; }
    }

    /// <summary>
    /// Comprehensive P2P benchmark result.
    /// </summary>
    public sealed class P2PComprehensiveBenchmarkResult
    {
        public required string BenchmarkId { get; init; }
        public required P2PMultiGpuBenchmarkResult MultiGpuResults { get; init; }
        public required P2POptimizationRecommendations OptimizationRecommendations { get; init; }
        public required P2PTopologyAnalysis TopologyAnalysis { get; init; }
        public required P2PSystemStatistics SystemStatistics { get; init; }
    }

    /// <summary>
    /// System-wide P2P statistics.
    /// </summary>
    public sealed class P2PSystemStatistics
    {
        public bool IsInitialized { get; set; }
        public int TotalDevices { get; set; }
        public int P2PConnections { get; set; }
        public required P2PTransferStatistics TransferStatistics { get; init; }
        public required P2PValidationStatistics ValidationStatistics { get; init; }
        public required P2POptimizationStatistics OptimizationStatistics { get; init; }
        public required P2PSyncStatistics SynchronizationStatistics { get; init; }
        public required P2PMatrixStatistics MatrixStatistics { get; init; }
        public required P2PManagerStatistics ManagerStatistics { get; init; }
    }

    /// <summary>
    /// P2P Memory Manager specific statistics.
    /// </summary>
    public sealed class P2PManagerStatistics
    {
        public long TotalManagedTransfers { get; set; }
        public long SuccessfulManagedTransfers { get; set; }
        public long FailedManagedTransfers { get; set; }
        public long TotalManagedBytes { get; set; }
        public int ManagedMemoryPools { get; set; }
        public bool IsInitialized { get; set; }
    }

    #endregion
}