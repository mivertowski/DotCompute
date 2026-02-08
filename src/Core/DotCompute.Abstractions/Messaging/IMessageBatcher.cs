// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Messaging;

/// <summary>
/// Batches multiple messages together for efficient transmission and processing.
/// </summary>
/// <remarks>
/// <para><b>Features</b>:</para>
/// <list type="bullet">
/// <item>Configurable batch size limits</item>
/// <item>Time-based batch flushing</item>
/// <item>Memory-efficient batching with pooled buffers</item>
/// <item>Support for heterogeneous message types</item>
/// </list>
/// </remarks>
public interface IMessageBatcher : IAsyncDisposable
{
    /// <summary>
    /// Gets the current number of messages in the batch.
    /// </summary>
    public int CurrentBatchSize { get; }

    /// <summary>
    /// Gets the current total payload size in bytes.
    /// </summary>
    public long CurrentPayloadSize { get; }

    /// <summary>
    /// Gets a value indicating whether the batch is ready to flush based on configured triggers.
    /// </summary>
    public bool IsReadyToFlush { get; }

    /// <summary>
    /// Adds a message to the current batch.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task that completes when the message is added.
    /// Returns true if the batch is ready to flush after adding.
    /// </returns>
    public Task<bool> AddAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage;

    /// <summary>
    /// Flushes the current batch and returns the batched payload.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batched messages as a single payload.</returns>
    public Task<MessageBatch> FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Occurs when a batch is automatically flushed due to timeout.
    /// </summary>
    public event EventHandler<BatchFlushedEventArgs>? BatchFlushed;
}

/// <summary>
/// Configuration options for message batching.
/// </summary>
public sealed class BatchingOptions
{
    /// <summary>
    /// Gets or sets the maximum number of messages per batch.
    /// Default: 100.
    /// </summary>
    public int MaxBatchSize { get; init; } = 100;

    /// <summary>
    /// Gets or sets the maximum payload size per batch in bytes.
    /// Default: 1MB.
    /// </summary>
    public long MaxPayloadBytes { get; init; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum time to wait before flushing a batch.
    /// Default: 100ms.
    /// </summary>
    public TimeSpan MaxBatchDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets whether to enable automatic time-based flushing.
    /// Default: true.
    /// </summary>
    public bool EnableTimeBasedFlushing { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to enable compression for batched payloads.
    /// Default: false.
    /// </summary>
    public bool EnableCompression { get; init; }

    /// <summary>
    /// Gets or sets the compression threshold in bytes.
    /// Only compress batches larger than this size.
    /// Default: 1KB.
    /// </summary>
    public long CompressionThreshold { get; init; } = 1024;

    /// <summary>
    /// Default batching options.
    /// </summary>
    public static BatchingOptions Default { get; } = new();
}

/// <summary>
/// Represents a batch of messages ready for transmission.
/// </summary>
public sealed class MessageBatch : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the unique identifier for this batch.
    /// </summary>
    public Guid BatchId { get; }

    /// <summary>
    /// Gets the number of messages in the batch.
    /// </summary>
    public int MessageCount { get; }

    /// <summary>
    /// Gets the total payload size in bytes.
    /// </summary>
    public long PayloadSize { get; }

    /// <summary>
    /// Gets when the batch was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets when the batch was flushed.
    /// </summary>
    public DateTimeOffset FlushedAt { get; }

    /// <summary>
    /// Gets the serialized batch payload.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Gets the message type information for deserializing the batch.
    /// </summary>
    public IReadOnlyList<BatchedMessageInfo> MessageInfos { get; }

    /// <summary>
    /// Gets whether the payload is compressed.
    /// </summary>
    public bool IsCompressed { get; }

    /// <summary>
    /// Creates a new message batch.
    /// </summary>
    public MessageBatch(
        Guid batchId,
        int messageCount,
        long payloadSize,
        DateTimeOffset createdAt,
        DateTimeOffset flushedAt,
        ReadOnlyMemory<byte> payload,
        IReadOnlyList<BatchedMessageInfo> messageInfos,
        bool isCompressed = false)
    {
        BatchId = batchId;
        MessageCount = messageCount;
        PayloadSize = payloadSize;
        CreatedAt = createdAt;
        FlushedAt = flushedAt;
        Payload = payload;
        MessageInfos = messageInfos;
        IsCompressed = isCompressed;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Payload memory is managed by the caller
    }
}

/// <summary>
/// Information about a single message within a batch.
/// </summary>
public readonly record struct BatchedMessageInfo
{
    /// <summary>
    /// Gets the message identifier.
    /// </summary>
    public required Guid MessageId { get; init; }

    /// <summary>
    /// Gets the message type identifier.
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Gets the offset within the batch payload.
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    /// Gets the length of this message within the payload.
    /// </summary>
    public required int Length { get; init; }
}

/// <summary>
/// Event arguments for batch flushed events.
/// </summary>
public sealed class BatchFlushedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the batch that was flushed.
    /// </summary>
    public required MessageBatch Batch { get; init; }

