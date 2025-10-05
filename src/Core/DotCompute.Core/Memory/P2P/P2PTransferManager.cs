// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
        private const double ValidationThresholdGB = 0.1; // Validate transfers > 100MB
        /// <summary>
        /// Initializes a new instance of the P2PTransferManager class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="capabilityDetector">The capability detector.</param>

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
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the total devices.
        /// </summary>
        /// <value>The total devices.</value>
        public int TotalDevices { get; set; }
        /// <summary>
        /// Gets or sets the successful connections.
        /// </summary>
        /// <value>The successful connections.</value>
        public int SuccessfulConnections { get; set; }
        /// <summary>
        /// Gets or sets the failed connections.
        /// </summary>
        /// <value>The failed connections.</value>
        public int FailedConnections { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the device pairs.
        /// </summary>
        /// <value>The device pairs.</value>
        public IList<P2PDevicePair> DevicePairs { get; set; } = [];
    }

    /// <summary>
    /// P2P device pair information.
    /// </summary>
    public sealed class P2PDevicePair
    {
        /// <summary>
        /// Gets or sets the device1.
        /// </summary>
        /// <value>The device1.</value>
        public required IAccelerator Device1 { get; init; }
        /// <summary>
        /// Gets or sets the device2.
        /// </summary>
        /// <value>The device2.</value>
        public required IAccelerator Device2 { get; init; }
        /// <summary>
        /// Gets or sets the capability.
        /// </summary>
        /// <value>The capability.</value>
        public required P2PConnectionCapability Capability { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether enabled.
        /// </summary>
        /// <value>The is enabled.</value>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// P2P transfer session tracking.
    /// </summary>
    public sealed class P2PTransferSession
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public required string Id { get; init; }
        /// <summary>
        /// Gets or sets the source device.
        /// </summary>
        /// <value>The source device.</value>
        public required IAccelerator SourceDevice { get; init; }
        /// <summary>
        /// Gets or sets the destination device.
        /// </summary>
        /// <value>The destination device.</value>
        public required IAccelerator DestinationDevice { get; init; }
        /// <summary>
        /// Gets or sets the transfer size.
        /// </summary>
        /// <value>The transfer size.</value>
        public required long TransferSize { get; init; }
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public required DateTimeOffset StartTime { get; init; }
        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        /// <value>The end time.</value>
        public DateTimeOffset? EndTime { get; set; }
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public required P2PTransferOptions Options { get; init; }
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>The status.</value>
        public P2PTransferStatus Status { get; set; }
        /// <summary>
        /// Gets or sets the transfer plan.
        /// </summary>
        /// <value>The transfer plan.</value>
        public P2PTransferPlan? TransferPlan { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// P2P transfer options for fine-tuned control.
    /// </summary>
    public sealed class P2PTransferOptions
    {
        /// <summary>
        /// Gets or sets the default.
        /// </summary>
        /// <value>The default.</value>
        public static P2PTransferOptions Default => new();
        /// <summary>
        /// Gets or sets the enable validation.
        /// </summary>
        /// <value>The enable validation.</value>

        public bool EnableValidation { get; init; } = true;
        /// <summary>
        /// Gets or sets the enable synchronization.
        /// </summary>
        /// <value>The enable synchronization.</value>
        public bool EnableSynchronization { get; init; } = true;
        /// <summary>
        /// Gets or sets the enable optimization.
        /// </summary>
        /// <value>The enable optimization.</value>
        public bool EnableOptimization { get; init; } = true;
        /// <summary>
        /// Gets or sets the allow host fallback.
        /// </summary>
        /// <value>The allow host fallback.</value>
        public bool AllowHostFallback { get; init; } = true;
        /// <summary>
        /// Gets or sets the preferred chunk size.
        /// </summary>
        /// <value>The preferred chunk size.</value>
        public int PreferredChunkSize { get; init; } = 8 * 1024 * 1024; // 8MB
        /// <summary>
        /// Gets or sets the pipeline depth.
        /// </summary>
        /// <value>The pipeline depth.</value>
        public int PipelineDepth { get; init; } = 2;
        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public P2PTransferPriority Priority { get; init; } = P2PTransferPriority.Normal;
    }

    /// <summary>
    /// P2P transfer result with comprehensive metrics.
    /// </summary>
    public sealed class P2PTransferResult
    {
        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        /// <value>The session id.</value>
        public required string SessionId { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public required bool IsSuccessful { get; init; }
        /// <summary>
        /// Gets or sets the transfer time ms.
        /// </summary>
        /// <value>The transfer time ms.</value>
        public required double TransferTimeMs { get; init; }
        /// <summary>
        /// Gets or sets the throughput g bps.
        /// </summary>
        /// <value>The throughput g bps.</value>
        public required double ThroughputGBps { get; init; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; init; }
        /// <summary>
        /// Gets or sets the validation results.
        /// </summary>
        /// <value>The validation results.</value>
        public P2PValidationResult? ValidationResults { get; init; }
        /// <summary>
        /// Gets or sets the transfer plan.
        /// </summary>
        /// <value>The transfer plan.</value>
        public P2PTransferPlan? TransferPlan { get; init; }
    }

    /// <summary>
    /// P2P scatter operation options.
    /// </summary>
    public sealed class P2PScatterOptions
    {
        /// <summary>
        /// Gets or sets the default.
        /// </summary>
        /// <value>The default.</value>
        public static P2PScatterOptions Default => new();
        /// <summary>
        /// Gets or sets the transfer options.
        /// </summary>
        /// <value>The transfer options.</value>

        public P2PTransferOptions TransferOptions { get; init; } = P2PTransferOptions.Default;
        /// <summary>
        /// Gets or sets the enable parallel scatter.
        /// </summary>
        /// <value>The enable parallel scatter.</value>
        public bool EnableParallelScatter { get; init; } = true;
        /// <summary>
        /// Gets or sets the max concurrent scatters.
        /// </summary>
        /// <value>The max concurrent scatters.</value>
        public int MaxConcurrentScatters { get; init; } = 8;
    }

    /// <summary>
    /// P2P gather operation options.
    /// </summary>
    public sealed class P2PGatherOptions
    {
        /// <summary>
        /// Gets or sets the default.
        /// </summary>
        /// <value>The default.</value>
        public static P2PGatherOptions Default => new();
        /// <summary>
        /// Gets or sets the transfer options.
        /// </summary>
        /// <value>The transfer options.</value>

        public P2PTransferOptions TransferOptions { get; init; } = P2PTransferOptions.Default;
        /// <summary>
        /// Gets or sets the enable parallel gather.
        /// </summary>
        /// <value>The enable parallel gather.</value>
        public bool EnableParallelGather { get; init; } = true;
        /// <summary>
        /// Gets or sets the max concurrent gathers.
        /// </summary>
        /// <value>The max concurrent gathers.</value>
        public int MaxConcurrentGathers { get; init; } = 8;
    }

    /// <summary>
    /// P2P scatter operation result.
    /// </summary>
    public sealed class P2PScatterResult
    {
        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        /// <value>The session id.</value>
        public required string SessionId { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the total transfer time ms.
        /// </summary>
        /// <value>The total transfer time ms.</value>
        public double TotalTransferTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the average throughput g bps.
        /// </summary>
        /// <value>The average throughput g bps.</value>
        public double AverageThroughputGBps { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the transfer results.
        /// </summary>
        /// <value>The transfer results.</value>
        public P2PTransferResult[] TransferResults { get; set; } = [];
    }

    /// <summary>
    /// P2P gather operation result.
    /// </summary>
    public sealed class P2PGatherResult
    {
        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        /// <value>The session id.</value>
        public required string SessionId { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the total transfer time ms.
        /// </summary>
        /// <value>The total transfer time ms.</value>
        public double TotalTransferTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the average throughput g bps.
        /// </summary>
        /// <value>The average throughput g bps.</value>
        public double AverageThroughputGBps { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the transfer results.
        /// </summary>
        /// <value>The transfer results.</value>
        public P2PTransferResult[] TransferResults { get; set; } = [];
    }

    /// <summary>
    /// P2P transfer comprehensive statistics.
    /// </summary>
    public sealed class P2PTransferStatistics
    {
        /// <summary>
        /// Gets or sets the total transfers.
        /// </summary>
        /// <value>The total transfers.</value>
        public long TotalTransfers { get; set; }
        /// <summary>
        /// Gets or sets the successful transfers.
        /// </summary>
        /// <value>The successful transfers.</value>
        public long SuccessfulTransfers { get; set; }
        /// <summary>
        /// Gets or sets the failed transfers.
        /// </summary>
        /// <value>The failed transfers.</value>
        public long FailedTransfers { get; set; }
        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        /// <value>The total bytes transferred.</value>
        public long TotalBytesTransferred { get; set; }
        /// <summary>
        /// Gets or sets the average throughput g bps.
        /// </summary>
        /// <value>The average throughput g bps.</value>
        public double AverageThroughputGBps { get; set; }
        /// <summary>
        /// Gets or sets the peak throughput g bps.
        /// </summary>
        /// <value>The peak throughput g bps.</value>
        public double PeakThroughputGBps { get; set; }
        /// <summary>
        /// Gets or sets the total transfer time.
        /// </summary>
        /// <value>The total transfer time.</value>
        public TimeSpan TotalTransferTime { get; set; }
        /// <summary>
        /// Gets or sets the direct p2 p transfers.
        /// </summary>
        /// <value>The direct p2 p transfers.</value>
        public long DirectP2PTransfers { get; set; }
        /// <summary>
        /// Gets or sets the host mediated transfers.
        /// </summary>
        /// <value>The host mediated transfers.</value>
        public long HostMediatedTransfers { get; set; }
        /// <summary>
        /// Gets or sets the active sessions.
        /// </summary>
        /// <value>The active sessions.</value>
        public int ActiveSessions { get; set; }
    }
    /// <summary>
    /// An p2 p transfer status enumeration.
    /// </summary>

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
    /// An p2 p transfer strategy enumeration.
    /// </summary>

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
    /// An p2 p transfer priority enumeration.
    /// </summary>

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
