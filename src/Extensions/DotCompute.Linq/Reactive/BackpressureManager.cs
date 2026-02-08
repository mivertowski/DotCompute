// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Reactive;

/// <summary>
/// Manages backpressure for streaming compute scenarios to prevent memory exhaustion.
/// </summary>
/// <remarks>
/// Phase 7: Reactive Extensions - Production implementation.
/// Backpressure strategies:
/// - Buffer: Queue up to N items (default 10000)
/// - Drop Latest: Discard new items when buffer full
/// - Drop Oldest: Discard old items when buffer full
/// - Block: Block producer thread until space available
/// - Sample: Keep only most recent item
/// </remarks>
public sealed class BackpressureManager : IBackpressureManager, IDisposable
{
    private readonly ILogger<BackpressureManager> _logger;

    // Backpressure state
    private BackpressureStrategy _currentStrategy = BackpressureStrategy.Buffer;
    private readonly ConcurrentQueue<object> _buffer = new();
    private readonly object _blockLock = new();
    private bool _isBlocked;
    private int _droppedCount;
    private readonly int _maxBufferSize = 10000;

    // Sampling state
    private object? _sampledItem;
    private readonly object _sampleLock = new();

    // Statistics
    private long _totalItemsProcessed;
    private long _totalItemsDropped;
    private readonly CancellationTokenSource _cancellationSource = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BackpressureManager"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    /// <param name="maxBufferSize">The maximum buffer size (default 10000).</param>
    public BackpressureManager(ILogger<BackpressureManager> logger, int maxBufferSize = 10000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (maxBufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBufferSize),
                "Maximum buffer size must be positive");
        }

        _maxBufferSize = maxBufferSize;

        _logger.LogInformation("BackpressureManager initialized with buffer size {BufferSize}",
            maxBufferSize);
    }

    /// <summary>
    /// Applies a backpressure strategy to control flow rate.
    /// </summary>
    /// <param name="strategy">The backpressure strategy to apply.</param>
    public void ApplyBackpressure(BackpressureStrategy strategy)
    {
        if (_currentStrategy != strategy)
        {
            _logger.LogInformation("Changing backpressure strategy from {OldStrategy} to {NewStrategy}",
                _currentStrategy, strategy);

            _currentStrategy = strategy;

            // Clear buffer when switching strategies
            if (strategy == BackpressureStrategy.Sample)
            {
                ClearBufferExceptLatest();
            }
        }
    }

    /// <summary>
    /// Gets the current backpressure state.
    /// </summary>
    /// <returns>The current backpressure state.</returns>
    public BackpressureState GetState()
    {
        var queuedItems = _buffer.Count;
        var utilization = (double)queuedItems / _maxBufferSize * 100.0;

        return new BackpressureState
        {
            QueuedItems = queuedItems,
            IsBlocked = _isBlocked,
            DroppedCount = _droppedCount,
            BufferUtilization = utilization
        };
    }

    #region Item Management

    /// <summary>
    /// Attempts to enqueue an item according to the current backpressure strategy.
    /// </summary>
    /// <typeparam name="T">The type of item to enqueue.</typeparam>
    /// <param name="item">The item to enqueue.</param>
    /// <returns>True if the item was enqueued; false if it was dropped.</returns>
    public bool TryEnqueue<T>(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Interlocked.Increment(ref _totalItemsProcessed);

        switch (_currentStrategy)
        {
            case BackpressureStrategy.Buffer:
                return HandleBuffer(item);

            case BackpressureStrategy.Drop:
            case BackpressureStrategy.DropLatest:
                return HandleDropLatest(item);

            case BackpressureStrategy.DropOldest:
                return HandleDropOldest(item);

            case BackpressureStrategy.Block:
                return HandleBlock(item);

            case BackpressureStrategy.Sample:
                return HandleSample(item);

            default:
                _logger.LogWarning("Unknown backpressure strategy {Strategy}, using Buffer",
                    _currentStrategy);
                return HandleBuffer(item);
        }
    }

    /// <summary>
    /// Attempts to dequeue an item from the buffer.
    /// </summary>
    /// <typeparam name="T">The type of item to dequeue.</typeparam>
    /// <param name="item">The dequeued item.</param>
    /// <returns>True if an item was dequeued; false if the buffer is empty.</returns>
    public bool TryDequeue<T>(out T? item)
    {
        // For sample strategy, return the sampled item
        if (_currentStrategy == BackpressureStrategy.Sample)
        {
            lock (_sampleLock)
            {
                if (_sampledItem != null && _sampledItem is T sampledValue)
                {
                    item = sampledValue;
                    _sampledItem = null;
                    return true;
                }
            }

            item = default;
            return false;
        }

        // For other strategies, dequeue from buffer
        if (_buffer.TryDequeue(out var obj) && obj is T value)
        {
            item = value;

            // Unblock if buffer has space
            if (_buffer.Count < _maxBufferSize * 0.8)
            {
                UnblockProducer();
            }

            return true;
        }

        item = default;
        return false;
    }

    #endregion

    #region Strategy Implementations

    /// <summary>
    /// Handles Buffer strategy - queue items up to max buffer size.
    /// </summary>
    private bool HandleBuffer<T>(T item)
    {
        if (_buffer.Count >= _maxBufferSize)
        {
            _logger.LogWarning("Buffer full ({Count} items), dropping item", _buffer.Count);
            Interlocked.Increment(ref _droppedCount);
            Interlocked.Increment(ref _totalItemsDropped);
            return false;
        }

        _buffer.Enqueue(item!);
        return true;
    }

    /// <summary>
    /// Handles DropLatest strategy - drop new items when buffer is full.
    /// </summary>
    private bool HandleDropLatest<T>(T item)
    {
        if (_buffer.Count >= _maxBufferSize)
        {
            _logger.LogDebug("Buffer full, dropping latest item");
            Interlocked.Increment(ref _droppedCount);
            Interlocked.Increment(ref _totalItemsDropped);
            return false;
        }

        _buffer.Enqueue(item!);
        return true;
    }

    /// <summary>
    /// Handles DropOldest strategy - drop old items to make room for new ones.
    /// </summary>
    private bool HandleDropOldest<T>(T item)
    {
        if (_buffer.Count >= _maxBufferSize)
        {
            // Remove oldest item
            if (_buffer.TryDequeue(out _))
            {
                _logger.LogDebug("Buffer full, dropped oldest item");
                Interlocked.Increment(ref _droppedCount);
                Interlocked.Increment(ref _totalItemsDropped);
            }
        }

        _buffer.Enqueue(item!);
        return true;
    }

    /// <summary>
    /// Handles Block strategy - block producer thread when buffer is full.
    /// </summary>
    private bool HandleBlock<T>(T item)
    {
        // Block if buffer is full
        while (_buffer.Count >= _maxBufferSize && !_cancellationSource.Token.IsCancellationRequested)
        {
            lock (_blockLock)
            {
                if (!_isBlocked)
                {
                    _isBlocked = true;
                    _logger.LogWarning("Buffer full ({Count} items), blocking producer",
                        _buffer.Count);
                }

                Monitor.Wait(_blockLock, TimeSpan.FromMilliseconds(100));
            }
        }

        if (_cancellationSource.Token.IsCancellationRequested)
        {
            return false;
        }

        _buffer.Enqueue(item!);
        return true;
    }

    /// <summary>
    /// Handles Sample strategy - keep only the most recent item.
    /// </summary>
    private bool HandleSample<T>(T item)
    {
        lock (_sampleLock)
        {
            if (_sampledItem != null)
            {
                Interlocked.Increment(ref _droppedCount);
                Interlocked.Increment(ref _totalItemsDropped);
                _logger.LogTrace("Replacing sampled item with newer item");
            }

            _sampledItem = item;
        }

        return true;
    }

    /// <summary>
    /// Unblocks the producer thread if blocked.
    /// </summary>
    private void UnblockProducer()
    {
        lock (_blockLock)
        {
            if (_isBlocked)
            {
                _isBlocked = false;
                _logger.LogDebug("Buffer space available, unblocking producer");
                Monitor.PulseAll(_blockLock);
            }
        }
    }

    /// <summary>
    /// Clears the buffer except for the latest item (for Sample strategy).
    /// </summary>
    private void ClearBufferExceptLatest()
    {
        object? lastItem = null;

        while (_buffer.TryDequeue(out var item))
        {
            lastItem = item;
        }

        if (lastItem != null)
        {
            _sampledItem = lastItem;
        }

        _logger.LogDebug("Cleared buffer for Sample strategy");
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets statistics about backpressure management.
    /// </summary>
    /// <returns>A dictionary of statistics.</returns>
    public System.Collections.Generic.Dictionary<string, object> GetStatistics()
    {
        return new System.Collections.Generic.Dictionary<string, object>
        {
            ["Strategy"] = _currentStrategy.ToString(),
            ["BufferSize"] = _buffer.Count,
            ["MaxBufferSize"] = _maxBufferSize,
            ["TotalProcessed"] = _totalItemsProcessed,
            ["TotalDropped"] = _totalItemsDropped,
            ["CurrentDropped"] = _droppedCount,
            ["IsBlocked"] = _isBlocked,
            ["BufferUtilization"] = (double)_buffer.Count / _maxBufferSize * 100.0
        };
    }

    /// <summary>
    /// Resets statistics counters.
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _droppedCount, 0);
        _logger.LogDebug("Statistics reset");
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes resources used by the backpressure manager.
    /// </summary>
    public void Dispose()
    {
        _cancellationSource.Cancel();
        _cancellationSource.Dispose();

        // Unblock any waiting threads
        UnblockProducer();

        _logger.LogInformation("BackpressureManager disposed. " +
            "Total processed: {Processed}, Total dropped: {Dropped}",
            _totalItemsProcessed, _totalItemsDropped);
    }

    #endregion
}
