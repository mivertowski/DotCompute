// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Messaging;

/// <summary>
/// Batches multiple messages together for efficient transmission.
/// </summary>
public sealed partial class MessageBatcher : IMessageBatcher
{
    private readonly BatchingOptions _options;
    private readonly ILogger<MessageBatcher> _logger;
    private readonly object _lock = new();
    private readonly List<BatchedMessageEntry> _entries = new();
    private readonly Timer? _flushTimer;
    private DateTimeOffset _batchCreatedAt;
    private long _currentPayloadSize;
    private bool _disposed;

    // Event IDs: 9300-9399 for MessageBatcher
    [LoggerMessage(EventId = 9300, Level = LogLevel.Debug,
        Message = "Message added to batch. Count: {Count}, Size: {Size} bytes")]
    private static partial void LogMessageAdded(ILogger logger, int count, long size);

    [LoggerMessage(EventId = 9301, Level = LogLevel.Debug,
        Message = "Batch flushed: {BatchId}. Messages: {Count}, Size: {Size} bytes, Reason: {Reason}")]
    private static partial void LogBatchFlushed(ILogger logger, Guid batchId, int count, long size, BatchFlushReason reason);

    [LoggerMessage(EventId = 9302, Level = LogLevel.Warning,
        Message = "Batch timeout triggered after {Delay}ms")]
    private static partial void LogBatchTimeout(ILogger logger, double delay);

    /// <inheritdoc />
    public int CurrentBatchSize
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <inheritdoc />
    public long CurrentPayloadSize
    {
        get
        {
            lock (_lock)
            {
                return _currentPayloadSize;
            }
        }
    }

    /// <inheritdoc />
    public bool IsReadyToFlush
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count >= _options.MaxBatchSize ||
                       _currentPayloadSize >= _options.MaxPayloadBytes;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<BatchFlushedEventArgs>? BatchFlushed;

    /// <summary>
    /// Creates a new message batcher with default options.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public MessageBatcher(ILogger<MessageBatcher> logger)
        : this(BatchingOptions.Default, logger)
    {
    }

    /// <summary>
    /// Creates a new message batcher with the specified options.
    /// </summary>
    /// <param name="options">Batching options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public MessageBatcher(BatchingOptions options, ILogger<MessageBatcher> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _batchCreatedAt = DateTimeOffset.UtcNow;

        if (_options.EnableTimeBasedFlushing && _options.MaxBatchDelay > TimeSpan.Zero)
        {
            _flushTimer = new Timer(
                OnFlushTimerElapsed,
                null,
                _options.MaxBatchDelay,
                Timeout.InfiniteTimeSpan);
        }
    }

    /// <inheritdoc />
    public Task<bool> AddAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        lock (_lock)
        {
            var serialized = message.Serialize().ToArray();
            var entry = new BatchedMessageEntry(
                message.MessageId,
                message.MessageType,
                serialized);

            _entries.Add(entry);
            _currentPayloadSize += serialized.Length;

            LogMessageAdded(_logger, _entries.Count, _currentPayloadSize);

            var shouldFlush = _entries.Count >= _options.MaxBatchSize ||
                              _currentPayloadSize >= _options.MaxPayloadBytes;

            return Task.FromResult(shouldFlush);
        }
    }

    /// <inheritdoc />
    public Task<MessageBatch> FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.FromResult(FlushInternal(BatchFlushReason.Manual));
    }

    private MessageBatch FlushInternal(BatchFlushReason reason)
    {
        lock (_lock)
        {
            var batchId = Guid.NewGuid();
            var messageInfos = new List<BatchedMessageInfo>();
            var totalSize = CalculateTotalSize();
            var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Max(totalSize, 1));

            try
            {
                var offset = 0;

                foreach (var entry in _entries)
                {
                    messageInfos.Add(new BatchedMessageInfo
                    {
                        MessageId = entry.MessageId,
                        MessageType = entry.MessageType,
                        Offset = offset,
                        Length = entry.Data.Length
                    });

                    entry.Data.CopyTo(buffer, offset);
                    offset += entry.Data.Length;
                }

                var payload = new byte[offset];
                Array.Copy(buffer, payload, offset);

                var batch = new MessageBatch(
                    batchId,
                    _entries.Count,
                    offset,
                    _batchCreatedAt,
                    DateTimeOffset.UtcNow,
                    payload,
                    messageInfos);

                LogBatchFlushed(_logger, batchId, batch.MessageCount, batch.PayloadSize, reason);

                // Raise event
                BatchFlushed?.Invoke(this, new BatchFlushedEventArgs
                {
                    Batch = batch,
                    Reason = reason
                });

                // Reset batch state
                _entries.Clear();
                _currentPayloadSize = 0;
                _batchCreatedAt = DateTimeOffset.UtcNow;

                // Reset timer
                ResetFlushTimer();

                return batch;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private long CalculateTotalSize()
    {
        long total = 0;
        foreach (var entry in _entries)
        {
            total += entry.Data.Length;
        }

        return total;
    }

    private void OnFlushTimerElapsed(object? state)
    {
        if (_disposed)
        {
            return;
        }

        bool hasMessages;
        lock (_lock)
        {
            hasMessages = _entries.Count > 0;
        }

        if (hasMessages)
        {
            LogBatchTimeout(_logger, _options.MaxBatchDelay.TotalMilliseconds);
            FlushInternal(BatchFlushReason.TimeoutExpired);
        }
        else
        {
            // Reset timer for next cycle
            ResetFlushTimer();
        }
    }

    private void ResetFlushTimer()
    {
        _flushTimer?.Change(_options.MaxBatchDelay, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop the timer
        if (_flushTimer != null)
        {
            await _flushTimer.DisposeAsync();
        }

        // Flush any remaining messages
        lock (_lock)
        {
            if (_entries.Count > 0)
            {
                FlushInternal(BatchFlushReason.Disposal);
            }
        }
    }

    private sealed class BatchedMessageEntry
    {
        public Guid MessageId { get; }
        public string MessageType { get; }
        public byte[] Data { get; }

        public BatchedMessageEntry(Guid messageId, string messageType, byte[] data)
        {
            MessageId = messageId;
            MessageType = messageType;
            Data = data;
        }
    }
}
