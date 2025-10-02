// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Memory.P2P
{
    /// <summary>
    /// Advanced P2P Synchronizer that provides cross-device synchronization primitives,
    /// barriers, and memory consistency guarantees for multi-GPU P2P operations.
    /// </summary>
    public sealed class P2PSynchronizer : IAsyncDisposable
    {
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
                });

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
                });

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


                _logger.LogTrace("Device {DeviceId} arrived at barrier {BarrierId} ({Arrived}/{Expected})",
                    deviceId, barrierId, barrier.ArrivedParticipants, barrier.ExpectedParticipants);

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
        public P2PSyncStatistics GetSynchronizationStatistics()
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
                var activeEventCount = _syncEvents.Values.Count(e => e.WaitingDevices.Any());

                if (activeBarrierCount > 0 || activeEventCount > 0)
                {
                    _logger.LogTrace("Sync monitor: {ActiveBarriers} active barriers, {ActiveEvents} active events with waiters",
                        activeBarrierCount, activeEventCount);
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
                _logger.LogWarning(ex, "Error in synchronization health monitoring");
            }
        }

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
        public required string BarrierId { get; init; }
        public required List<string> ParticipantDevices { get; init; }
        public required int ExpectedParticipants { get; init; }
        public int ArrivedParticipants { get; set; }
        public P2PBarrierState BarrierState { get; set; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required int TimeoutMs { get; init; }
        public required TaskCompletionSource<bool> CompletionSource { get; init; }
    }

    /// <summary>
    /// Multi-device P2P synchronization barrier.
    /// </summary>
    public sealed class P2PMultiDeviceBarrier
    {
        public required string BarrierId { get; init; }
        public required IAccelerator[] Devices { get; init; }
        public bool IsActive { get; set; }
        public required Task<bool> CompletionTask { get; init; }
    }

    /// <summary>
    /// P2P synchronization event for cross-device signaling.
    /// </summary>
    public sealed class P2PSyncEvent
    {
        public required string EventId { get; init; }
        public required List<string> ParticipantDevices { get; init; }
        public required P2PSyncEventType EventType { get; init; }
        public bool IsSignaled { get; set; }
        public required ConcurrentDictionary<string, TaskCompletionSource<bool>> WaitingDevices { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LastSignaledAt { get; set; }
        public long SignalCount { get; set; }
    }

    /// <summary>
    /// P2P atomic counter for cross-device coordination.
    /// </summary>
    public sealed class P2PAtomicCounter
    {
        public required string CounterId { get; init; }
        public long Value { get; set; }
        public required DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LastModifiedAt { get; set; }
        public long OperationCount { get; set; }
    }

    /// <summary>
    /// P2P device synchronization state.
    /// </summary>
    public sealed class P2PDeviceSyncState
    {
        public required string DeviceId { get; init; }
        public required string DeviceName { get; init; }
        public required IAccelerator Device { get; init; }
        public bool IsInitialized { get; set; }
        public required List<string> ActiveBarriers { get; init; }
        public required List<string> ActiveEvents { get; init; }
        public DateTimeOffset LastSyncOperation { get; set; }
    }

    /// <summary>
    /// P2P synchronization statistics.
    /// </summary>
    public sealed class P2PSyncStatistics
    {
        public long BarriersCreated { get; set; }
        public long BarriersCompleted { get; set; }
        public long BarriersTimedOut { get; set; }
        public int ActiveBarriers { get; set; }
        public long EventsCreated { get; set; }
        public long EventsSignaled { get; set; }
        public int ActiveEvents { get; set; }
        public long AtomicCountersCreated { get; set; }
        public int ActiveAtomicCounters { get; set; }
        public long AtomicOperations { get; set; }
        public TimeSpan TotalBarrierTime { get; set; }
        public TimeSpan AverageBarrierTime { get; set; }
        public int InitializedDevices { get; set; }
    }

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
    /// P2P synchronization event types.
    /// </summary>
    public enum P2PSyncEventType
    {
        AutoReset,  // Automatically resets after signaling
        ManualReset // Remains signaled until manually reset
    }

    #endregion
}
