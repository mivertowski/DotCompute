// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Configuration;
using DotCompute.Abstractions.Pipelines.Execution;

namespace DotCompute.Abstractions.Pipelines.Streaming;

/// <summary>
/// Specialized execution context for streaming pipeline operations.
/// </summary>
/// <remarks>
/// <para>
/// Extends the base pipeline execution context with streaming-specific capabilities
/// including buffer management, flow control, and event publishing.
/// </para>
/// <para>
/// Streaming contexts are optimized for processing continuous data flows with
/// backpressure handling and configurable buffering strategies.
/// </para>
/// </remarks>
public interface IStreamingExecutionContext : IPipelineExecutionContext
{
    /// <summary>Configuration for streaming behavior.</summary>
    public StreamingConfiguration StreamingConfig { get; }

    // TODO: Define missing type
    /* <summary>Buffer manager for streaming data.</summary>
    IStreamingBufferManager BufferManager { get; } */

    // TODO: Define missing type
    /* <summary>Flow control for managing streaming throughput.</summary>
    IStreamFlowControl FlowControl { get; } */

    // TODO: Define missing type
    /* <summary>Event publisher for streaming events.</summary>
    IStreamEventPublisher EventPublisher { get; } */
}

/// <summary>
/// Configuration for streaming pipeline execution.
/// </summary>
/// <remarks>
/// <para>
/// Defines streaming behavior including buffer sizing, concurrency limits,
/// item timeouts, order preservation, backpressure strategies, and error handling.
/// </para>
/// <para>
/// These settings directly impact streaming throughput, memory usage, and
/// fault tolerance characteristics.
/// </para>
/// </remarks>
public class StreamingConfiguration
{
    /// <summary>Size of internal buffers for streaming operations.</summary>
    /// <remarks>
    /// Larger buffers improve throughput but increase memory usage and latency.
    /// Default value of 1024 provides good balance for most workloads.
    /// </remarks>
    public int BufferSize { get; set; } = 1024;

    /// <summary>Maximum number of items to process in parallel.</summary>
    /// <remarks>
    /// Defaults to processor count for optimal CPU utilization.
    /// Adjust based on workload characteristics (CPU vs I/O bound).
    /// </remarks>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

    /// <summary>Timeout for individual item processing.</summary>
    /// <remarks>
    /// Items that exceed this timeout may be retried, skipped, or cause
    /// stream termination depending on ErrorHandling configuration.
    /// </remarks>
    public TimeSpan ItemTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Whether to preserve order in streaming results.</summary>
    /// <remarks>
    /// Order preservation adds overhead but ensures deterministic output.
    /// Disable for better performance when order doesn't matter.
    /// </remarks>
    public bool PreserveOrder { get; set; } = true;

    /// <summary>Backpressure handling strategy.</summary>
    /// <remarks>
    /// Determines behavior when consumers cannot keep up with producers.
    /// Choose strategy based on latency tolerance and memory constraints.
    /// </remarks>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Buffer;

    /// <summary>Error handling for streaming operations.</summary>
    /// <remarks>
    /// Defines how individual item errors affect overall stream processing.
    /// Skip is safest default, ensuring stream continues despite failures.
    /// </remarks>
    public StreamingErrorHandling ErrorHandling { get; set; } = StreamingErrorHandling.Skip;
}

/// <summary>
/// Backpressure handling strategies for streaming operations.
/// </summary>
/// <remarks>
/// <para>
/// Backpressure mechanisms prevent memory exhaustion when producers
/// generate data faster than consumers can process it.
/// </para>
/// <para>
/// Strategy selection depends on whether data loss is acceptable and
/// whether blocking producers is feasible.
/// </para>
/// </remarks>
public enum BackpressureStrategy
{
    /// <summary>Buffer items when consumer is slower than producer.</summary>
    /// <remarks>
    /// Safe but can lead to unbounded memory growth. Monitor buffer sizes.
    /// </remarks>
    Buffer,

    /// <summary>Drop oldest items when buffer is full.</summary>
    /// <remarks>
    /// Preserves recent data, useful for real-time data where old data becomes irrelevant.
    /// </remarks>
    DropOldest,

    /// <summary>Drop newest items when buffer is full.</summary>
    /// <remarks>
    /// Preserves historical data, useful when all data must be processed eventually.
    /// </remarks>
    DropNewest,

    /// <summary>Block producer when buffer is full.</summary>
    /// <remarks>
    /// Prevents data loss but can cause producer starvation and reduced throughput.
    /// </remarks>
    Block,

    /// <summary>Throw exception when buffer is full.</summary>
    /// <remarks>
    /// Fails fast, alerting operators to capacity issues. Use with monitoring.
    /// </remarks>
    Fail
}

/// <summary>
/// Error handling strategies specific to streaming operations.
/// </summary>
/// <remarks>
/// Determines how errors in processing individual stream items
/// affect overall stream processing and downstream consumers.
/// </remarks>
public enum StreamingErrorHandling
{
    /// <summary>Skip items that cause errors and continue processing.</summary>
    /// <remarks>
    /// Best for fault tolerance. Ensure error logging to detect patterns.
    /// </remarks>
    Skip,

    /// <summary>Stop the entire stream on first error.</summary>
    /// <remarks>
    /// Strictest error handling. Use when data integrity is critical.
    /// </remarks>
    Stop,

    /// <summary>Retry failed items with backoff.</summary>
    /// <remarks>
    /// Handles transient failures. Configure retry limits to prevent infinite loops.
    /// </remarks>
    Retry,

    /// <summary>Send failed items to dead letter queue.</summary>
    /// <remarks>
    /// Allows offline analysis and reprocessing of failed items.
    /// </remarks>
    DeadLetter
}