    /// <summary>
    /// Gets the reason the batch was flushed.
    /// </summary>
    public required BatchFlushReason Reason { get; init; }
}

/// <summary>
/// Reason why a batch was flushed.
/// </summary>
public enum BatchFlushReason
{
    /// <summary>
    /// Manual flush request.
    /// </summary>
    Manual,

    /// <summary>
    /// Maximum message count reached.
    /// </summary>
    MaxCountReached,

    /// <summary>
    /// Maximum payload size reached.
    /// </summary>
    MaxSizeReached,

    /// <summary>
    /// Maximum delay time reached.
    /// </summary>
    TimeoutExpired,

    /// <summary>
    /// Batcher is being disposed.
    /// </summary>
    Disposal
}

/// <summary>
/// Aggregates responses from multiple requests into a single result.
/// </summary>
/// <typeparam name="TResult">The aggregated result type.</typeparam>
public interface IMessageAggregator<TResult> : IAsyncDisposable
{
    /// <summary>
    /// Gets the number of expected responses.
    /// </summary>
    public int ExpectedCount { get; }

    /// <summary>
    /// Gets the number of received responses.
    /// </summary>
    public int ReceivedCount { get; }

    /// <summary>
    /// Gets a value indicating whether all expected responses have been received.
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>
    /// Gets a task that completes when aggregation is complete or times out.
    /// </summary>
    public Task<TResult> AggregatedResult { get; }

    /// <summary>
    /// Adds a response to the aggregation.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="response">The response to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if aggregation is complete after adding.</returns>
    public Task<bool> AddResponseAsync<TResponse>(TResponse response, CancellationToken cancellationToken = default)
        where TResponse : IResponseMessage;

    /// <summary>
    /// Marks a specific request as failed.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the failed request.</param>
    /// <param name="exception">The exception that occurred.</param>
    public void MarkFailed(Guid correlationId, Exception exception);

    /// <summary>
    /// Cancels the aggregation and completes with partial results.
    /// </summary>
    public void Cancel();
}

/// <summary>
/// Factory for creating message aggregators.
/// </summary>
public interface IMessageAggregatorFactory
{
    /// <summary>
    /// Creates a new aggregator that combines responses into a single result.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="TResult">The aggregated result type.</typeparam>
    /// <param name="expectedCount">Number of expected responses.</param>
    /// <param name="aggregateFunc">Function to aggregate responses.</param>
    /// <param name="timeout">Timeout for aggregation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new message aggregator.</returns>
    public IMessageAggregator<TResult> CreateAggregator<TResponse, TResult>(
        int expectedCount,
        Func<IReadOnlyList<TResponse>, TResult> aggregateFunc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TResponse : IResponseMessage;

    /// <summary>
    /// Creates a new aggregator that waits for all responses.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="expectedCount">Number of expected responses.</param>
    /// <param name="timeout">Timeout for aggregation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new message aggregator that returns all responses.</returns>
    public IMessageAggregator<IReadOnlyList<TResponse>> CreateWaitAll<TResponse>(
        int expectedCount,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TResponse : IResponseMessage;

    /// <summary>
    /// Creates a new aggregator that returns the first successful response.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="expectedCount">Number of expected responses.</param>
    /// <param name="timeout">Timeout for aggregation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new message aggregator that returns the first success.</returns>
    public IMessageAggregator<TResponse?> CreateFirstSuccess<TResponse>(
        int expectedCount,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TResponse : class, IResponseMessage;
}

/// <summary>
/// Configuration options for message aggregation.
/// </summary>
public sealed record AggregationOptions
{
    /// <summary>
    /// Gets or sets the default timeout for aggregation.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to fail fast on first error.
    /// Default: false.
    /// </summary>
    public bool FailFastOnError { get; init; }

    /// <summary>
    /// Gets or sets the minimum number of successful responses required.
    /// Default: 0 (all must succeed).
    /// </summary>
    public int MinimumSuccessCount { get; init; }

    /// <summary>
    /// Gets or sets whether to include partial results on timeout.
    /// Default: true.
    /// </summary>
    public bool IncludePartialResults { get; init; } = true;

    /// <summary>
    /// Default aggregation options.
    /// </summary>
    public static AggregationOptions Default { get; } = new();
}
