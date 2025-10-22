// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DotCompute.Core.Performance;

/// <summary>
/// High-performance async thread synchronization patterns optimized for compute workloads:
/// - Lock-free async coordination using channels and semaphores
/// - Producer-consumer patterns with backpressure handling
/// - Async resource pooling with fair scheduling
/// - Work-stealing task coordination
/// - Barrier synchronization for parallel algorithms
/// Target: Sub-microsecond coordination overhead with zero blocking
/// </summary>
public static class AsyncSynchronizationPatterns
{
    /// <summary>
    /// Creates a high-performance async producer-consumer channel with optimal batching.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="capacity">Channel capacity for backpressure control.</param>
    /// <param name="singleReader">True if only one consumer will read.</param>
    /// <param name="singleWriter">True if only one producer will write.</param>
    /// <returns>Optimized async channel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncChannel<T> CreateOptimizedChannel<T>(
        int capacity = 1000,
        bool singleReader = false,
        bool singleWriter = false)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            SingleReader = singleReader,
            SingleWriter = singleWriter,
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait
        };

        var channel = Channel.CreateBounded<T>(options);
        return new AsyncChannel<T>(channel);
    }

    /// <summary>
    /// Creates an async work-stealing coordinator for parallel task execution.
    /// </summary>
    /// <typeparam name="T">Work item type.</typeparam>
    /// <param name="workerCount">Number of worker threads.</param>
    /// <returns>Work-stealing coordinator.</returns>
    public static AsyncWorkStealingCoordinator<T> CreateWorkStealingCoordinator<T>(int workerCount = -1)
    {
        if (workerCount <= 0)
        {
            workerCount = Environment.ProcessorCount;
        }


        return new AsyncWorkStealingCoordinator<T>(workerCount);
    }

    /// <summary>
    /// Creates an async resource pool with fair scheduling and overflow handling.
    /// </summary>
    /// <typeparam name="TResource">Resource type.</typeparam>
    /// <param name="factory">Resource factory function.</param>
    /// <param name="maxResources">Maximum number of resources.</param>
    /// <param name="fairScheduling">Enable fair FIFO scheduling.</param>
    /// <returns>Async resource pool.</returns>
    public static AsyncResourcePool<TResource> CreateResourcePool<TResource>(
        Func<TResource> factory,
        int maxResources = 10,
        bool fairScheduling = true) where TResource : class => new(factory, maxResources, fairScheduling);

    /// <summary>
    /// Creates an async barrier for coordinating parallel algorithm phases.
    /// </summary>
    /// <param name="participantCount">Number of participants.</param>
    /// <param name="postPhaseAction">Optional action to run after each phase.</param>
    /// <returns>Async barrier.</returns>
    public static AsyncBarrier CreateBarrier(int participantCount, Action? postPhaseAction = null) => new(participantCount, postPhaseAction);
}

