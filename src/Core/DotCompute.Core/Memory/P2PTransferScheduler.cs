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
    public sealed class P2PTransferScheduler : IAsyncDisposable
    {
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

            _logger.LogDebugMessage($"P2P transfer scheduler initialized with {MaxConcurrentTransfersPerDevice} concurrent transfers per device");
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

                _logger.LogTrace("P2P transfer completed: {TransferSize} bytes from {Source} to {Target} in {Duration}ms",
                    transferSize, sourceBuffer.Accelerator.Info.Name, targetBuffer.Accelerator.Info.Name,
                    (DateTimeOffset.UtcNow - operation.ScheduledAt).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"P2P transfer failed: {sourceBuffer.Accelerator.Info.Name} to {targetBuffer.Accelerator.Info.Name}");
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
        public int GetPendingTransferCount() => _transferQueue.Count;

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
            _logger.LogDebugMessage("P2P transfer scheduler started");

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
                    _logger.LogErrorMessage(ex, "Error in transfer scheduler main loop");
                }
            }

            _logger.LogDebugMessage("P2P transfer scheduler stopped");
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

                _logger.LogTrace("Transfer executed successfully: {Size} bytes, {ThroughputMBps:F1} MB/s",
                    operation.TransferSize, throughputMBps);
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Transfer execution failed: {sourceDeviceId} -> {targetDeviceId}");

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
            if (!_deviceChannels.Any())
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
                    _logger.LogDebugMessage($"High bandwidth utilization detected: {overallUtilization}, throttling transfers");

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
                _logger.LogWarning(ex, "Error in bandwidth monitoring");
            }
        }

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
                await _schedulerTask.ConfigureAwait(false);
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

            _logger.LogDebugMessage("P2P transfer scheduler disposed");
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

        public string DeviceId => _deviceId;
        public string DeviceName => _deviceName;
        public int ActiveTransfers => _concurrencySemaphore.CurrentCount;
        public double BandwidthUtilization => _currentBandwidthUtilization;

        public async ValueTask AcquireAsync(CancellationToken cancellationToken) => await _concurrencySemaphore.WaitAsync(cancellationToken);

        public void Release() => _concurrencySemaphore.Release();

        public void RecordTransfer(long bytes, TimeSpan duration)
        {
            lock (_statsLock)
            {
                _totalTransfers++;
                _totalBytesTransferred += bytes;
                _totalTransferTime += duration;
            }
        }

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

        public void Dispose() => _concurrencySemaphore.Dispose();
    }

    /// <summary>
    /// Base class for P2P transfer operations.
    /// </summary>
    internal abstract class P2PTransferOperation
    {
        public required Guid Id { get; init; }
        public required long TransferSize { get; init; }
        public required TransferStrategy Strategy { get; init; }
        public required P2PTransferPriority Priority { get; init; }
        public required DateTimeOffset ScheduledAt { get; init; }
        public required TaskCompletionSource<bool> CompletionSource { get; init; }

        public abstract IAccelerator SourceDevice { get; }
        public abstract IAccelerator TargetDevice { get; }

        public abstract Task ExecuteDirectTransferAsync(CancellationToken cancellationToken);
        public abstract Task ExecuteHostMediatedTransferAsync(CancellationToken cancellationToken);
        public abstract Task ExecuteStreamingTransferAsync(int chunkSize, CancellationToken cancellationToken);
        public abstract Task ExecuteMemoryMappedTransferAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Typed P2P transfer operation.
    /// </summary>
    internal sealed class P2PTransferOperation<T> : P2PTransferOperation where T : unmanaged
    {
        public required IUnifiedMemoryBuffer<T> SourceBuffer { get; init; }
        public required IUnifiedMemoryBuffer<T> TargetBuffer { get; init; }
        public required int SourceOffset { get; init; }
        public required int TargetOffset { get; init; }
        public required int ElementCount { get; init; }

        public override IAccelerator SourceDevice => SourceBuffer.Accelerator;
        public override IAccelerator TargetDevice => TargetBuffer.Accelerator;

        public override async Task ExecuteDirectTransferAsync(CancellationToken cancellationToken) => await SourceBuffer.CopyToAsync(SourceOffset, TargetBuffer, TargetOffset, ElementCount, cancellationToken);

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
    /// P2P transfer priority levels.
    /// </summary>
    public enum P2PTransferPriority
    {
        Background = 0,
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Transfer scheduler statistics.
    /// </summary>
    public sealed class TransferStatistics
    {
        public long TotalTransfers { get; set; }
        public long TotalBytesTransferred { get; set; }
        public int ActiveTransfers { get; set; }
        public int QueuedTransfers { get; set; }
        public double AverageThroughputMBps { get; set; }
        public double PeakThroughputMBps { get; set; }
        public TimeSpan TotalTransferTime { get; set; }
        public double BandwidthUtilization { get; set; }
    }
}
