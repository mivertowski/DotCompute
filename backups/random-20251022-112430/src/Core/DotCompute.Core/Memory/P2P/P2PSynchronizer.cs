// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Advanced P2P Synchronizer that provides cross-device synchronization primitives,
    /// barriers, and memory consistency guarantees for multi-GPU P2P operations.
    /// </summary>
    public sealed partial class P2PSynchronizer : IAsyncDisposable
    {
        // LoggerMessage delegates - Event ID range 14200-14299 for P2PSynchronizer (Memory/P2P module)
        private static readonly Action<ILogger, int, int, Exception?> _logSyncMonitor =
            LoggerMessage.Define<int, int>(
                MsLogLevel.Trace,
                new EventId(14200, nameof(LogSyncMonitor)),
                "Sync monitor: {ActiveBarriers} active barriers, {ActiveEvents} active events with waiters");

        private static readonly Action<ILogger, Exception?> _logMonitoringError =
            LoggerMessage.Define(
                MsLogLevel.Warning,
                new EventId(14201, nameof(LogMonitoringError)),
                "Error in synchronization health monitoring");

        private static readonly Action<ILogger, string, string, int, int, Exception?> _logBarrierArrival =
            LoggerMessage.Define<string, string, int, int>(
                MsLogLevel.Trace,
                new EventId(14202, nameof(LogBarrierArrival)),
                "Device {DeviceId} arrived at barrier {BarrierId} ({Arrived}/{Expected})");

        // Wrapper methods
        private static void LogSyncMonitor(ILogger logger, int activeBarriers, int activeEvents)
            => _logSyncMonitor(logger, activeBarriers, activeEvents, null);

        private static void LogMonitoringError(ILogger logger, Exception ex)
            => _logMonitoringError(logger, ex);

        private static void LogBarrierArrival(ILogger logger, string deviceId, string barrierId, int arrived, int expected)
            => _logBarrierArrival(logger, deviceId, barrierId, arrived, expected, null);

        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, P2PSyncBarrier> _barriers;
        private readonly ConcurrentDictionary<string, P2PSyncEvent> _syncEvents;
        private readonly ConcurrentDictionary<string, P2PAtomicCounter> _atomicCounters;
        private readonly ConcurrentDictionary<string, P2PDeviceSyncState> _deviceSyncStates;
        private readonly SemaphoreSlim _synchronizerSemaphore;
        private readonly Timer? _syncMonitorTimer;
        private readonly P2PSyncStatistics _statistics;
        private bool _disposed;
        private const int SyncMonitorIntervalMs = 5000; // 5 seconds
        private const int MaxConcurrentSyncOperations = 32;
        private const int DefaultBarrierTimeoutMs = 10000; // 10 seconds
        /// <summary>
        /// Initializes a new instance of the P2PSynchronizer class.
        /// </summary>
        /// <param name="logger">The logger.</param>

        public P2PSynchronizer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _barriers = new ConcurrentDictionary<string, P2PSyncBarrier>();
            _syncEvents = new ConcurrentDictionary<string, P2PSyncEvent>();
            _atomicCounters = new ConcurrentDictionary<string, P2PAtomicCounter>();
            _deviceSyncStates = new ConcurrentDictionary<string, P2PDeviceSyncState>();
            _synchronizerSemaphore = new SemaphoreSlim(MaxConcurrentSyncOperations, MaxConcurrentSyncOperations);
            _statistics = new P2PSyncStatistics();

            // Start synchronization monitoring
            _syncMonitorTimer = new Timer(MonitorSynchronizationHealth, null,
                TimeSpan.FromMilliseconds(SyncMonitorIntervalMs),
                TimeSpan.FromMilliseconds(SyncMonitorIntervalMs));

            _logger.LogDebugMessage("P2P Synchronizer initialized with {MaxConcurrentSyncOperations} concurrent operations");
        }

        /// <summary>
        /// Initializes synchronization support for the provided devices.
        /// </summary>
        public async Task InitializeDevicesAsync(
            IAccelerator[] devices,
            CancellationToken cancellationToken = default)
        {
            if (devices == null || devices.Length == 0)
            {

                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }


            _logger.LogInfoMessage("Initializing P2P synchronization for {devices.Length} devices");

            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                foreach (var device in devices)
                {
                    var syncState = new P2PDeviceSyncState
                    {
                        DeviceId = device.Info.Id,
                        DeviceName = device.Info.Name,
                        Device = device,
                        IsInitialized = true,
                        ActiveBarriers = [],
                        ActiveEvents = [],
                        LastSyncOperation = DateTimeOffset.UtcNow
                    };

                    _deviceSyncStates[device.Info.Id] = syncState;
                }

                _logger.LogInfoMessage("P2P synchronization initialized for {devices.Length} devices");
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Establishes a P2P transfer barrier between two devices for synchronized operations.
        /// </summary>
        public async Task EstablishTransferBarrierAsync(
            IAccelerator sourceDevice,
            IAccelerator targetDevice,
            string transferId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sourceDevice);


            ArgumentNullException.ThrowIfNull(targetDevice);


            if (string.IsNullOrEmpty(transferId))
            {
                throw new ArgumentException("Transfer ID cannot be empty", nameof(transferId));
            }


            var barrierId = $"transfer_barrier_{transferId}";

            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                var barrier = new P2PSyncBarrier
                {
                    BarrierId = barrierId,
                    ParticipantDevices = [sourceDevice.Info.Id, targetDevice.Info.Id],
                    ExpectedParticipants = 2,
                    ArrivedParticipants = 0,
                    BarrierState = P2PBarrierState.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    TimeoutMs = DefaultBarrierTimeoutMs,
                    CompletionSource = new TaskCompletionSource<bool>()
                };

                _barriers[barrierId] = barrier;

                // Update device sync states
                if (_deviceSyncStates.TryGetValue(sourceDevice.Info.Id, out var sourceState))
                {
                    sourceState.ActiveBarriers.Add(barrierId);
                }
                if (_deviceSyncStates.TryGetValue(targetDevice.Info.Id, out var targetState))
                {
                    targetState.ActiveBarriers.Add(barrierId);
                }

                // Set up automatic timeout
                _ = Task.Delay(barrier.TimeoutMs, cancellationToken).ContinueWith(async _ =>
                {
                    if (barrier.BarrierState == P2PBarrierState.Active)
                    {
                        await TimeoutBarrierAsync(barrierId);
                    }
                }, TaskScheduler.Default);

                _logger.LogDebugMessage($"Transfer barrier established: {barrierId} between {sourceDevice.Info.Name} and {targetDevice.Info.Name}");

                lock (_statistics)
                {
                    _statistics.BarriersCreated++;
                    _statistics.ActiveBarriers++;
                }
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Releases a P2P transfer barrier after completion.
        /// </summary>
        public async Task ReleaseTransferBarrierAsync(
            string transferId,
            CancellationToken cancellationToken = default)
        {
            var barrierId = $"transfer_barrier_{transferId}";

            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_barriers.TryRemove(barrierId, out var barrier))
                {
                    barrier.BarrierState = P2PBarrierState.Completed;
                    _ = barrier.CompletionSource.TrySetResult(true);

                    // Update device sync states
                    foreach (var deviceId in barrier.ParticipantDevices)
                    {
                        if (_deviceSyncStates.TryGetValue(deviceId, out var deviceState))
                        {
                            _ = deviceState.ActiveBarriers.Remove(barrierId);
                            deviceState.LastSyncOperation = DateTimeOffset.UtcNow;
                        }
                    }

                    lock (_statistics)
                    {
                        _statistics.BarriersCompleted++;
                        _statistics.ActiveBarriers--;
                        var duration = DateTimeOffset.UtcNow - barrier.CreatedAt;
                        _statistics.TotalBarrierTime += duration;
                    }

                    _logger.LogDebugMessage("Transfer barrier released: {barrierId}");
                }
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Creates a multi-device synchronization barrier for coordinated operations.
        /// </summary>
        public async Task<P2PMultiDeviceBarrier> CreateMultiDeviceBarrierAsync(
            IAccelerator[] devices,
            string barrierId,
            int timeoutMs = DefaultBarrierTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            if (devices == null || devices.Length == 0)
            {

                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }


            if (string.IsNullOrEmpty(barrierId))
            {

                throw new ArgumentException("Barrier ID cannot be empty", nameof(barrierId));
            }


            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                var deviceIds = devices.Select(d => d.Info.Id).ToList();
                var barrier = new P2PSyncBarrier
                {
                    BarrierId = barrierId,
                    ParticipantDevices = deviceIds,
                    ExpectedParticipants = devices.Length,
                    ArrivedParticipants = 0,
                    BarrierState = P2PBarrierState.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    TimeoutMs = timeoutMs,
                    CompletionSource = new TaskCompletionSource<bool>()
                };

                _barriers[barrierId] = barrier;

                // Update device sync states
                foreach (var deviceId in deviceIds)
                {
                    if (_deviceSyncStates.TryGetValue(deviceId, out var deviceState))
                    {
                        deviceState.ActiveBarriers.Add(barrierId);
                    }
                }

                // Set up timeout
                _ = Task.Delay(timeoutMs, cancellationToken).ContinueWith(async _ =>
                {
                    if (barrier.BarrierState == P2PBarrierState.Active)
                    {
                        await TimeoutBarrierAsync(barrierId);
                    }
                }, TaskScheduler.Default);

                var multiBarrier = new P2PMultiDeviceBarrier
                {
                    BarrierId = barrierId,
                    Devices = devices,
                    IsActive = true,
                    CompletionTask = barrier.CompletionSource.Task
                };

                _logger.LogDebugMessage($"Multi-device barrier created: {barrierId} with {devices.Length} devices");

                lock (_statistics)
                {
                    _statistics.BarriersCreated++;
                    _statistics.ActiveBarriers++;
                }

                return multiBarrier;
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Signals arrival at a barrier for a specific device.
        /// </summary>
        public async Task<bool> ArriveAtBarrierAsync(
            string barrierId,
            string deviceId,
            CancellationToken cancellationToken = default)
        {
            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!_barriers.TryGetValue(barrierId, out var barrier))
                {
                    _logger.LogWarningMessage("Barrier not found: {barrierId}");
                    return false;
                }

                if (barrier.BarrierState != P2PBarrierState.Active)
                {
                    _logger.LogWarningMessage("Barrier {BarrierId} is not active (state: {barrierId, barrier.BarrierState})");
                    return false;
                }

                if (!barrier.ParticipantDevices.Contains(deviceId))
                {
                    _logger.LogWarningMessage("Device {DeviceId} is not a participant in barrier {deviceId, barrierId}");
                    return false;
                }

                barrier.ArrivedParticipants++;


                LogBarrierArrival(_logger, deviceId, barrierId, barrier.ArrivedParticipants, barrier.ExpectedParticipants);

                // Check if all participants have arrived
                if (barrier.ArrivedParticipants >= barrier.ExpectedParticipants)
                {
                    barrier.BarrierState = P2PBarrierState.Completed;
                    _ = barrier.CompletionSource.TrySetResult(true);

                    _logger.LogDebugMessage("Barrier {barrierId} completed - all participants arrived");

                    lock (_statistics)
                    {
                        _statistics.BarriersCompleted++;
                        _statistics.ActiveBarriers--;
                        var duration = DateTimeOffset.UtcNow - barrier.CreatedAt;
                        _statistics.TotalBarrierTime += duration;
                    }

                    return true;
                }

                return false; // Barrier not yet complete
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Waits for a barrier to complete with timeout support.
        /// </summary>
        public async Task<bool> WaitForBarrierAsync(
            string barrierId,
            CancellationToken cancellationToken = default)
        {
            if (!_barriers.TryGetValue(barrierId, out var barrier))
            {
                _logger.LogWarningMessage("Barrier not found: {barrierId}");
                return false;
            }

            try
            {
                using var timeoutCts = new CancellationTokenSource(barrier.TimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                _ = await barrier.CompletionSource.Task.WaitAsync(combinedCts.Token);
                return barrier.BarrierState == P2PBarrierState.Completed;
            }
            catch (OperationCanceledException)
            {
                if (barrier.BarrierState == P2PBarrierState.Active)
                {
                    await TimeoutBarrierAsync(barrierId);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error waiting for barrier {barrierId}");
                return false;
            }
        }

        /// <summary>
        /// Creates a cross-device synchronization event for signaling.
        /// </summary>
        public async Task<P2PSyncEvent> CreateSyncEventAsync(
            string eventId,
            IAccelerator[] devices,
            P2PSyncEventType eventType = P2PSyncEventType.AutoReset,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(eventId))
            {

                throw new ArgumentException("Event ID cannot be empty", nameof(eventId));
            }


            if (devices == null || devices.Length == 0)
            {

                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }


            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                var syncEvent = new P2PSyncEvent
                {
                    EventId = eventId,
                    ParticipantDevices = [.. devices.Select(d => d.Info.Id)],
                    EventType = eventType,
                    IsSignaled = false,
                    WaitingDevices = new ConcurrentDictionary<string, TaskCompletionSource<bool>>(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    SignalCount = 0
                };

                _syncEvents[eventId] = syncEvent;

                // Update device sync states
                foreach (var device in devices)
                {
                    if (_deviceSyncStates.TryGetValue(device.Info.Id, out var deviceState))
                    {
                        deviceState.ActiveEvents.Add(eventId);
                    }
                }

                _logger.LogDebugMessage($"Sync event created: {eventId} with {devices.Length} devices, type: {eventType}");

                lock (_statistics)
                {
                    _statistics.EventsCreated++;
                    _statistics.ActiveEvents++;
                }

                return syncEvent;
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Signals a cross-device synchronization event.
        /// </summary>
        public async Task<bool> SignalEventAsync(
            string eventId,
            CancellationToken cancellationToken = default)
        {
            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!_syncEvents.TryGetValue(eventId, out var syncEvent))
                {
                    _logger.LogWarningMessage("Sync event not found: {eventId}");
                    return false;
                }

                syncEvent.IsSignaled = true;
                syncEvent.SignalCount++;
                syncEvent.LastSignaledAt = DateTimeOffset.UtcNow;

                // Wake up all waiting devices
                var waitingTasks = syncEvent.WaitingDevices.Values.ToList();
                foreach (var tcs in waitingTasks)
                {
                    _ = tcs.TrySetResult(true);
                }

                if (syncEvent.EventType == P2PSyncEventType.AutoReset)
                {
                    // Auto-reset: clear signal after waking waiters
                    syncEvent.IsSignaled = false;
                    syncEvent.WaitingDevices.Clear();
                }

                _logger.LogDebugMessage($"Sync event signaled: {eventId}, woke up {waitingTasks.Count} devices");

                lock (_statistics)
                {
                    _statistics.EventsSignaled++;
                }

                return true;
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Waits for a cross-device synchronization event to be signaled.
        /// </summary>
        public async Task<bool> WaitForEventAsync(
            string eventId,
            string deviceId,
            int timeoutMs = DefaultBarrierTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            if (!_syncEvents.TryGetValue(eventId, out var syncEvent))
            {
                _logger.LogWarningMessage("Sync event not found: {eventId}");
                return false;
            }

            if (syncEvent.EventType == P2PSyncEventType.ManualReset && syncEvent.IsSignaled)
            {
                // Manual reset event is already signaled
                return true;
            }

            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            TaskCompletionSource<bool> waitTcs;
            try
            {
                if (syncEvent.EventType == P2PSyncEventType.ManualReset && syncEvent.IsSignaled)
                {
                    return true;
                }

                waitTcs = new TaskCompletionSource<bool>();
                syncEvent.WaitingDevices[deviceId] = waitTcs;
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }

            try
            {
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                _ = await waitTcs.Task.WaitAsync(combinedCts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                // Remove from waiting list on timeout/cancellation
                _ = syncEvent.WaitingDevices.TryRemove(deviceId, out _);
                return false;
            }
        }

        /// <summary>
        /// Creates an atomic counter for cross-device coordination.
        /// </summary>
        public async Task<P2PAtomicCounter> CreateAtomicCounterAsync(
            string counterId,
            long initialValue = 0,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(counterId))
            {

                throw new ArgumentException("Counter ID cannot be empty", nameof(counterId));
            }


            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                var counter = new P2PAtomicCounter
                {
                    CounterId = counterId,
                    Value = initialValue,
                    CreatedAt = DateTimeOffset.UtcNow,
                    OperationCount = 0
                };

                _atomicCounters[counterId] = counter;

                _logger.LogDebugMessage($"Atomic counter created: {counterId} with initial value {initialValue}");

                lock (_statistics)
                {
                    _statistics.AtomicCountersCreated++;
                    _statistics.ActiveAtomicCounters++;
                }

                return counter;
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        /// <summary>
        /// Atomically increments a counter and returns the new value.
        /// </summary>
        public async Task<long> IncrementAtomicCounterAsync(
            string counterId,
            long increment = 1,
            CancellationToken cancellationToken = default)
        {
            await _synchronizerSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!_atomicCounters.TryGetValue(counterId, out var counter))
                {
                    _logger.LogWarningMessage("Atomic counter not found: {counterId}");
                    return -1;
                }

                counter.Value += increment;
                counter.OperationCount++;
                counter.LastModifiedAt = DateTimeOffset.UtcNow;

                lock (_statistics)
                {
                    _statistics.AtomicOperations++;
                }

                return counter.Value;
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }


        /// <summary>
        /// Gets comprehensive synchronization statistics.
        /// </summary>
#pragma warning disable CA1024 // Use properties where appropriate - Method creates new object with calculations
        public P2PSyncStatistics GetSynchronizationStatistics()
#pragma warning restore CA1024
        {
            lock (_statistics)
            {
                var averageBarrierTime = _statistics.BarriersCompleted > 0
                    ? _statistics.TotalBarrierTime / _statistics.BarriersCompleted
                    : TimeSpan.Zero;

                return new P2PSyncStatistics
                {
                    BarriersCreated = _statistics.BarriersCreated,
                    BarriersCompleted = _statistics.BarriersCompleted,
                    BarriersTimedOut = _statistics.BarriersTimedOut,
                    ActiveBarriers = _statistics.ActiveBarriers,
                    EventsCreated = _statistics.EventsCreated,
                    EventsSignaled = _statistics.EventsSignaled,
                    ActiveEvents = _statistics.ActiveEvents,
                    AtomicCountersCreated = _statistics.AtomicCountersCreated,
                    ActiveAtomicCounters = _statistics.ActiveAtomicCounters,
                    AtomicOperations = _statistics.AtomicOperations,
                    TotalBarrierTime = _statistics.TotalBarrierTime,
                    AverageBarrierTime = averageBarrierTime,
                    InitializedDevices = _deviceSyncStates.Count
                };
            }
        }

        /// <summary>
        /// Gets active synchronization state for all devices.
        /// </summary>
        public IReadOnlyList<P2PDeviceSyncState> GetDeviceSyncStates() => _deviceSyncStates.Values.ToList();

        #region Private Implementation

        private async Task TimeoutBarrierAsync(string barrierId)
        {
            await _synchronizerSemaphore.WaitAsync();
            try
            {
                if (_barriers.TryGetValue(barrierId, out var barrier) && barrier.BarrierState == P2PBarrierState.Active)
                {
                    barrier.BarrierState = P2PBarrierState.TimedOut;
                    _ = barrier.CompletionSource.TrySetException(new TimeoutException($"Barrier {barrierId} timed out"));

                    // Update device sync states
                    foreach (var deviceId in barrier.ParticipantDevices)
                    {
                        if (_deviceSyncStates.TryGetValue(deviceId, out var deviceState))
                        {
                            _ = deviceState.ActiveBarriers.Remove(barrierId);
                        }
                    }

                    lock (_statistics)
                    {
                        _statistics.BarriersTimedOut++;
                        _statistics.ActiveBarriers--;
                    }

                    _logger.LogWarningMessage("Barrier timed out: {BarrierId} after {barrierId, barrier.TimeoutMs}ms");
                }
            }
            finally
            {
                _ = _synchronizerSemaphore.Release();
            }
        }

        private void MonitorSynchronizationHealth(object? state)
        {
            try
            {
                var activeBarrierCount = _barriers.Values.Count(b => b.BarrierState == P2PBarrierState.Active);
                var activeEventCount = _syncEvents.Values.Count(e => !e.WaitingDevices.IsEmpty);

                if (activeBarrierCount > 0 || activeEventCount > 0)
                {
                    LogSyncMonitor(_logger, activeBarrierCount, activeEventCount);
                }

                // Check for stuck barriers (created more than 2x timeout ago)
                var stuckBarriers = _barriers.Values
                    .Where(b => b.BarrierState == P2PBarrierState.Active)
                    .Where(b => DateTimeOffset.UtcNow - b.CreatedAt > TimeSpan.FromMilliseconds(b.TimeoutMs * 2))
                    .ToList();

                foreach (var stuckBarrier in stuckBarriers)
                {
                    _logger.LogWarningMessage($"Stuck barrier detected: {stuckBarrier.BarrierId}, created {stuckBarrier.CreatedAt}, {stuckBarrier.ArrivedParticipants}/{stuckBarrier.ExpectedParticipants} participants");
                }
            }
            catch (Exception ex)
            {
                LogMonitoringError(_logger, ex);
            }
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        #endregion

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }


            _disposed = true;

            _syncMonitorTimer?.Dispose();

            // Complete all active barriers and events
            _ = new List<Task>();

            foreach (var barrier in _barriers.Values)
            {
                if (barrier.BarrierState == P2PBarrierState.Active)
                {
                    barrier.BarrierState = P2PBarrierState.Cancelled;
                    _ = barrier.CompletionSource.TrySetCanceled();
                }
            }

            foreach (var syncEvent in _syncEvents.Values)
            {
                foreach (var tcs in syncEvent.WaitingDevices.Values)
                {
                    _ = tcs.TrySetCanceled();
                }
            }

            _barriers.Clear();
            _syncEvents.Clear();
            _atomicCounters.Clear();
            _deviceSyncStates.Clear();
            _synchronizerSemaphore.Dispose();

            _logger.LogDebugMessage("P2P Synchronizer disposed");


            return ValueTask.CompletedTask;
        }
    }

    #region Supporting Types

    /// <summary>
    /// P2P synchronization barrier implementation.
    /// </summary>
    internal sealed class P2PSyncBarrier
    {
        /// <summary>
        /// Gets or sets the barrier identifier.
        /// </summary>
        /// <value>The barrier id.</value>
        public required string BarrierId { get; init; }
        /// <summary>
        /// Gets or sets the participant devices.
        /// </summary>
        /// <value>The participant devices.</value>
        public required IList<string> ParticipantDevices { get; init; }
        /// <summary>
        /// Gets or sets the expected participants.
        /// </summary>
        /// <value>The expected participants.</value>
        public required int ExpectedParticipants { get; init; }
        /// <summary>
        /// Gets or sets the arrived participants.
        /// </summary>
        /// <value>The arrived participants.</value>
        public int ArrivedParticipants { get; set; }
        /// <summary>
        /// Gets or sets the barrier state.
        /// </summary>
        /// <value>The barrier state.</value>
        public P2PBarrierState BarrierState { get; set; }
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>The created at.</value>
        public required DateTimeOffset CreatedAt { get; init; }
        /// <summary>
        /// Gets or sets the timeout ms.
        /// </summary>
        /// <value>The timeout ms.</value>
        public required int TimeoutMs { get; init; }
        /// <summary>
        /// Gets or sets the completion source.
        /// </summary>
        /// <value>The completion source.</value>
        public required TaskCompletionSource<bool> CompletionSource { get; init; }
    }

    /// <summary>
    /// Multi-device P2P synchronization barrier.
    /// </summary>
    public sealed class P2PMultiDeviceBarrier
    {
        /// <summary>
        /// Gets or sets the barrier identifier.
        /// </summary>
        /// <value>The barrier id.</value>
        public required string BarrierId { get; init; }
        /// <summary>
        /// Gets or sets the devices.
        /// </summary>
        /// <value>The devices.</value>
        public required IReadOnlyList<IAccelerator> Devices { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether active.
        /// </summary>
        /// <value>The is active.</value>
        public bool IsActive { get; set; }
        /// <summary>
        /// Gets or sets the completion task.
        /// </summary>
        /// <value>The completion task.</value>
        public required Task<bool> CompletionTask { get; init; }
    }

    /// <summary>
    /// P2P synchronization event for cross-device signaling.
    /// </summary>
    public sealed class P2PSyncEvent
    {
        /// <summary>
        /// Gets or sets the event identifier.
        /// </summary>
        /// <value>The event id.</value>
        public required string EventId { get; init; }
        /// <summary>
        /// Gets or sets the participant devices.
        /// </summary>
        /// <value>The participant devices.</value>
        public required IList<string> ParticipantDevices { get; init; }
        /// <summary>
        /// Gets or sets the event type.
        /// </summary>
        /// <value>The event type.</value>
        public required P2PSyncEventType EventType { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether signaled.
        /// </summary>
        /// <value>The is signaled.</value>
        public bool IsSignaled { get; set; }
        /// <summary>
        /// Gets or sets the waiting devices.
        /// </summary>
        /// <value>The waiting devices.</value>
        public required ConcurrentDictionary<string, TaskCompletionSource<bool>> WaitingDevices { get; init; }
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>The created at.</value>
        public required DateTimeOffset CreatedAt { get; init; }
        /// <summary>
        /// Gets or sets the last signaled at.
        /// </summary>
        /// <value>The last signaled at.</value>
        public DateTimeOffset? LastSignaledAt { get; set; }
        /// <summary>
        /// Gets or sets the signal count.
        /// </summary>
        /// <value>The signal count.</value>
        public long SignalCount { get; set; }
    }

    /// <summary>
    /// P2P atomic counter for cross-device coordination.
    /// </summary>
    public sealed class P2PAtomicCounter
    {
        /// <summary>
        /// Gets or sets the counter identifier.
        /// </summary>
        /// <value>The counter id.</value>
        public required string CounterId { get; init; }
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        public long Value { get; set; }
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>The created at.</value>
        public required DateTimeOffset CreatedAt { get; init; }
        /// <summary>
        /// Gets or sets the last modified at.
        /// </summary>
        /// <value>The last modified at.</value>
        public DateTimeOffset? LastModifiedAt { get; set; }
        /// <summary>
        /// Gets or sets the operation count.
        /// </summary>
        /// <value>The operation count.</value>
        public long OperationCount { get; set; }
    }

    /// <summary>
    /// P2P device synchronization state.
    /// </summary>
    public sealed class P2PDeviceSyncState
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device id.</value>
        public required string DeviceId { get; init; }
        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        /// <value>The device name.</value>
        public required string DeviceName { get; init; }
        /// <summary>
        /// Gets or sets the device.
        /// </summary>
        /// <value>The device.</value>
        public required IAccelerator Device { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether initialized.
        /// </summary>
        /// <value>The is initialized.</value>
        public bool IsInitialized { get; set; }
        /// <summary>
        /// Gets or sets the active barriers.
        /// </summary>
        /// <value>The active barriers.</value>
        public required IList<string> ActiveBarriers { get; init; }
        /// <summary>
        /// Gets or sets the active events.
        /// </summary>
        /// <value>The active events.</value>
        public required IList<string> ActiveEvents { get; init; }
        /// <summary>
        /// Gets or sets the last sync operation.
        /// </summary>
        /// <value>The last sync operation.</value>
        public DateTimeOffset LastSyncOperation { get; set; }
    }

    /// <summary>
    /// P2P synchronization statistics.
    /// </summary>
    public sealed class P2PSyncStatistics
    {
        /// <summary>
        /// Gets or sets the barriers created.
        /// </summary>
        /// <value>The barriers created.</value>
        public long BarriersCreated { get; set; }
        /// <summary>
        /// Gets or sets the barriers completed.
        /// </summary>
        /// <value>The barriers completed.</value>
        public long BarriersCompleted { get; set; }
        /// <summary>
        /// Gets or sets the barriers timed out.
        /// </summary>
        /// <value>The barriers timed out.</value>
        public long BarriersTimedOut { get; set; }
        /// <summary>
        /// Gets or sets the active barriers.
        /// </summary>
        /// <value>The active barriers.</value>
        public int ActiveBarriers { get; set; }
        /// <summary>
        /// Gets or sets the events created.
        /// </summary>
        /// <value>The events created.</value>
        public long EventsCreated { get; set; }
        /// <summary>
        /// Gets or sets the events signaled.
        /// </summary>
        /// <value>The events signaled.</value>
        public long EventsSignaled { get; set; }
        /// <summary>
        /// Gets or sets the active events.
        /// </summary>
        /// <value>The active events.</value>
        public int ActiveEvents { get; set; }
        /// <summary>
        /// Gets or sets the atomic counters created.
        /// </summary>
        /// <value>The atomic counters created.</value>
        public long AtomicCountersCreated { get; set; }
        /// <summary>
        /// Gets or sets the active atomic counters.
        /// </summary>
        /// <value>The active atomic counters.</value>
        public int ActiveAtomicCounters { get; set; }
        /// <summary>
        /// Gets or sets the atomic operations.
        /// </summary>
        /// <value>The atomic operations.</value>
        public long AtomicOperations { get; set; }
        /// <summary>
        /// Gets or sets the total barrier time.
        /// </summary>
        /// <value>The total barrier time.</value>
        public TimeSpan TotalBarrierTime { get; set; }
        /// <summary>
        /// Gets or sets the average barrier time.
        /// </summary>
        /// <value>The average barrier time.</value>
        public TimeSpan AverageBarrierTime { get; set; }
        /// <summary>
        /// Gets or sets the initialized devices.
        /// </summary>
        /// <value>The initialized devices.</value>
        public int InitializedDevices { get; set; }
    }
    /// <summary>
    /// An p2 p barrier state enumeration.
    /// </summary>

    /// <summary>
    /// P2P barrier states.
    /// </summary>
    public enum P2PBarrierState
    {
        Active,
        Completed,
        TimedOut,
        Cancelled
    }
    /// <summary>
    /// An p2 p sync event type enumeration.
    /// </summary>

    /// <summary>
    /// P2P synchronization event types.
    /// </summary>
    public enum P2PSyncEventType
    {
        AutoReset,  // Automatically resets after signaling
        ManualReset // Remains signaled until manually reset
    }

    #endregion
}
