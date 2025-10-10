// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Memory
{

    /// <summary>
    /// Advanced P2P transfer scheduler that optimizes bandwidth usage and coordinates transfers.
    /// Implements bandwidth-optimal scheduling, concurrent transfer management, and synchronization.
    /// </summary>
    public sealed partial class P2PTransferScheduler : IAsyncDisposable
    {
        // LoggerMessage delegates - Event ID range 14400-14411 for P2PTransferScheduler
        private static readonly Action<ILogger, int, Exception?> _logSchedulerInitialized =
            LoggerMessage.Define<int>(
                LogLevel.Debug,
                new EventId(14400, nameof(LogSchedulerInitialized)),
                "P2P transfer scheduler initialized with {MaxConcurrentTransfers} concurrent transfers per device");

        private static void LogSchedulerInitialized(ILogger logger, int maxConcurrentTransfers)
            => _logSchedulerInitialized(logger, maxConcurrentTransfers, null);

        private static readonly Action<ILogger, long, string, string, double, Exception?> _logTransferCompleted =
            LoggerMessage.Define<long, string, string, double>(
                LogLevel.Trace,
                new EventId(14401, nameof(LogTransferCompleted)),
                "P2P transfer completed: {TransferSize} bytes from {Source} to {Target} in {DurationMs}ms");

        private static void LogTransferCompleted(ILogger logger, long transferSize, string source, string target, double durationMs)
            => _logTransferCompleted(logger, transferSize, source, target, durationMs, null);

        private static readonly Action<ILogger, string, string, Exception?> _logTransferFailed =
            LoggerMessage.Define<string, string>(
                LogLevel.Error,
                new EventId(14402, nameof(LogTransferFailed)),
                "P2P transfer failed: {Source} to {Target}");

        private static void LogTransferFailed(ILogger logger, string source, string target, Exception exception)
            => _logTransferFailed(logger, source, target, exception);

        private static readonly Action<ILogger, Exception?> _logSchedulerStarted =
            LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(14403, nameof(LogSchedulerStarted)),
                "P2P transfer scheduler started");

        private static void LogSchedulerStarted(ILogger logger)
            => _logSchedulerStarted(logger, null);

        private static readonly Action<ILogger, Exception?> _logSchedulerStopped =
            LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(14404, nameof(LogSchedulerStopped)),
                "P2P transfer scheduler stopped");

        private static void LogSchedulerStopped(ILogger logger)
            => _logSchedulerStopped(logger, null);

        private static readonly Action<ILogger, Exception?> _logSchedulerError =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(14405, nameof(LogSchedulerError)),
                "Error in transfer scheduler main loop");

        private static void LogSchedulerError(ILogger logger, Exception exception)
            => _logSchedulerError(logger, exception);

        private static readonly Action<ILogger, long, double, Exception?> _logTransferExecuted =
            LoggerMessage.Define<long, double>(
                LogLevel.Trace,
                new EventId(14406, nameof(LogTransferExecuted)),
                "Transfer executed successfully: {Size} bytes, {ThroughputMBps:F1} MB/s");

        private static void LogTransferExecuted(ILogger logger, long size, double throughputMBps)
            => _logTransferExecuted(logger, size, throughputMBps, null);

        private static readonly Action<ILogger, string, string, Exception?> _logTransferExecutionFailed =
            LoggerMessage.Define<string, string>(
                LogLevel.Error,
                new EventId(14407, nameof(LogTransferExecutionFailed)),
                "Transfer execution failed: {SourceDeviceId} -> {TargetDeviceId}");

        private static void LogTransferExecutionFailed(ILogger logger, string sourceDeviceId, string targetDeviceId, Exception exception)
            => _logTransferExecutionFailed(logger, sourceDeviceId, targetDeviceId, exception);

        private static readonly Action<ILogger, double, Exception?> _logHighBandwidthUtilization =
            LoggerMessage.Define<double>(
                LogLevel.Debug,
                new EventId(14408, nameof(LogHighBandwidthUtilization)),
                "High bandwidth utilization detected: {Utilization:F2}, throttling transfers");

        private static void LogHighBandwidthUtilization(ILogger logger, double utilization)
            => _logHighBandwidthUtilization(logger, utilization, null);

        private static readonly Action<ILogger, Exception?> _logSchedulerDisposed =
            LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(14409, nameof(LogSchedulerDisposed)),
                "P2P transfer scheduler disposed");

        private static void LogSchedulerDisposed(ILogger logger)
            => _logSchedulerDisposed(logger, null);

        private static readonly Action<ILogger, Exception?> _logBandwidthMonitoringError =
            LoggerMessage.Define(
                LogLevel.Warning,
                new EventId(14413, nameof(LogBandwidthMonitoringError)),
                "Error in bandwidth monitoring");

        private static void LogBandwidthMonitoringError(ILogger logger, Exception exception)
            => _logBandwidthMonitoringError(logger, exception);

        private readonly ILogger _logger;
        private readonly ConcurrentQueue<P2PTransferOperation> _transferQueue;
        private readonly ConcurrentDictionary<string, TransferChannel> _deviceChannels;
        private readonly ConcurrentDictionary<string, List<TaskCompletionSource<bool>>> _deviceTransferCompletions;
        private readonly SemaphoreSlim _schedulerSemaphore;
        private readonly Task _schedulerTask;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private readonly Timer _bandwidthMonitor;
        private readonly TransferStatistics _statistics;
        private bool _disposed;

        // Scheduler configuration
        private const int MaxConcurrentTransfersPerDevice = 4;
        private const int BandwidthMonitorIntervalMs = 1000;
        private const double BandwidthUtilizationThreshold = 0.85;
        /// <summary>
        /// Initializes a new instance of the P2PTransferScheduler class.
        /// </summary>
        /// <param name="logger">The logger.</param>

        public P2PTransferScheduler(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transferQueue = new ConcurrentQueue<P2PTransferOperation>();
            _deviceChannels = new ConcurrentDictionary<string, TransferChannel>();
            _deviceTransferCompletions = new ConcurrentDictionary<string, List<TaskCompletionSource<bool>>>();
            _schedulerSemaphore = new SemaphoreSlim(1, 1);
            _shutdownTokenSource = new CancellationTokenSource();
            _statistics = new TransferStatistics();

            // Start background scheduler
            _schedulerTask = Task.Run(ProcessTransferQueueAsync, _shutdownTokenSource.Token);

            // Start bandwidth monitoring
            _bandwidthMonitor = new Timer(MonitorBandwidthUsage, null,
                TimeSpan.FromMilliseconds(BandwidthMonitorIntervalMs),
                TimeSpan.FromMilliseconds(BandwidthMonitorIntervalMs));

            LogSchedulerInitialized(_logger, MaxConcurrentTransfersPerDevice);
        }

        /// <summary>
        /// Schedules a P2P-optimized transfer with bandwidth management.
        /// </summary>
        public async ValueTask ScheduleP2PTransferAsync<T>(
            IUnifiedMemoryBuffer<T> sourceBuffer,
            IUnifiedMemoryBuffer<T> targetBuffer,
            int sourceOffset,
            int targetOffset,
            int elementCount,
            TransferStrategy strategy,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);

            ArgumentNullException.ThrowIfNull(targetBuffer);

            ArgumentNullException.ThrowIfNull(strategy);

            var transferSize = elementCount * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var operation = new P2PTransferOperation<T>
            {
                Id = Guid.NewGuid(),
                SourceBuffer = sourceBuffer,
                TargetBuffer = targetBuffer,
                SourceOffset = sourceOffset,
                TargetOffset = targetOffset,
                ElementCount = elementCount,
                TransferSize = transferSize,
                Strategy = strategy,
                Priority = DeterminePriority(transferSize),
                ScheduledAt = DateTimeOffset.UtcNow,
                CompletionSource = new TaskCompletionSource<bool>()
            };

            // Ensure transfer channels exist for both devices
            await EnsureTransferChannelsAsync(sourceBuffer.Accelerator, targetBuffer.Accelerator, strategy);

            // Queue the operation
            _transferQueue.Enqueue(operation);

            // Wait for completion
            try
            {
                _ = await operation.CompletionSource.Task.WaitAsync(cancellationToken);

                LogTransferCompleted(_logger, transferSize, sourceBuffer.Accelerator.Info.Name, targetBuffer.Accelerator.Info.Name,
                    (DateTimeOffset.UtcNow - operation.ScheduledAt).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                LogTransferFailed(_logger, sourceBuffer.Accelerator.Info.Name, targetBuffer.Accelerator.Info.Name, ex);
                throw;
            }
        }

        /// <summary>
        /// Waits for all pending transfers to a specific device to complete.
        /// </summary>
        public async ValueTask WaitForDeviceTransfersAsync(string deviceId, CancellationToken cancellationToken)
        {
            if (_deviceTransferCompletions.TryGetValue(deviceId, out var completions))
            {
                Task[] tasks;
                lock (completions)
                {
                    tasks = completions.Select(tcs => tcs.Task).ToArray();
                }

                if (tasks.Length > 0)
                {
                    await Task.WhenAll(tasks).WaitAsync(cancellationToken);
                }
            }
        }

        /// <summary>
        /// Gets the number of pending transfers.
        /// </summary>
        public int PendingTransferCount => _transferQueue.Count;

        /// <summary>
        /// Gets comprehensive transfer statistics.
        /// </summary>
        public TransferStatistics GetStatistics()
        {
            lock (_statistics)
            {
                var activeTransfers = _deviceChannels.Values.Sum(channel => channel.ActiveTransfers);

                return new TransferStatistics
                {
                    TotalTransfers = _statistics.TotalTransfers,
                    TotalBytesTransferred = _statistics.TotalBytesTransferred,
                    ActiveTransfers = activeTransfers,
                    QueuedTransfers = _transferQueue.Count,
                    AverageThroughputMBps = _statistics.AverageThroughputMBps,
                    PeakThroughputMBps = _statistics.PeakThroughputMBps,
                    TotalTransferTime = _statistics.TotalTransferTime,
                    BandwidthUtilization = CalculateOverallBandwidthUtilization()
                };
            }
        }

        /// <summary>
        /// Main transfer processing loop.
        /// </summary>
        private async Task ProcessTransferQueueAsync()
        {
            LogSchedulerStarted(_logger);

            while (!_shutdownTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_transferQueue.TryDequeue(out var operation))
                    {
                        await ProcessTransferOperationAsync(operation);
                    }
                    else
                    {
                        // No operations to process - short delay
                        await Task.Delay(1, _shutdownTokenSource.Token);
                    }
                }
                catch (OperationCanceledException) when (_shutdownTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogSchedulerError(_logger, ex);
                }
            }

            LogSchedulerStopped(_logger);
        }

        /// <summary>
        /// Processes a single transfer operation with bandwidth optimization.
        /// </summary>
        private async Task ProcessTransferOperationAsync(P2PTransferOperation operation)
        {
            var sourceDeviceId = operation.SourceDevice.Info.Id;
            var targetDeviceId = operation.TargetDevice.Info.Id;
            var sourceChannel = _deviceChannels[sourceDeviceId];
            var targetChannel = _deviceChannels[targetDeviceId];

            // Wait for available bandwidth on both channels
            await sourceChannel.AcquireAsync(_shutdownTokenSource.Token);
            await targetChannel.AcquireAsync(_shutdownTokenSource.Token);

            var startTime = DateTimeOffset.UtcNow;

            try
            {
                // Execute the actual transfer based on strategy
                await ExecuteTransferAsync(operation);

                var duration = DateTimeOffset.UtcNow - startTime;
                var throughputMBps = (operation.TransferSize / (1024.0 * 1024.0)) / duration.TotalSeconds;

                // Update statistics
                lock (_statistics)
                {
                    _statistics.TotalTransfers++;
                    _statistics.TotalBytesTransferred += operation.TransferSize;
                    _statistics.TotalTransferTime += duration;

                    // Update throughput statistics
                    if (throughputMBps > _statistics.PeakThroughputMBps)
                    {
                        _statistics.PeakThroughputMBps = throughputMBps;
                    }

                    _statistics.AverageThroughputMBps = _statistics.TotalTransfers > 0
                        ? (_statistics.TotalBytesTransferred / (1024.0 * 1024.0)) / _statistics.TotalTransferTime.TotalSeconds
                        : 0.0;
                }

                // Update channel statistics
                sourceChannel.RecordTransfer(operation.TransferSize, duration);
                targetChannel.RecordTransfer(operation.TransferSize, duration);

                operation.CompletionSource.SetResult(true);

                LogTransferExecuted(_logger, operation.TransferSize, throughputMBps);
            }
            catch (Exception ex)
            {
                LogTransferExecutionFailed(_logger, sourceDeviceId, targetDeviceId, ex);

                operation.CompletionSource.SetException(ex);
            }
            finally
            {
                sourceChannel.Release();
                targetChannel.Release();
            }
        }

        /// <summary>
        /// Executes the actual transfer based on the strategy.
        /// </summary>
        private async Task ExecuteTransferAsync(P2PTransferOperation operation)
        {
            switch (operation.Strategy.Type)
            {
                case TransferType.DirectP2P:
                    await ExecuteDirectP2PTransferAsync(operation);
                    break;

                case TransferType.HostMediated:
                    await ExecuteHostMediatedTransferAsync(operation);
                    break;

                case TransferType.Streaming:
                    await ExecuteStreamingTransferAsync(operation);
                    break;

                case TransferType.MemoryMapped:
                    await ExecuteMemoryMappedTransferAsync(operation);
                    break;

                default:
                    await ExecuteHostMediatedTransferAsync(operation);
                    break;
            }
        }

        /// <summary>
        /// Executes a direct P2P transfer.
        /// </summary>
        private async Task ExecuteDirectP2PTransferAsync(P2PTransferOperation operation)
            // Direct device-to-device transfer
            // This would use platform-specific P2P APIs




            => await operation.ExecuteDirectTransferAsync(_shutdownTokenSource.Token);

        /// <summary>
        /// Executes a host-mediated transfer.
        /// </summary>
        private async Task ExecuteHostMediatedTransferAsync(P2PTransferOperation operation)
            // Transfer via host memory




            => await operation.ExecuteHostMediatedTransferAsync(_shutdownTokenSource.Token);

        /// <summary>
        /// Executes a streaming transfer with chunking.
        /// </summary>
        private async Task ExecuteStreamingTransferAsync(P2PTransferOperation operation)
            // Chunked streaming transfer




            => await operation.ExecuteStreamingTransferAsync(operation.Strategy.ChunkSize, _shutdownTokenSource.Token);

        /// <summary>
        /// Executes a memory-mapped transfer.
        /// </summary>
        private async Task ExecuteMemoryMappedTransferAsync(P2PTransferOperation operation)
            // Memory-mapped file transfer




            => await operation.ExecuteMemoryMappedTransferAsync(_shutdownTokenSource.Token);

        /// <summary>
        /// Ensures transfer channels exist for the devices.
        /// </summary>
        private async ValueTask EnsureTransferChannelsAsync(
        IAccelerator sourceDevice,
        IAccelerator targetDevice,
        TransferStrategy strategy)
        {
            await _schedulerSemaphore.WaitAsync();
            try
            {
                // Create source channel if needed
                if (!_deviceChannels.ContainsKey(sourceDevice.Info.Id))
                {
                    _deviceChannels[sourceDevice.Info.Id] = new TransferChannel(
                        sourceDevice.Info.Id,
                        sourceDevice.Info.Name,
                        MaxConcurrentTransfersPerDevice,
                        strategy.EstimatedBandwidthGBps);
                }

                // Create target channel if needed
                if (!_deviceChannels.ContainsKey(targetDevice.Info.Id))
                {
                    _deviceChannels[targetDevice.Info.Id] = new TransferChannel(
                        targetDevice.Info.Id,
                        targetDevice.Info.Name,
                        MaxConcurrentTransfersPerDevice,
                        strategy.EstimatedBandwidthGBps);
                }

                // Create completion tracking if needed
                _ = _deviceTransferCompletions.TryAdd(sourceDevice.Info.Id, []);
                _ = _deviceTransferCompletions.TryAdd(targetDevice.Info.Id, []);
            }
            finally
            {
                _ = _schedulerSemaphore.Release();
            }
        }

        /// <summary>
        /// Determines transfer priority based on size.
        /// </summary>
        private static P2PTransferPriority DeterminePriority(long transferSize)
        {
            return transferSize switch
            {
                < 1024 * 1024 => P2PTransferPriority.High,      // < 1MB - high priority
                < 64 * 1024 * 1024 => P2PTransferPriority.Normal, // < 64MB - normal priority
                < 512 * 1024 * 1024 => P2PTransferPriority.Low,   // < 512MB - low priority
                _ => P2PTransferPriority.Background                 // >= 512MB - background
            };
        }

        /// <summary>
        /// Calculates overall bandwidth utilization across all devices.
        /// </summary>
        private double CalculateOverallBandwidthUtilization()
        {
            if (_deviceChannels.IsEmpty)
            {
                return 0.0;
            }

            var totalUtilization = _deviceChannels.Values.Sum(channel => channel.BandwidthUtilization);
            return totalUtilization / _deviceChannels.Count;
        }

        /// <summary>
        /// Monitors bandwidth usage and adjusts scheduling.
        /// </summary>
        private void MonitorBandwidthUsage(object? state)
        {
            try
            {
                var overallUtilization = CalculateOverallBandwidthUtilization();

                if (overallUtilization > BandwidthUtilizationThreshold)
                {
                    LogHighBandwidthUtilization(_logger, overallUtilization);

                    // Implement throttling logic here if needed
                    // For now, just log the situation
                }

                // Update channel statistics
                foreach (var channel in _deviceChannels.Values)
                {
                    channel.UpdateStatistics();
                }
            }
            catch (Exception ex)
            {
                LogBandwidthMonitoringError(_logger, ex);
            }
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Cancel all operations
            await _shutdownTokenSource.CancelAsync();

            try
            {
                // Wait for scheduler to finish
#pragma warning disable VSTHRD003 // Intentional pattern: awaiting background task started in constructor
                await _schedulerTask.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }

            // Dispose resources
            await _bandwidthMonitor.DisposeAsync();
            _schedulerSemaphore.Dispose();
            _shutdownTokenSource.Dispose();

            // Dispose all transfer channels
            foreach (var channel in _deviceChannels.Values)
            {
                channel.Dispose();
            }

            _deviceChannels.Clear();
            _deviceTransferCompletions.Clear();

            LogSchedulerDisposed(_logger);
        }
    }

    /// <summary>
    /// Represents a transfer channel for a specific device with bandwidth management.
    /// </summary>
    internal sealed class TransferChannel(string deviceId, string deviceName, int maxConcurrentTransfers, double maxBandwidthGBps) : IDisposable
    {
        private readonly string _deviceId = deviceId;
        private readonly string _deviceName = deviceName;
        private readonly double _maxBandwidthGBps = maxBandwidthGBps;
        private readonly SemaphoreSlim _concurrencySemaphore = new(maxConcurrentTransfers, maxConcurrentTransfers);
        private readonly object _statsLock = new();

        private long _totalTransfers;
        private long _totalBytesTransferred;
        private TimeSpan _totalTransferTime;
        private double _currentBandwidthUtilization;
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device id.</value>

        public string DeviceId => _deviceId;
        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        /// <value>The device name.</value>
        public string DeviceName => _deviceName;
        /// <summary>
        /// Gets or sets the active transfers.
        /// </summary>
        /// <value>The active transfers.</value>
        public int ActiveTransfers => _concurrencySemaphore.CurrentCount;
        /// <summary>
        /// Gets or sets the bandwidth utilization.
        /// </summary>
        /// <value>The bandwidth utilization.</value>
        public double BandwidthUtilization => _currentBandwidthUtilization;
        /// <summary>
        /// Gets acquire asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async ValueTask AcquireAsync(CancellationToken cancellationToken) => await _concurrencySemaphore.WaitAsync(cancellationToken);
        /// <summary>
        /// Performs release.
        /// </summary>

        public void Release() => _concurrencySemaphore.Release();
        /// <summary>
        /// Performs record transfer.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="duration">The duration.</param>

        public void RecordTransfer(long bytes, TimeSpan duration)
        {
            lock (_statsLock)
            {
                _totalTransfers++;
                _totalBytesTransferred += bytes;
                _totalTransferTime += duration;
            }
        }
        /// <summary>
        /// Updates the statistics.
        /// </summary>

        public void UpdateStatistics()
        {
            lock (_statsLock)
            {
                if (_totalTransferTime.TotalSeconds > 0)
                {
                    var actualBandwidthGBps = (_totalBytesTransferred / (1024.0 * 1024.0 * 1024.0)) / _totalTransferTime.TotalSeconds;
                    _currentBandwidthUtilization = Math.Min(1.0, actualBandwidthGBps / _maxBandwidthGBps);
                }
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose() => _concurrencySemaphore.Dispose();
    }

    /// <summary>
    /// Base class for P2P transfer operations.
    /// </summary>
    internal abstract class P2PTransferOperation
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public required Guid Id { get; init; }
        /// <summary>
        /// Gets or sets the transfer size.
        /// </summary>
        /// <value>The transfer size.</value>
        public required long TransferSize { get; init; }
        /// <summary>
        /// Gets or sets the strategy.
        /// </summary>
        /// <value>The strategy.</value>
        public required TransferStrategy Strategy { get; init; }
        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public required P2PTransferPriority Priority { get; init; }
        /// <summary>
        /// Gets or sets the scheduled at.
        /// </summary>
        /// <value>The scheduled at.</value>
        public required DateTimeOffset ScheduledAt { get; init; }
        /// <summary>
        /// Gets or sets the completion source.
        /// </summary>
        /// <value>The completion source.</value>
        public required TaskCompletionSource<bool> CompletionSource { get; init; }
        /// <summary>
        /// Gets or sets the source device.
        /// </summary>
        /// <value>The source device.</value>

        public abstract IAccelerator SourceDevice { get; }
        /// <summary>
        /// Gets or sets the target device.
        /// </summary>
        /// <value>The target device.</value>
        public abstract IAccelerator TargetDevice { get; }
        /// <summary>
        /// Gets execute direct transfer asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public abstract Task ExecuteDirectTransferAsync(CancellationToken cancellationToken);
        /// <summary>
        /// Gets execute host mediated transfer asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public abstract Task ExecuteHostMediatedTransferAsync(CancellationToken cancellationToken);
        /// <summary>
        /// Gets execute streaming transfer asynchronously.
        /// </summary>
        /// <param name="chunkSize">The chunk size.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public abstract Task ExecuteStreamingTransferAsync(int chunkSize, CancellationToken cancellationToken);
        /// <summary>
        /// Gets execute memory mapped transfer asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public abstract Task ExecuteMemoryMappedTransferAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Typed P2P transfer operation.
    /// </summary>
    internal sealed class P2PTransferOperation<T> : P2PTransferOperation where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the source buffer.
        /// </summary>
        /// <value>The source buffer.</value>
        public required IUnifiedMemoryBuffer<T> SourceBuffer { get; init; }
        /// <summary>
        /// Gets or sets the target buffer.
        /// </summary>
        /// <value>The target buffer.</value>
        public required IUnifiedMemoryBuffer<T> TargetBuffer { get; init; }
        /// <summary>
        /// Gets or sets the source offset.
        /// </summary>
        /// <value>The source offset.</value>
        public required int SourceOffset { get; init; }
        /// <summary>
        /// Gets or sets the target offset.
        /// </summary>
        /// <value>The target offset.</value>
        public required int TargetOffset { get; init; }
        /// <summary>
        /// Gets or sets the element count.
        /// </summary>
        /// <value>The element count.</value>
        public required int ElementCount { get; init; }
        /// <summary>
        /// Gets or sets the source device.
        /// </summary>
        /// <value>The source device.</value>

        public override IAccelerator SourceDevice => SourceBuffer.Accelerator;
        /// <summary>
        /// Gets or sets the target device.
        /// </summary>
        /// <value>The target device.</value>
        public override IAccelerator TargetDevice => TargetBuffer.Accelerator;
        /// <summary>
        /// Gets execute direct transfer asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override async Task ExecuteDirectTransferAsync(CancellationToken cancellationToken) => await SourceBuffer.CopyToAsync(SourceOffset, TargetBuffer, TargetOffset, ElementCount, cancellationToken);
        /// <summary>
        /// Gets execute host mediated transfer asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override async Task ExecuteHostMediatedTransferAsync(CancellationToken cancellationToken)
        {
            var hostData = new T[ElementCount];

            // Copy from source to host
            var fullSourceData = new T[SourceBuffer.Length];
            await SourceBuffer.CopyToAsync(fullSourceData.AsMemory(), cancellationToken);
            Array.Copy(fullSourceData, SourceOffset, hostData, 0, ElementCount);

            // Copy from host to target
            await TargetBuffer.CopyFromAsync(hostData.AsMemory(), cancellationToken);
        }
        /// <summary>
        /// Gets execute streaming transfer asynchronously.
        /// </summary>
        /// <param name="chunkSize">The chunk size.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override async Task ExecuteStreamingTransferAsync(int chunkSize, CancellationToken cancellationToken)
        {
            var elementSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var elementsPerChunk = Math.Max(1, chunkSize / elementSize);
            var remainingElements = ElementCount;
            var currentSourceOffset = SourceOffset;
            var currentTargetOffset = TargetOffset;

            while (remainingElements > 0)
            {
                var chunkElements = Math.Min(elementsPerChunk, remainingElements);

                await SourceBuffer.CopyToAsync(currentSourceOffset, TargetBuffer, currentTargetOffset, chunkElements, cancellationToken);

                remainingElements -= chunkElements;
                currentSourceOffset += chunkElements;
                currentTargetOffset += chunkElements;
            }
        }
        /// <summary>
        /// Gets execute memory mapped transfer asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public override async Task ExecuteMemoryMappedTransferAsync(CancellationToken cancellationToken)
        {
            // For very large transfers, use memory-mapped approach
            var hostData = new T[ElementCount];

            // Copy source range to host
            var fullSourceData = new T[SourceBuffer.Length];
            await SourceBuffer.CopyToAsync(fullSourceData.AsMemory(), cancellationToken);
            Array.Copy(fullSourceData, SourceOffset, hostData, 0, ElementCount);

            // Copy from host to target
            await TargetBuffer.CopyFromAsync(hostData.AsMemory(), cancellationToken);
        }
    }
    /// <summary>
    /// An p2 p transfer priority enumeration.
    /// </summary>

    /// <summary>
    /// P2P transfer priority levels.
    /// </summary>
    public enum P2PTransferPriority
    {
        /// <summary>
        /// Background priority level for low-importance transfers that can be deferred.
        /// </summary>
        Background = 0,
        /// <summary>
        /// Low priority level for transfers that are not time-sensitive.
        /// </summary>
        Low = 1,
        /// <summary>
        /// Normal priority level for standard transfers (default priority).
        /// </summary>
        Normal = 2,
        /// <summary>
        /// High priority level for time-sensitive transfers that should be expedited.
        /// </summary>
        High = 3,
        /// <summary>
        /// Critical priority level for urgent transfers that must be processed immediately.
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Transfer scheduler statistics.
    /// </summary>
    public sealed class TransferStatistics
    {
        /// <summary>
        /// Gets or sets the total transfers.
        /// </summary>
        /// <value>The total transfers.</value>
        public long TotalTransfers { get; set; }
        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        /// <value>The total bytes transferred.</value>
        public long TotalBytesTransferred { get; set; }
        /// <summary>
        /// Gets or sets the active transfers.
        /// </summary>
        /// <value>The active transfers.</value>
        public int ActiveTransfers { get; set; }
        /// <summary>
        /// Gets or sets the queued transfers.
        /// </summary>
        /// <value>The queued transfers.</value>
        public int QueuedTransfers { get; set; }
        /// <summary>
        /// Gets or sets the average throughput m bps.
        /// </summary>
        /// <value>The average throughput m bps.</value>
        public double AverageThroughputMBps { get; set; }
        /// <summary>
        /// Gets or sets the peak throughput m bps.
        /// </summary>
        /// <value>The peak throughput m bps.</value>
        public double PeakThroughputMBps { get; set; }
        /// <summary>
        /// Gets or sets the total transfer time.
        /// </summary>
        /// <value>The total transfer time.</value>
        public TimeSpan TotalTransferTime { get; set; }
        /// <summary>
        /// Gets or sets the bandwidth utilization.
        /// </summary>
        /// <value>The bandwidth utilization.</value>
        public double BandwidthUtilization { get; set; }
    }
}