/// <summary>
/// High-performance async channel wrapper with batching optimizations.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed class AsyncChannel<T> : IDisposable
{
    private readonly Channel<T> _channel;
    private readonly ChannelWriter<T> _writer;
    private readonly ChannelReader<T> _reader;
    private volatile bool _disposed;

    internal AsyncChannel(Channel<T> channel)
    {
        _channel = channel;
        _writer = channel.Writer;
        _reader = channel.Reader;
    }

    /// <summary>
    /// Writer for producing items.
    /// </summary>
    public ChannelWriter<T> Writer => _writer;

    /// <summary>
    /// Reader for consuming items.
    /// </summary>
    public ChannelReader<T> Reader => _reader;

    /// <summary>
    /// Writes an item to the channel with optimal async behavior.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes multiple items in a batch for better throughput.
    /// </summary>
    public async ValueTask WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        foreach (var item in items)
        {
            await _writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads an item from the channel with optimal async behavior.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads multiple items in a batch with timeout support.
    /// </summary>
    public async ValueTask<List<T>> ReadBatchAsync(int maxCount, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var items = new List<T>(maxCount);
        try
        {
            while (items.Count < maxCount && await _reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
            {
                if (_reader.TryRead(out var item))
                {
                    items.Add(item);
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred, return what we have
        }

        return items;
    }

    /// <summary>
    /// Completes the writer to signal no more items will be written.
    /// </summary>
    public void Complete(Exception? error = null)
    {
        if (error != null)
        {
            _ = _writer.TryComplete(error);
        }
        else
        {
            _ = _writer.TryComplete();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Complete();
        }
    }
}

/// <summary>
/// Async work-stealing coordinator for optimal parallel task distribution.
/// </summary>
/// <typeparam name="T">Work item type.</typeparam>
public sealed class AsyncWorkStealingCoordinator<T> : IDisposable
{
    private readonly ConcurrentQueue<T>[] _workQueues;
    private readonly SemaphoreSlim[] _workAvailable;
    private readonly int _workerCount;
    private readonly Random _random = new();
    private volatile bool _disposed;

    internal AsyncWorkStealingCoordinator(int workerCount)
    {
        _workerCount = workerCount;
        _workQueues = new ConcurrentQueue<T>[workerCount];
        _workAvailable = new SemaphoreSlim[workerCount];

        for (var i = 0; i < workerCount; i++)
        {
            _workQueues[i] = new ConcurrentQueue<T>();
            _workAvailable[i] = new SemaphoreSlim(0);
        }
    }

    /// <summary>
    /// Submits work to a specific worker queue.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubmitWork(T work, int preferredWorker = -1)
    {
        ThrowIfDisposed();

        if (preferredWorker < 0 || preferredWorker >= _workerCount)
        {
#pragma warning disable CA5394 // Random is used for load balancing distribution, not security
            preferredWorker = _random.Next(_workerCount);
#pragma warning restore CA5394
        }


        _workQueues[preferredWorker].Enqueue(work);
        _ = _workAvailable[preferredWorker].Release();
    }

    /// <summary>
    /// Worker method that efficiently steals work from other queues when idle.
    /// </summary>
    public async ValueTask<T?> GetWorkAsync(int workerId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (workerId < 0 || workerId >= _workerCount)
        {

            throw new ArgumentOutOfRangeException(nameof(workerId));
        }

        // Try own queue first

        if (_workQueues[workerId].TryDequeue(out var work))
        {
            return work;
        }

        // Wait for work on own queue

        await _workAvailable[workerId].WaitAsync(cancellationToken).ConfigureAwait(false);

        // Try own queue again
        if (_workQueues[workerId].TryDequeue(out work))
        {

            return work;
        }

        // Work stealing - try other queues

        for (var i = 1; i < _workerCount; i++)
        {
            var targetQueue = (workerId + i) % _workerCount;
            if (_workQueues[targetQueue].TryDequeue(out work))
            {

                return work;
            }
        }

        return default;
    }

    /// <summary>
    /// Submits multiple work items efficiently across all worker queues.
    /// </summary>
    public void SubmitWorkBatch(IEnumerable<T> workItems)
    {
        ThrowIfDisposed();

        var currentWorker = 0;
        foreach (var work in workItems)
        {
            _workQueues[currentWorker].Enqueue(work);
            _ = _workAvailable[currentWorker].Release();
            currentWorker = (currentWorker + 1) % _workerCount;
        }
    }

    /// <summary>
    /// Gets the current work queue length for a specific worker.
    /// </summary>
    public int GetQueueLength(int workerId)
    {
        ThrowIfDisposed();
        return workerId >= 0 && workerId < _workerCount ? _workQueues[workerId].Count : 0;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            for (var i = 0; i < _workerCount; i++)
            {
                _workAvailable[i]?.Dispose();
            }
        }
    }
}

/// <summary>
/// Async resource pool with fair scheduling and overflow protection.
/// </summary>
/// <typeparam name="TResource">Resource type.</typeparam>
public sealed class AsyncResourcePool<TResource> : IDisposable where TResource : class
{
    private readonly Func<TResource> _factory;
    private readonly ConcurrentStack<TResource> _resources;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<TaskCompletionSource<TResource>>? _waitQueue;
    private readonly int _maxResources;
    private volatile bool _disposed;

    internal AsyncResourcePool(Func<TResource> factory, int maxResources, bool fairScheduling)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _maxResources = maxResources;
        _resources = new ConcurrentStack<TResource>();
        _semaphore = new SemaphoreSlim(maxResources, maxResources);

        if (fairScheduling)
        {
            _waitQueue = new ConcurrentQueue<TaskCompletionSource<TResource>>();
        }
    }

    /// <summary>
    /// Acquires a resource from the pool with optional timeout.
    /// </summary>
    public async ValueTask<PooledResource<TResource>> AcquireAsync(
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != default)
        {
            timeoutCts.CancelAfter(timeout);
        }

        try
        {
            await _semaphore.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            if (!_resources.TryPop(out var resource))
            {
                resource = _factory();
            }

            return new PooledResource<TResource>(resource, this);
        }
        finally
        {
            timeoutCts.Dispose();
        }
    }

    /// <summary>
    /// Returns a resource to the pool.
    /// </summary>
    internal void Return(TResource resource)
    {
        if (_disposed || resource == null)
        {
            return;
        }

        // If using fair scheduling, serve waiting requests first

        if (_waitQueue != null && _waitQueue.TryDequeue(out var tcs))
        {
            tcs.SetResult(resource);
        }
        else
        {
            _resources.Push(resource);
        }

        _ = _semaphore.Release();
    }

    /// <summary>
    /// Gets current pool statistics.
    /// </summary>
    public PoolStatistics Statistics
    {
        get
        {
            return new PoolStatistics
            {
                AvailableResources = _resources.Count,
                TotalResources = _maxResources - _semaphore.CurrentCount,
                WaitingRequests = _waitQueue?.Count ?? 0
            };
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _semaphore?.Dispose();

            // Complete any waiting requests with cancellation
            if (_waitQueue != null)
            {
                while (_waitQueue.TryDequeue(out var tcs))
                {
                    tcs.SetCanceled();
                }
            }

            // Dispose resources if they implement IDisposable
            while (_resources.TryPop(out var resource))
            {
                if (resource is IDisposable disposable)
                {

                    disposable.Dispose();
                }
            }
        }
    }
}

/// <summary>
/// RAII wrapper for pooled resources with automatic return.
/// </summary>
/// <typeparam name="TResource">Resource type.</typeparam>
public readonly struct PooledResource<TResource> : IDisposable, IEquatable<PooledResource<TResource>> where TResource : class
{
    private readonly TResource _resource;
    private readonly AsyncResourcePool<TResource> _pool;

    internal PooledResource(TResource resource, AsyncResourcePool<TResource> pool)
    {
        _resource = resource;
        _pool = pool;
    }

    /// <summary>
    /// Gets the underlying resource.
    /// </summary>
    public TResource Resource => _resource;

    /// <summary>
    /// Converts the pooled resource to the underlying resource type.
    /// </summary>
    /// <param name="pooled">The pooled resource.</param>
    /// <returns>The underlying resource.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Explicit conversion method complements the implicit operator for API completeness")]
    public static TResource ToTResource(PooledResource<TResource> pooled) => pooled._resource;

    /// <summary>
    /// Implicitly converts to the resource type.
    /// </summary>
    public static implicit operator TResource(PooledResource<TResource> pooled) => pooled._resource;
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose() => _pool?.Return(_resource);

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is PooledResource<TResource> other && Equals(other);

    /// <summary>
    /// Determines whether the specified PooledResource is equal to the current instance.
    /// </summary>
    /// <param name="other">The PooledResource to compare with the current instance.</param>
    /// <returns>true if the specified PooledResource is equal to the current instance; otherwise, false.</returns>
    public bool Equals(PooledResource<TResource> other) => ReferenceEquals(_resource, other._resource) && ReferenceEquals(_pool, other._pool);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(_resource, _pool);

    /// <summary>
    /// Determines whether two specified PooledResource instances are equal.
    /// </summary>
    /// <param name="left">The first PooledResource to compare.</param>
    /// <param name="right">The second PooledResource to compare.</param>
    /// <returns>true if left and right are equal; otherwise, false.</returns>
    public static bool operator ==(PooledResource<TResource> left, PooledResource<TResource> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two specified PooledResource instances are not equal.
    /// </summary>
    /// <param name="left">The first PooledResource to compare.</param>
    /// <param name="right">The second PooledResource to compare.</param>
    /// <returns>true if left and right are not equal; otherwise, false.</returns>
    public static bool operator !=(PooledResource<TResource> left, PooledResource<TResource> right) => !left.Equals(right);
}

/// <summary>
/// Async barrier for coordinating parallel algorithm phases.
/// </summary>
public sealed class AsyncBarrier : IDisposable
{
    private readonly int _participantCount;
    private readonly Action? _postPhaseAction;
    private volatile int _currentPhase;
    private volatile int _participantsRemaining;
    private volatile TaskCompletionSource<bool> _phaseCompletion;
    private readonly object _lock = new();
    private volatile bool _disposed;

    internal AsyncBarrier(int participantCount, Action? postPhaseAction = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantCount);

        _participantCount = participantCount;
        _postPhaseAction = postPhaseAction;
        _participantsRemaining = participantCount;
        _phaseCompletion = new TaskCompletionSource<bool>();
    }

    /// <summary>
    /// Current phase number.
    /// </summary>
    public int CurrentPhase => _currentPhase;

    /// <summary>
    /// Number of participants still needed for this phase.
    /// </summary>
    public int ParticipantsRemaining => _participantsRemaining;

    /// <summary>
    /// Signals arrival and waits for all other participants.
    /// </summary>
    public async ValueTask SignalAndWaitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        TaskCompletionSource<bool> currentCompletion;
        var lastParticipant = false;

        lock (_lock)
        {
            currentCompletion = _phaseCompletion;
            _participantsRemaining--;

            if (_participantsRemaining == 0)
            {
                lastParticipant = true;
                _currentPhase++;
                _participantsRemaining = _participantCount;
                _phaseCompletion = new TaskCompletionSource<bool>();
            }
        }

        if (lastParticipant)
        {
            try
            {
                _postPhaseAction?.Invoke();
            }
            catch (Exception ex)
            {
                currentCompletion.SetException(ex);
                return;
            }

            currentCompletion.SetResult(true);
        }
        else
        {
            _ = await currentCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            lock (_lock)
            {
                _phaseCompletion?.SetCanceled();
            }
        }
    }
}
