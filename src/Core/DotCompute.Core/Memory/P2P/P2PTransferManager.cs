// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Comprehensive P2P Transfer Manager that coordinates all peer-to-peer operations
    /// across multiple GPU devices with advanced optimization and validation.
    /// </summary>
    public sealed class P2PTransferManager : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly P2PCapabilityMatrix _capabilityMatrix;
        private readonly P2PValidator _validator;
        private readonly P2POptimizer _optimizer;
        private readonly P2PSynchronizer _synchronizer;
        private readonly P2PTransferScheduler _scheduler;
        private readonly ConcurrentDictionary<string, P2PTransferSession> _activeSessions;
        private readonly SemaphoreSlim _transferSemaphore;
        private readonly P2PTransferStatistics _statistics;
        private bool _disposed;

        // Transfer configuration
        private const int MaxConcurrentTransfers = 16;
        private const int DefaultChunkSizeBytes = 8 * 1024 * 1024; // 8MB
        private const double ValidationThresholdGB = 0.1; // Validate transfers > 100MB

        public P2PTransferManager(
            ILogger logger,
            P2PCapabilityDetector capabilityDetector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityMatrix = new P2PCapabilityMatrix(logger);
            _validator = new P2PValidator(logger);
            _optimizer = new P2POptimizer(logger, _capabilityMatrix);
            _synchronizer = new P2PSynchronizer(logger);
            _scheduler = new P2PTransferScheduler(logger);
            _activeSessions = new ConcurrentDictionary<string, P2PTransferSession>();
            _transferSemaphore = new SemaphoreSlim(MaxConcurrentTransfers, MaxConcurrentTransfers);
            _statistics = new P2PTransferStatistics();

            _logger.LogInfoMessage("P2P Transfer Manager initialized with {MaxConcurrentTransfers} concurrent transfers");
        }

        /// <summary>
        /// Initializes P2P capabilities across all provided devices.
        /// </summary>
        public async Task<P2PInitializationResult> InitializeP2PTopologyAsync(
            IAccelerator[] devices,
            CancellationToken cancellationToken = default)
        {
            if (devices == null || devices.Length == 0)
            {

                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }


            _logger.LogInfoMessage("Initializing P2P topology for {devices.Length} devices");

            var initResult = new P2PInitializationResult();
            var detectedPairs = new List<P2PDevicePair>();

            try
            {
                // Build capability matrix
                await _capabilityMatrix.BuildMatrixAsync(devices, cancellationToken);

                // Validate all device pairs
                for (var i = 0; i < devices.Length; i++)
                {
                    for (var j = i + 1; j < devices.Length; j++)
                    {
                        var device1 = devices[i];
                        var device2 = devices[j];


                        var capability = await _capabilityMatrix.GetP2PCapabilityAsync(device1, device2, cancellationToken);


                        var pair = new P2PDevicePair
                        {
                            Device1 = device1,
                            Device2 = device2,
                            Capability = capability,
                            IsEnabled = capability.IsSupported
                        };

                        detectedPairs.Add(pair);

                        if (capability.IsSupported)
                        {
                            initResult.SuccessfulConnections++;
                            _logger.LogDebugMessage($"P2P enabled: {device1.Info.Name} <-> {device2.Info.Name} ({capability.ConnectionType}, {capability.EstimatedBandwidthGBps} GB/s)");
                        }
                        else
                        {
                            initResult.FailedConnections++;
                            _logger.LogDebugMessage($"P2P not available: {device1.Info.Name} <-> {device2.Info.Name} ({capability.LimitationReason})");
                        }
                    }
                }

                // Initialize optimizer with topology
                await _optimizer.InitializeTopologyAsync(detectedPairs, cancellationToken);

                // Initialize synchronizer
                await _synchronizer.InitializeDevicesAsync(devices, cancellationToken);

                initResult.TotalDevices = devices.Length;
                initResult.DevicePairs = detectedPairs;
                initResult.IsSuccessful = initResult.SuccessfulConnections > 0;

                _logger.LogInfoMessage($"P2P topology initialization completed: {initResult.SuccessfulConnections}/{initResult.TotalDevices * (initResult.TotalDevices - 1) / 2} connections, {initResult.FailedConnections} failed");

                return initResult;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "P2P topology initialization failed");
                initResult.IsSuccessful = false;
                initResult.ErrorMessage = ex.Message;
                return initResult;
            }
        }

        /// <summary>
        /// Executes a high-performance P2P transfer with full optimization and validation.
        /// </summary>
        public async Task<P2PTransferResult> ExecuteP2PTransferAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            P2PTransferOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);


            ArgumentNullException.ThrowIfNull(destinationBuffer);


            options ??= P2PTransferOptions.Default;
            var sessionId = Guid.NewGuid().ToString();
            var transferSize = sourceBuffer.SizeInBytes;

            await _transferSemaphore.WaitAsync(cancellationToken);
            try
            {
                var session = new P2PTransferSession
                {
                    Id = sessionId,
                    SourceDevice = sourceBuffer.Accelerator,
                    DestinationDevice = destinationBuffer.Accelerator,
                    TransferSize = transferSize,
                    StartTime = DateTimeOffset.UtcNow,
                    Options = options,
                    Status = P2PTransferStatus.Initializing
                };

                _activeSessions[sessionId] = session;

                try
                {
                    // Phase 1: Optimization and path planning
                    var transferPlan = await _optimizer.CreateOptimalTransferPlanAsync(
                        sourceBuffer.Accelerator, destinationBuffer.Accelerator, transferSize, options, cancellationToken);

                    session.TransferPlan = transferPlan;
                    session.Status = P2PTransferStatus.Planned;

                    _logger.LogDebugMessage($"P2P transfer planned: {sourceBuffer.Accelerator.Info.Name} -> {destinationBuffer.Accelerator.Info.Name}, Strategy: {transferPlan.Strategy}, Estimated: {transferPlan.EstimatedTransferTimeMs}ms");

                    // Phase 2: Pre-transfer validation (for large transfers)
                    if (options.EnableValidation && transferSize > ValidationThresholdGB * 1024 * 1024 * 1024)
                    {
                        var preValidation = await _validator.ValidateTransferReadinessAsync(
                            sourceBuffer, destinationBuffer, transferPlan, cancellationToken);

                        if (!preValidation.IsValid)
                        {
                            session.Status = P2PTransferStatus.ValidationFailed;
                            return new P2PTransferResult
                            {
                                SessionId = sessionId,
                                IsSuccessful = false,
                                ErrorMessage = $"Pre-transfer validation failed: {preValidation.ErrorMessage}",
                                TransferTimeMs = 0,
                                ThroughputGBps = 0,
                                ValidationResults = preValidation
                            };
                        }
                    }

                    // Phase 3: Synchronization and barrier setup
                    if (options.EnableSynchronization)
                    {
                        await _synchronizer.EstablishTransferBarrierAsync(
                            sourceBuffer.Accelerator, destinationBuffer.Accelerator, sessionId, cancellationToken);
                    }

                    session.Status = P2PTransferStatus.Transferring;
                    var transferStartTime = DateTimeOffset.UtcNow;

                    // Phase 4: Execute optimized transfer
                    await ExecuteOptimizedTransferAsync(sourceBuffer, destinationBuffer, transferPlan, session, cancellationToken);

                    var transferDuration = DateTimeOffset.UtcNow - transferStartTime;
                    var throughputGBps = (transferSize / (1024.0 * 1024.0 * 1024.0)) / transferDuration.TotalSeconds;

                    session.Status = P2PTransferStatus.Validating;

                    // Phase 5: Post-transfer validation
                    P2PValidationResult? validationResult = null;
                    if (options.EnableValidation)
                    {
                        validationResult = await _validator.ValidateTransferIntegrityAsync(
                            sourceBuffer, destinationBuffer, transferPlan, cancellationToken);

                        if (!validationResult.IsValid)
                        {
                            session.Status = P2PTransferStatus.ValidationFailed;
                            return new P2PTransferResult
                            {
                                SessionId = sessionId,
                                IsSuccessful = false,
                                ErrorMessage = $"Post-transfer validation failed: {validationResult.ErrorMessage}",
                                TransferTimeMs = transferDuration.TotalMilliseconds,
                                ThroughputGBps = throughputGBps,
                                ValidationResults = validationResult
                            };
                        }
                    }

                    // Phase 6: Cleanup and synchronization release
                    if (options.EnableSynchronization)
                    {
                        await _synchronizer.ReleaseTransferBarrierAsync(sessionId, cancellationToken);
                    }

                    session.Status = P2PTransferStatus.Completed;
                    session.EndTime = DateTimeOffset.UtcNow;

                    // Update statistics
                    UpdateTransferStatistics(transferSize, transferDuration, throughputGBps, transferPlan.Strategy);

                    _logger.LogInfoMessage($"P2P transfer completed: {transferSize} bytes in {transferDuration.TotalMilliseconds}ms ({throughputGBps} GB/s)");

                    return new P2PTransferResult
                    {
                        SessionId = sessionId,
                        IsSuccessful = true,
                        TransferTimeMs = transferDuration.TotalMilliseconds,
                        ThroughputGBps = throughputGBps,
                        ValidationResults = validationResult,
                        TransferPlan = transferPlan
                    };
                }
                catch (Exception ex)
                {
                    session.Status = P2PTransferStatus.Failed;
                    session.ErrorMessage = ex.Message;
                    _logger.LogErrorMessage(ex, $"P2P transfer failed: {sourceBuffer.Accelerator.Info.Name} -> {destinationBuffer.Accelerator.Info.Name}");

                    return new P2PTransferResult
                    {
                        SessionId = sessionId,
                        IsSuccessful = false,
                        ErrorMessage = ex.Message,
                        TransferTimeMs = 0,
                        ThroughputGBps = 0
                    };
                }
                finally
                {
                    _ = _activeSessions.TryRemove(sessionId, out _);
                }
            }
            finally
            {
                _ = _transferSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a multi-buffer scatter operation with P2P optimization.
        /// </summary>
        public async Task<P2PScatterResult> ExecuteP2PScatterAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T>[] destinationBuffers,
            P2PScatterOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);


            if (destinationBuffers == null || destinationBuffers.Length == 0)
            {

                throw new ArgumentException("At least one destination buffer is required", nameof(destinationBuffers));
            }


            options ??= P2PScatterOptions.Default;
            var scatterResult = new P2PScatterResult { SessionId = Guid.NewGuid().ToString() };

            try
            {
                // Create scatter plan
                var scatterPlan = await _optimizer.CreateScatterPlanAsync(
                    sourceBuffer, destinationBuffers, options, cancellationToken);

                var scatterTasks = new List<Task<P2PTransferResult>>();

                // Execute parallel scatter operations
                for (var i = 0; i < destinationBuffers.Length; i++)
                {
                    var destBuffer = destinationBuffers[i];
                    var scatterChunk = scatterPlan.Chunks[i];


                    var scatterTask = ExecuteScatterChunkAsync(
                        sourceBuffer, destBuffer, scatterChunk, options.TransferOptions, cancellationToken);


                    scatterTasks.Add(scatterTask);
                }

                var transferResults = await Task.WhenAll(scatterTasks);

                scatterResult.IsSuccessful = transferResults.All(r => r.IsSuccessful);
                scatterResult.TransferResults = transferResults;
                scatterResult.TotalTransferTimeMs = transferResults.Max(r => r.TransferTimeMs);
                scatterResult.AverageThroughputGBps = transferResults.Where(r => r.IsSuccessful).Average(r => r.ThroughputGBps);

                return scatterResult;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "P2P scatter operation failed");
                scatterResult.IsSuccessful = false;
                scatterResult.ErrorMessage = ex.Message;
                return scatterResult;
            }
        }

        /// <summary>
        /// Executes a multi-buffer gather operation with P2P optimization.
        /// </summary>
        public async Task<P2PGatherResult> ExecuteP2PGatherAsync<T>(
            IUnifiedMemoryBuffer<T>[] sourceBuffers,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            P2PGatherOptions? options = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (sourceBuffers == null || sourceBuffers.Length == 0)
            {

                throw new ArgumentException("At least one source buffer is required", nameof(sourceBuffers));
            }


            ArgumentNullException.ThrowIfNull(destinationBuffer);


            options ??= P2PGatherOptions.Default;
            var gatherResult = new P2PGatherResult { SessionId = Guid.NewGuid().ToString() };

            try
            {
                // Create gather plan
                var gatherPlan = await _optimizer.CreateGatherPlanAsync(
                    sourceBuffers, destinationBuffer, options, cancellationToken);

                var gatherTasks = new List<Task<P2PTransferResult>>();

                // Execute parallel gather operations
                for (var i = 0; i < sourceBuffers.Length; i++)
                {
                    var srcBuffer = sourceBuffers[i];
                    var gatherChunk = gatherPlan.Chunks[i];


                    var gatherTask = ExecuteGatherChunkAsync(
                        srcBuffer, destinationBuffer, gatherChunk, options.TransferOptions, cancellationToken);


                    gatherTasks.Add(gatherTask);
                }

                var transferResults = await Task.WhenAll(gatherTasks);

                gatherResult.IsSuccessful = transferResults.All(r => r.IsSuccessful);
                gatherResult.TransferResults = transferResults;
                gatherResult.TotalTransferTimeMs = transferResults.Max(r => r.TransferTimeMs);
                gatherResult.AverageThroughputGBps = transferResults.Where(r => r.IsSuccessful).Average(r => r.ThroughputGBps);

                return gatherResult;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "P2P gather operation failed");
                gatherResult.IsSuccessful = false;
                gatherResult.ErrorMessage = ex.Message;
                return gatherResult;
            }
        }

        /// <summary>
        /// Gets comprehensive P2P transfer statistics.
        /// </summary>
        public P2PTransferStatistics GetTransferStatistics()
        {
            lock (_statistics)
            {
                return new P2PTransferStatistics
                {
                    TotalTransfers = _statistics.TotalTransfers,
                    SuccessfulTransfers = _statistics.SuccessfulTransfers,
                    FailedTransfers = _statistics.FailedTransfers,
                    TotalBytesTransferred = _statistics.TotalBytesTransferred,
                    AverageThroughputGBps = _statistics.AverageThroughputGBps,
                    PeakThroughputGBps = _statistics.PeakThroughputGBps,
                    TotalTransferTime = _statistics.TotalTransferTime,
                    DirectP2PTransfers = _statistics.DirectP2PTransfers,
                    HostMediatedTransfers = _statistics.HostMediatedTransfers,
                    ActiveSessions = _activeSessions.Count
                };
            }
        }

        /// <summary>
        /// Gets capability matrix for topology analysis.
        /// </summary>
        public P2PCapabilityMatrix GetCapabilityMatrix() => _capabilityMatrix;

        /// <summary>
        /// Gets active transfer sessions.
        /// </summary>
        public IReadOnlyList<P2PTransferSession> GetActiveSessions() => _activeSessions.Values.ToList();

        #region Private Implementation

        private async Task ExecuteOptimizedTransferAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> destinationBuffer,
            P2PTransferPlan transferPlan,
            P2PTransferSession session,
            CancellationToken cancellationToken) where T : unmanaged
        {
            switch (transferPlan.Strategy)
            {
                case P2PTransferStrategy.DirectP2P:
                    await ExecuteDirectP2PTransferAsync(sourceBuffer, destinationBuffer, transferPlan, cancellationToken);
                    break;

                case P2PTransferStrategy.ChunkedP2P:
                    await ExecuteChunkedP2PTransferAsync(sourceBuffer, destinationBuffer, transferPlan, cancellationToken);
                    break;

                case P2PTransferStrategy.PipelinedP2P:
                    await ExecutePipelinedP2PTransferAsync(sourceBuffer, destinationBuffer, transferPlan, cancellationToken);
                    break;

                case P2PTransferStrategy.HostMediated:
                    await ExecuteHostMediatedTransferAsync(sourceBuffer, destinationBuffer, transferPlan, cancellationToken);
                    break;

                default:
                    throw new NotSupportedException($"Transfer strategy {transferPlan.Strategy} is not supported");
            }
        }

        private static async Task ExecuteDirectP2PTransferAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            P2PTransferPlan plan,
            CancellationToken cancellationToken) where T : unmanaged
            // Single direct P2P transfer

            => await source.CopyToAsync(destination, cancellationToken);

        private static async Task ExecuteChunkedP2PTransferAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            P2PTransferPlan plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var chunkSize = plan.ChunkSize;
            var elementSize = Unsafe.SizeOf<T>();
            var elementsPerChunk = chunkSize / elementSize;
            var totalElements = source.Length;

            var chunkTasks = new List<Task>();

            for (var offset = 0; offset < totalElements; offset += elementsPerChunk)
            {
                var chunkElements = Math.Min(elementsPerChunk, totalElements - offset);
                var chunkTask = source.CopyToAsync(offset, destination, offset, chunkElements, cancellationToken);
                chunkTasks.Add(chunkTask.AsTask());

                // Limit concurrency to prevent memory pressure
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

        private async Task ExecutePipelinedP2PTransferAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            P2PTransferPlan plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Pipelined transfer with overlapped operations
            var pipeline = new P2PPipeline<T>(source, destination, plan.ChunkSize, plan.PipelineDepth, _logger);
            await pipeline.ExecuteAsync(cancellationToken);
        }

        private static async Task ExecuteHostMediatedTransferAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            P2PTransferPlan plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Host-mediated transfer via CPU memory
            var hostData = new T[source.Length];
            await source.CopyToAsync(hostData.AsMemory(), cancellationToken);
            await destination.CopyFromAsync(hostData.AsMemory(), cancellationToken);
        }

        private async Task<P2PTransferResult> ExecuteScatterChunkAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            P2PTransferChunk chunk,
            P2PTransferOptions options,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Create a slice of the source buffer for this chunk
            var sourceSlice = source.Slice(chunk.SourceOffset, chunk.ElementCount);
            var destSlice = destination.Slice(chunk.DestinationOffset, chunk.ElementCount);

            return await ExecuteP2PTransferAsync(sourceSlice, destSlice, options, cancellationToken);
        }

        private async Task<P2PTransferResult> ExecuteGatherChunkAsync<T>(
            IUnifiedMemoryBuffer<T> source,
            IUnifiedMemoryBuffer<T> destination,
            P2PTransferChunk chunk,
            P2PTransferOptions options,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Create slices for gather operation
            var sourceSlice = source.Slice(chunk.SourceOffset, chunk.ElementCount);
            var destSlice = destination.Slice(chunk.DestinationOffset, chunk.ElementCount);

            return await ExecuteP2PTransferAsync(sourceSlice, destSlice, options, cancellationToken);
        }

        private void UpdateTransferStatistics(long transferSize, TimeSpan duration, double throughputGBps, P2PTransferStrategy strategy)
        {
            lock (_statistics)
            {
                _statistics.TotalTransfers++;
                _statistics.SuccessfulTransfers++;
                _statistics.TotalBytesTransferred += transferSize;
                _statistics.TotalTransferTime += duration;

                if (throughputGBps > _statistics.PeakThroughputGBps)
                {
                    _statistics.PeakThroughputGBps = throughputGBps;
                }

                // Calculate running average throughput
                _statistics.AverageThroughputGBps = _statistics.TotalTransfers > 0
                    ? (_statistics.TotalBytesTransferred / (1024.0 * 1024.0 * 1024.0)) / _statistics.TotalTransferTime.TotalSeconds
                    : 0.0;

                // Update strategy counters
                if (strategy is P2PTransferStrategy.DirectP2P or P2PTransferStrategy.ChunkedP2P or P2PTransferStrategy.PipelinedP2P)
                {
                    _statistics.DirectP2PTransfers++;
                }
                else
                {
                    _statistics.HostMediatedTransfers++;
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

            // Cancel all active sessions
            foreach (var session in _activeSessions.Values)
            {
                session.Status = P2PTransferStatus.Cancelled;
            }

            // Dispose components
            await _scheduler.DisposeAsync();
            await _synchronizer.DisposeAsync();
            await _optimizer.DisposeAsync();
            await _validator.DisposeAsync();
            await _capabilityMatrix.DisposeAsync();

            _activeSessions.Clear();
            _transferSemaphore.Dispose();

            _logger.LogInfoMessage("P2P Transfer Manager disposed");
        }
    }

    #region Supporting Types

    /// <summary>
    /// P2P initialization result with detailed topology information.
    /// </summary>
    public sealed class P2PInitializationResult
    {
        public bool IsSuccessful { get; set; }
        public int TotalDevices { get; set; }
        public int SuccessfulConnections { get; set; }
        public int FailedConnections { get; set; }
        public string? ErrorMessage { get; set; }
        public List<P2PDevicePair> DevicePairs { get; set; } = [];
    }

    /// <summary>
    /// P2P device pair information.
    /// </summary>
    public sealed class P2PDevicePair
    {
        public required IAccelerator Device1 { get; init; }
        public required IAccelerator Device2 { get; init; }
        public required P2PConnectionCapability Capability { get; init; }
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// P2P transfer session tracking.
    /// </summary>
    public sealed class P2PTransferSession
    {
        public required string Id { get; init; }
        public required IAccelerator SourceDevice { get; init; }
        public required IAccelerator DestinationDevice { get; init; }
        public required long TransferSize { get; init; }
        public required DateTimeOffset StartTime { get; init; }
        public DateTimeOffset? EndTime { get; set; }
        public required P2PTransferOptions Options { get; init; }
        public P2PTransferStatus Status { get; set; }
        public P2PTransferPlan? TransferPlan { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// P2P transfer options for fine-tuned control.
    /// </summary>
    public sealed class P2PTransferOptions
    {
        public static P2PTransferOptions Default => new();

        public bool EnableValidation { get; init; } = true;
        public bool EnableSynchronization { get; init; } = true;
        public bool EnableOptimization { get; init; } = true;
        public bool AllowHostFallback { get; init; } = true;
        public int PreferredChunkSize { get; init; } = 8 * 1024 * 1024; // 8MB
        public int PipelineDepth { get; init; } = 2;
        public P2PTransferPriority Priority { get; init; } = P2PTransferPriority.Normal;
    }

    /// <summary>
    /// P2P transfer result with comprehensive metrics.
    /// </summary>
    public sealed class P2PTransferResult
    {
        public required string SessionId { get; init; }
        public required bool IsSuccessful { get; init; }
        public required double TransferTimeMs { get; init; }
        public required double ThroughputGBps { get; init; }
        public string? ErrorMessage { get; init; }
        public P2PValidationResult? ValidationResults { get; init; }
        public P2PTransferPlan? TransferPlan { get; init; }
    }

    /// <summary>
    /// P2P scatter operation options.
    /// </summary>
    public sealed class P2PScatterOptions
    {
        public static P2PScatterOptions Default => new();

        public P2PTransferOptions TransferOptions { get; init; } = P2PTransferOptions.Default;
        public bool EnableParallelScatter { get; init; } = true;
        public int MaxConcurrentScatters { get; init; } = 8;
    }

    /// <summary>
    /// P2P gather operation options.
    /// </summary>
    public sealed class P2PGatherOptions
    {
        public static P2PGatherOptions Default => new();

        public P2PTransferOptions TransferOptions { get; init; } = P2PTransferOptions.Default;
        public bool EnableParallelGather { get; init; } = true;
        public int MaxConcurrentGathers { get; init; } = 8;
    }

    /// <summary>
    /// P2P scatter operation result.
    /// </summary>
    public sealed class P2PScatterResult
    {
        public required string SessionId { get; init; }
        public bool IsSuccessful { get; set; }
        public double TotalTransferTimeMs { get; set; }
        public double AverageThroughputGBps { get; set; }
        public string? ErrorMessage { get; set; }
        public P2PTransferResult[] TransferResults { get; set; } = [];
    }

    /// <summary>
    /// P2P gather operation result.
    /// </summary>
    public sealed class P2PGatherResult
    {
        public required string SessionId { get; init; }
        public bool IsSuccessful { get; set; }
        public double TotalTransferTimeMs { get; set; }
        public double AverageThroughputGBps { get; set; }
        public string? ErrorMessage { get; set; }
        public P2PTransferResult[] TransferResults { get; set; } = [];
    }

    /// <summary>
    /// P2P transfer comprehensive statistics.
    /// </summary>
    public sealed class P2PTransferStatistics
    {
        public long TotalTransfers { get; set; }
        public long SuccessfulTransfers { get; set; }
        public long FailedTransfers { get; set; }
        public long TotalBytesTransferred { get; set; }
        public double AverageThroughputGBps { get; set; }
        public double PeakThroughputGBps { get; set; }
        public TimeSpan TotalTransferTime { get; set; }
        public long DirectP2PTransfers { get; set; }
        public long HostMediatedTransfers { get; set; }
        public int ActiveSessions { get; set; }
    }

    /// <summary>
    /// P2P transfer status enumeration.
    /// </summary>
    public enum P2PTransferStatus
    {
        Initializing,
        Planned,
        Transferring,
        Validating,
        Completed,
        ValidationFailed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// P2P transfer strategies for optimization.
    /// </summary>
    public enum P2PTransferStrategy
    {
        DirectP2P,      // Single direct P2P transfer
        ChunkedP2P,     // Chunked P2P transfers
        PipelinedP2P,   // Pipelined overlapped transfers
        HostMediated    // Host-mediated fallback
    }

    /// <summary>
    /// P2P transfer priority levels.
    /// </summary>
    public enum P2PTransferPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    #endregion
}
