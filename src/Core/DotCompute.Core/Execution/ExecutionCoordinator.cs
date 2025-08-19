// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution
{

/// <summary>
/// Coordinates execution across multiple devices with synchronization primitives.
/// </summary>
public sealed class ExecutionCoordinator : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ExecutionEvent> _events;
    private readonly ConcurrentDictionary<string, ExecutionBarrier> _barriers;
    private readonly SemaphoreSlim _coordinationSemaphore;
    private bool _disposed;

    public ExecutionCoordinator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _events = new ConcurrentDictionary<string, ExecutionEvent>();
        _barriers = new ConcurrentDictionary<string, ExecutionBarrier>();
        _coordinationSemaphore = new SemaphoreSlim(1);
    }

    /// <summary>
    /// Creates a synchronization event.
    /// </summary>
    public ExecutionEvent CreateEvent(string eventName)
    {
        var executionEvent = new ExecutionEvent(eventName, _logger);
        _events[eventName] = executionEvent;
        _logger.LogDebug("Created execution event: {EventName}", eventName);
        return executionEvent;
    }

    /// <summary>
    /// Creates a barrier for synchronizing multiple devices.
    /// </summary>
    public ExecutionBarrier CreateBarrier(string barrierName, int participantCount)
    {
        var barrier = new ExecutionBarrier(barrierName, participantCount, _logger);
        _barriers[barrierName] = barrier;
        _logger.LogDebug("Created execution barrier: {BarrierName} with {ParticipantCount} participants", 
            barrierName, participantCount);
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
            _logger.LogTrace("Signaled event: {EventName}", executionEvent.Name);
        }
        finally
        {
            _coordinationSemaphore.Release();
        }
    }

    /// <summary>
    /// Waits for a specific event to be signaled.
    /// </summary>
    public async ValueTask WaitForEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default)
    {
        await executionEvent.WaitAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Completed wait for event: {EventName}", executionEvent.Name);
    }

    /// <summary>
    /// Waits for all specified events to be signaled.
    /// </summary>
    public async ValueTask WaitForAllEventsAsync(ExecutionEvent[] events, CancellationToken cancellationToken = default)
    {
        var waitTasks = events.Select(e => e.WaitAsync(cancellationToken).AsTask()).ToArray();
        await Task.WhenAll(waitTasks).ConfigureAwait(false);
        _logger.LogDebug("All events completed: {EventNames}", string.Join(", ", events.Select(e => e.Name)));
    }

    /// <summary>
    /// Waits for any of the specified events to be signaled.
    /// </summary>
    public async ValueTask<ExecutionEvent> WaitForAnyEventAsync(ExecutionEvent[] events, CancellationToken cancellationToken = default)
    {
        var waitTasks = events.Select((e, index) => e.WaitAsync(cancellationToken).AsTask().ContinueWith(_ => index, cancellationToken)).ToArray();
        var completedIndex = await Task.WhenAny(waitTasks).Result.ConfigureAwait(false);
        var completedEvent = events[completedIndex];
        _logger.LogDebug("Event completed first: {EventName}", completedEvent.Name);
        return completedEvent;
    }

    /// <summary>
    /// Enters a barrier and waits for all participants.
    /// </summary>
    public async ValueTask EnterBarrierAsync(ExecutionBarrier barrier, CancellationToken cancellationToken = default)
    {
        await barrier.EnterAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Entered barrier: {BarrierName}", barrier.Name);
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
            _logger.LogDebug("Reset all events and barriers");
        }
        finally
        {
            _coordinationSemaphore.Release();
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing ExecutionCoordinator");

        // Dispose all events and barriers
        var disposeTasks = new List<ValueTask>();
        
        foreach (var kvp in _events)
        {
            disposeTasks.Add(kvp.Value.DisposeAsync());
        }

        foreach (var kvp in _barriers)
        {
            disposeTasks.Add(kvp.Value.DisposeAsync());
        }

        foreach (var task in disposeTasks)
        {
            await task.ConfigureAwait(false);
        }

        _events.Clear();
        _barriers.Clear();
        _coordinationSemaphore.Dispose();
        
        _disposed = true;
        _logger.LogInformation("ExecutionCoordinator disposed");
    }
}

/// <summary>
/// Represents a synchronization event for device coordination.
/// </summary>
public sealed class ExecutionEvent : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _semaphore;
    private volatile bool _isSignaled;
    private bool _disposed;

    public ExecutionEvent(string name, ILogger logger)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _semaphore = new SemaphoreSlim(0);
    }

    /// <summary>Gets the event name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the event has been signaled.</summary>
    public bool IsSignaled => _isSignaled;

    /// <summary>
    /// Signals the event, allowing waiting threads to proceed.
    /// </summary>
    public async ValueTask SignalAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionEvent));
        }

        if (!_isSignaled)
        {
            _isSignaled = true;
            _semaphore.Release();
            _logger.LogTrace("Signaled execution event: {EventName}", Name);
        }

        await ValueTask.CompletedTask;
    }

    /// <summary>
    /// Waits for the event to be signaled.
    /// </summary>
    public async ValueTask WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionEvent));
        }

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionEvent));
        }

        _isSignaled = false;
        // Drain any pending releases
        while (_semaphore.CurrentCount > 0)
        {
            try
            {
                _semaphore.Wait(0);
            }
            catch (TimeoutException)
            {
                break;
            }
        }
        
        _logger.LogTrace("Reset execution event: {EventName}", Name);
        await ValueTask.CompletedTask;
    }

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
public sealed class ExecutionBarrier : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly int _participantCount;
    private readonly SemaphoreSlim _entrySemaphore;
    private volatile int _participantsEntered;
    private volatile bool _isComplete;
    private bool _disposed;

    public ExecutionBarrier(string name, int participantCount, ILogger logger)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _participantCount = participantCount;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _entrySemaphore = new SemaphoreSlim(0);
    }

    /// <summary>Gets the barrier name.</summary>
    public string Name { get; }

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionBarrier));
        }

        var participantNumber = Interlocked.Increment(ref _participantsEntered);
        _logger.LogTrace("Participant {ParticipantNumber}/{TotalParticipants} entered barrier: {BarrierName}",
            participantNumber, _participantCount, Name);

        if (participantNumber == _participantCount)
        {
            // Last participant - signal all waiting participants
            _isComplete = true;
            _entrySemaphore.Release(_participantCount - 1); // Release all but the current thread
            _logger.LogDebug("All participants entered barrier: {BarrierName}", Name);
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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionBarrier));
        }

        _participantsEntered = 0;
        _isComplete = false;
        
        // Drain any pending releases
        while (_entrySemaphore.CurrentCount > 0)
        {
            try
            {
                _entrySemaphore.Wait(0);
            }
            catch (TimeoutException)
            {
                break;
            }
        }
        
        _logger.LogTrace("Reset execution barrier: {BarrierName}", Name);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Release all waiting participants
        if (!_isComplete && _participantsEntered > 0)
        {
            _entrySemaphore.Release(_participantsEntered);
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
}}
