// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.RingKernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.RingKernels;

/// <summary>
/// CPU-based message queue simulation using thread-safe collections.
/// </summary>
/// <typeparam name="T">Message payload type (must be unmanaged).</typeparam>
/// <remarks>
/// This is a simulation of GPU message queues for testing and systems without GPU.
/// Uses .NET concurrent collections with similar semantics to GPU lock-free queues.
/// </remarks>
public sealed class CpuMessageQueue<T> : IMessageQueue<T> where T : unmanaged
{
    private readonly int _capacity;
    private readonly ILogger<CpuMessageQueue<T>> _logger;
    private readonly BlockingCollection<KernelMessage<T>> _queue;
    private readonly object _lock = new();

    // Statistics tracking
    private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();
    private long _totalEnqueued;
    private long _totalDequeued;
    private long _totalDropped;
    private bool _isInitialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuMessageQueue{T}"/> class.
    /// </summary>
    /// <param name="capacity">Queue capacity (must be power of 2).</param>
    /// <param name="logger">Logger instance.</param>
    public CpuMessageQueue(int capacity, ILogger<CpuMessageQueue<T>> logger)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a positive power of 2", nameof(capacity));
        }

        _capacity = capacity;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queue = new BlockingCollection<KernelMessage<T>>(_capacity);

        _logger.LogDebug("Created CPU message queue with capacity {Capacity}", _capacity);
    }

    /// <inheritdoc/>
    public int Capacity => _capacity;

    /// <inheritdoc/>
    public bool IsEmpty => _queue.Count == 0;

    /// <inheritdoc/>
    public bool IsFull => _queue.Count >= _capacity;

    /// <inheritdoc/>
    public int Count => _queue.Count;

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetBuffer()
    {
        ThrowIfNotInitialized();
        throw new NotSupportedException("CPU message queues do not use unified memory buffers");
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetHeadPtr()
    {
        ThrowIfNotInitialized();
        throw new NotSupportedException("CPU message queues do not use unified memory buffers");
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetTailPtr()
    {
        ThrowIfNotInitialized();
        throw new NotSupportedException("CPU message queues do not use unified memory buffers");
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                _logger.LogWarning("Queue already initialized");
                return Task.CompletedTask;
            }

            _isInitialized = true;
            _logger.LogInformation("CPU message queue initialized with capacity {Capacity}", _capacity);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> TryEnqueueAsync(KernelMessage<T> message, CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        var added = _queue.TryAdd(message, 0, cancellationToken);

        if (added)
        {
            Interlocked.Increment(ref _totalEnqueued);
            _logger.LogTrace("Enqueued message to CPU queue (count: {Count})", _queue.Count);
        }
        else
        {
            Interlocked.Increment(ref _totalDropped);
            _logger.LogTrace("Failed to enqueue message - queue full (capacity: {Capacity})", _capacity);
        }

        return Task.FromResult(added);
    }

    /// <inheritdoc/>
    public Task<KernelMessage<T>?> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        var taken = _queue.TryTake(out var message, 0, cancellationToken);

        if (taken)
        {
            Interlocked.Increment(ref _totalDequeued);
            _logger.LogTrace("Dequeued message from CPU queue (count: {Count})", _queue.Count);
            return Task.FromResult<KernelMessage<T>?>(message);
        }

        return Task.FromResult<KernelMessage<T>?>(null);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(KernelMessage<T> message, TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        if (timeout == default)
        {
            _queue.Add(message, cancellationToken);
        }
        else
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                _queue.Add(message, cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Enqueue operation timed out after {timeout.TotalMilliseconds}ms");
            }
        }

        Interlocked.Increment(ref _totalEnqueued);
        _logger.LogTrace("Blocking enqueue completed (count: {Count})", _queue.Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<KernelMessage<T>> DequeueAsync(TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        KernelMessage<T> message;

        if (timeout == default)
        {
            message = await Task.Run(() => _queue.Take(cancellationToken), cancellationToken);
        }
        else
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                message = await Task.Run(() => _queue.Take(cts.Token), cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Dequeue operation timed out after {timeout.TotalMilliseconds}ms");
            }
        }

        Interlocked.Increment(ref _totalDequeued);
        _logger.LogTrace("Blocking dequeue completed (count: {Count})", _queue.Count);
        return message;
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        // BlockingCollection doesn't have a clear method, so we drain it
        while (_queue.TryTake(out _))
        {
            // Just discard messages
        }

        _logger.LogInformation("CPU message queue cleared");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<MessageQueueStatistics> GetStatisticsAsync()
    {
        ThrowIfNotInitialized();

        var totalMessages = Interlocked.Read(ref _totalEnqueued);
        var utilization = totalMessages > 0 ? (_queue.Count / (double)_capacity) : 0.0;
        var uptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds;
        var totalDequeued = Interlocked.Read(ref _totalDequeued);

        var stats = new MessageQueueStatistics
        {
            TotalEnqueued = totalMessages,
            TotalDequeued = totalDequeued,
            TotalDropped = Interlocked.Read(ref _totalDropped),
            Utilization = utilization,
            EnqueueThroughput = uptimeSeconds > 0 ? totalMessages / uptimeSeconds : 0,
            DequeueThroughput = uptimeSeconds > 0 ? totalDequeued / uptimeSeconds : 0,
            AverageLatencyUs = 0 // Requires per-message timestamp tracking
        };

        return Task.FromResult(stats);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _queue.CompleteAdding();
        _queue.Dispose();

        _logger.LogInformation(
            "CPU message queue disposed - enqueued: {Enqueued}, dequeued: {Dequeued}, dropped: {Dropped}",
            _totalEnqueued, _totalDequeued, _totalDropped);

        return ValueTask.CompletedTask;
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Message queue not initialized. Call InitializeAsync first.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuMessageQueue<T>));
        }
    }
}
