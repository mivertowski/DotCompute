// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Execution
{

    /// <summary>
    /// Coordinates execution across multiple devices with synchronization primitives.
    /// </summary>
    public sealed partial class ExecutionCoordinator(ILogger logger) : IAsyncDisposable
    {
        // LoggerMessage delegates - Event ID range 23100-23114 for ExecutionCoordinator
        private static readonly Action<ILogger, string, Exception?> _logEventCreated =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(23100, nameof(LogEventCreated)),
                "Created execution event: {EventName}");

        private static void LogEventCreated(ILogger logger, string eventName)
            => _logEventCreated(logger, eventName, null);

        private static readonly Action<ILogger, string, int, Exception?> _logBarrierCreated =
            LoggerMessage.Define<string, int>(
                LogLevel.Debug,
                new EventId(23101, nameof(LogBarrierCreated)),
                "Created execution barrier: {BarrierName} with {ParticipantCount} participants");

        private static void LogBarrierCreated(ILogger logger, string barrierName, int participantCount)
            => _logBarrierCreated(logger, barrierName, participantCount, null);

        private static readonly Action<ILogger, string, Exception?> _logEventSignaled =
            LoggerMessage.Define<string>(
                LogLevel.Trace,
                new EventId(23102, nameof(LogEventSignaled)),
                "Signaled event: {EventName}");

        private static void LogEventSignaled(ILogger logger, string eventName)
            => _logEventSignaled(logger, eventName, null);

        private static readonly Action<ILogger, string, Exception?> _logEventWaitCompleted =
            LoggerMessage.Define<string>(
                LogLevel.Trace,
                new EventId(23103, nameof(LogEventWaitCompleted)),
                "Completed wait for event: {EventName}");

        private static void LogEventWaitCompleted(ILogger logger, string eventName)
            => _logEventWaitCompleted(logger, eventName, null);

        private static readonly Action<ILogger, string, Exception?> _logAllEventsCompleted =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(23104, nameof(LogAllEventsCompleted)),
                "All events completed: {EventNames}");

        private static void LogAllEventsCompleted(ILogger logger, string eventNames)
            => _logAllEventsCompleted(logger, eventNames, null);

        private static readonly Action<ILogger, string, Exception?> _logEventCompletedFirst =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(23105, nameof(LogEventCompletedFirst)),
                "Event completed first: {EventName}");

        private static void LogEventCompletedFirst(ILogger logger, string eventName)
            => _logEventCompletedFirst(logger, eventName, null);

        private static readonly Action<ILogger, string, Exception?> _logBarrierEntered =
            LoggerMessage.Define<string>(
                LogLevel.Trace,
                new EventId(23106, nameof(LogBarrierEntered)),
                "Entered barrier: {BarrierName}");

        private static void LogBarrierEntered(ILogger logger, string barrierName)
            => _logBarrierEntered(logger, barrierName, null);

        private static readonly Action<ILogger, Exception?> _logResetAllCompleted =
            LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(23107, nameof(LogResetAllCompleted)),
                "Reset all events and barriers");

        private static void LogResetAllCompleted(ILogger logger)
            => _logResetAllCompleted(logger, null);

        private static readonly Action<ILogger, Exception?> _logCoordinatorDisposing =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(23108, nameof(LogCoordinatorDisposing)),
                "Disposing ExecutionCoordinator");

        private static void LogCoordinatorDisposing(ILogger logger)
            => _logCoordinatorDisposing(logger, null);

        private static readonly Action<ILogger, Exception?> _logCoordinatorDisposed =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(23109, nameof(LogCoordinatorDisposed)),
                "ExecutionCoordinator disposed");

        private static void LogCoordinatorDisposed(ILogger logger)
            => _logCoordinatorDisposed(logger, null);

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ConcurrentDictionary<string, ExecutionEvent> _events = new();
        private readonly ConcurrentDictionary<string, ExecutionBarrier> _barriers = new();
        private readonly SemaphoreSlim _coordinationSemaphore = new(1);
        private bool _disposed;

        /// <summary>
        /// Creates a synchronization event.
        /// </summary>
        public ExecutionEvent CreateEvent(string eventName)
        {
            var executionEvent = new ExecutionEvent(eventName, _logger);
            _events[eventName] = executionEvent;
            LogEventCreated(_logger, eventName);
            return executionEvent;
        }

        /// <summary>
        /// Creates a barrier for synchronizing multiple devices.
        /// </summary>
        public ExecutionBarrier CreateBarrier(string barrierName, int participantCount)
        {
            var barrier = new ExecutionBarrier(barrierName, participantCount, _logger);
            _barriers[barrierName] = barrier;
            LogBarrierCreated(_logger, barrierName, participantCount);
            return barrier;
        }

        /// <summary>
        /// Signals an event completion.
        /// </summary>
        public async ValueTask SignalEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default)
        {
            await _coordinationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await executionEvent.SignalAsync(cancellationToken).ConfigureAwait(false);
                LogEventSignaled(_logger, executionEvent.Name);
            }
            finally
            {
                _ = _coordinationSemaphore.Release();
            }
        }

        /// <summary>
        /// Waits for a specific event to be signaled.
        /// </summary>
        public async ValueTask WaitForEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default)
        {
            await executionEvent.WaitAsync(cancellationToken).ConfigureAwait(false);
            LogEventWaitCompleted(_logger, executionEvent.Name);
        }

        /// <summary>
        /// Waits for all specified events to be signaled.
        /// </summary>
        public async ValueTask WaitForAllEventsAsync(ExecutionEvent[] events, CancellationToken cancellationToken = default)
        {
            var waitTasks = events.Select(e => e.WaitAsync(cancellationToken).AsTask()).ToArray();
            await Task.WhenAll(waitTasks).ConfigureAwait(false);
            LogAllEventsCompleted(_logger, string.Join(", ", events.Select(e => e.ToString())));
        }

        /// <summary>
        /// Waits for any of the specified events to be signaled.
        /// </summary>
        public async ValueTask<ExecutionEvent> WaitForAnyEventAsync(ExecutionEvent[] events, CancellationToken cancellationToken = default)
        {
            var waitTasks = events.Select((e, index) => e.WaitAsync(cancellationToken).AsTask().ContinueWith(_ => index, cancellationToken)).ToArray();
            var completedTask = await Task.WhenAny(waitTasks).ConfigureAwait(false);
            var completedIndex = await completedTask.ConfigureAwait(false);
            var completedEvent = events[completedIndex];
            LogEventCompletedFirst(_logger, completedEvent.Name);
            return completedEvent;
        }

        /// <summary>
        /// Enters a barrier and waits for all participants.
        /// </summary>
        public async ValueTask EnterBarrierAsync(ExecutionBarrier barrier, CancellationToken cancellationToken = default)
        {
            await barrier.EnterAsync(cancellationToken).ConfigureAwait(false);
            LogBarrierEntered(_logger, barrier.Name);
        }

        /// <summary>
        /// Resets all events and barriers for reuse.
        /// </summary>
        public async ValueTask ResetAllAsync()
        {
            await _coordinationSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var resetTasks = new List<Task>();

                foreach (var kvp in _events)
                {
                    resetTasks.Add(kvp.Value.ResetAsync().AsTask());
                }

                foreach (var kvp in _barriers)
                {
                    resetTasks.Add(kvp.Value.ResetAsync().AsTask());
                }

                await Task.WhenAll(resetTasks).ConfigureAwait(false);
                LogResetAllCompleted(_logger);
            }
            finally
            {
                _ = _coordinationSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets statistics about coordination operations.
        /// </summary>
        public CoordinationStatistics GetStatistics()
        {
            return new CoordinationStatistics
            {
                TotalEvents = _events.Count,
                TotalBarriers = _barriers.Count,
                ActiveEvents = _events.Count(kvp => !kvp.Value.IsSignaled),
                ActiveBarriers = _barriers.Count(kvp => !kvp.Value.IsComplete)
            };
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

            LogCoordinatorDisposing(_logger);

            // Dispose all events and barriers
            var disposeTasks = new List<Task>();

            foreach (var kvp in _events)
            {
                disposeTasks.Add(kvp.Value.DisposeAsync().AsTask());
            }

            foreach (var kvp in _barriers)
            {
                disposeTasks.Add(kvp.Value.DisposeAsync().AsTask());
            }

            foreach (var task in disposeTasks)
            {
                await task.ConfigureAwait(false);
            }

            _events.Clear();
            _barriers.Clear();
            _coordinationSemaphore.Dispose();

            _disposed = true;
            LogCoordinatorDisposed(_logger);
        }
    }

    /// <summary>
    /// Represents a synchronization event for device coordination.
    /// </summary>
    public sealed partial class ExecutionEvent(string name, ILogger logger) : IAsyncDisposable
    {
        // LoggerMessage delegates - Event ID range 23110-23112 for ExecutionEvent
        private static readonly Action<ILogger, string, Exception?> _logExecutionEventSignaled =
            LoggerMessage.Define<string>(
                LogLevel.Trace,
                new EventId(23110, nameof(LogExecutionEventSignaled)),
                "Signaled execution event: {EventName}");

        private static void LogExecutionEventSignaled(ILogger logger, string eventName)
            => _logExecutionEventSignaled(logger, eventName, null);

        private static readonly Action<ILogger, string, Exception?> _logExecutionEventReset =
            LoggerMessage.Define<string>(
                LogLevel.Trace,
                new EventId(23111, nameof(LogExecutionEventReset)),
                "Reset execution event: {EventName}");

        private static void LogExecutionEventReset(ILogger logger, string eventName)
            => _logExecutionEventReset(logger, eventName, null);

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly SemaphoreSlim _semaphore = new(0);
        private volatile bool _isSignaled;
        private bool _disposed;

        /// <summary>Gets the event name.</summary>
        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

        /// <summary>Gets whether the event has been signaled.</summary>
        public bool IsSignaled => _isSignaled;

        /// <summary>
        /// Signals the event, allowing waiting threads to proceed.
        /// </summary>
        public async ValueTask SignalAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_isSignaled)
            {
                _isSignaled = true;
                _ = _semaphore.Release();
                LogExecutionEventSignaled(_logger, Name);
            }

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Waits for the event to be signaled.
        /// </summary>
        public async ValueTask WaitAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_isSignaled)
            {
                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resets the event to unsignaled state.
        /// </summary>
        public async ValueTask ResetAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _isSignaled = false;
            // Drain any pending releases
            while (_semaphore.CurrentCount > 0)
            {
                try
                {
                    _ = _semaphore.Wait(0);
                }
                catch (TimeoutException)
                {
                    break;
                }
            }

            LogExecutionEventReset(_logger, Name);
            await ValueTask.CompletedTask;
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

            // Signal to unblock any waiting threads
            if (!_isSignaled)
            {
                await SignalAsync().ConfigureAwait(false);
            }

            _semaphore.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Represents a synchronization barrier for coordinating multiple devices.
    /// </summary>
    public sealed partial class ExecutionBarrier(string name, int participantCount, ILogger logger) : IAsyncDisposable
    {
        // LoggerMessage delegates - Event ID range 23112-23114 for ExecutionBarrier
        private static readonly Action<ILogger, int, int, string, Exception?> _logBarrierParticipantEntered =
            LoggerMessage.Define<int, int, string>(
                LogLevel.Trace,
                new EventId(23112, nameof(LogBarrierParticipantEntered)),
                "Participant {ParticipantNumber}/{TotalParticipants} entered barrier: {BarrierName}");

        private static void LogBarrierParticipantEntered(ILogger logger, int participantNumber, int totalParticipants, string barrierName)
            => _logBarrierParticipantEntered(logger, participantNumber, totalParticipants, barrierName, null);

        private static readonly Action<ILogger, string, Exception?> _logBarrierAllParticipantsEntered =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(23113, nameof(LogBarrierAllParticipantsEntered)),
                "All participants entered barrier: {BarrierName}");

        private static void LogBarrierAllParticipantsEntered(ILogger logger, string barrierName)
            => _logBarrierAllParticipantsEntered(logger, barrierName, null);

        private static readonly Action<ILogger, string, Exception?> _logExecutionBarrierReset =
            LoggerMessage.Define<string>(
                LogLevel.Trace,
                new EventId(23114, nameof(LogExecutionBarrierReset)),
                "Reset execution barrier: {BarrierName}");

        private static void LogExecutionBarrierReset(ILogger logger, string barrierName)
            => _logExecutionBarrierReset(logger, barrierName, null);

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly int _participantCount = participantCount;
        private readonly SemaphoreSlim _entrySemaphore = new(0);
        private volatile int _participantsEntered;
        private volatile bool _isComplete;
        private bool _disposed;

        /// <summary>Gets the barrier name.</summary>
        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

        /// <summary>Gets the number of participants required.</summary>
        public int ParticipantCount => _participantCount;

        /// <summary>Gets the number of participants that have entered.</summary>
        public int ParticipantsEntered => _participantsEntered;

        /// <summary>Gets whether all participants have entered the barrier.</summary>
        public bool IsComplete => _isComplete;

        /// <summary>
        /// Enters the barrier and waits for all participants.
        /// </summary>
        public async ValueTask EnterAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var participantNumber = Interlocked.Increment(ref _participantsEntered);
            LogBarrierParticipantEntered(_logger, participantNumber, _participantCount, Name);

            if (participantNumber == _participantCount)
            {
                // Last participant - signal all waiting participants
                _isComplete = true;
                _ = _entrySemaphore.Release(_participantCount - 1); // Release all but the current thread
                LogBarrierAllParticipantsEntered(_logger, Name);
            }
            else
            {
                // Wait for all participants to arrive
                await _entrySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resets the barrier for reuse.
        /// </summary>
        public async ValueTask ResetAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _participantsEntered = 0;
            _isComplete = false;

            // Drain any pending releases
            while (_entrySemaphore.CurrentCount > 0)
            {
                try
                {
                    _ = _entrySemaphore.Wait(0);
                }
                catch (TimeoutException)
                {
                    break;
                }
            }

            LogExecutionBarrierReset(_logger, Name);
            await ValueTask.CompletedTask;
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

            // Release all waiting participants
            if (!_isComplete && _participantsEntered > 0)
            {
                _ = _entrySemaphore.Release(_participantsEntered);
            }

            _entrySemaphore.Dispose();
            _disposed = true;
            await ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Statistics about coordination operations.
    /// </summary>
    public class CoordinationStatistics
    {
        /// <summary>Gets or sets the total number of events.</summary>
        public int TotalEvents { get; set; }

        /// <summary>Gets or sets the total number of barriers.</summary>
        public int TotalBarriers { get; set; }

        /// <summary>Gets or sets the number of active (unsignaled) events.</summary>
        public int ActiveEvents { get; set; }

        /// <summary>Gets or sets the number of active (incomplete) barriers.</summary>
        public int ActiveBarriers { get; set; }
    }
}
