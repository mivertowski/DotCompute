// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;

namespace DotCompute.Abstractions.RingKernels;

/// <summary>
/// Configuration options for launching a ring kernel.
/// </summary>
/// <remarks>
/// <para>
/// Ring kernels are persistent GPU kernels that process messages from input queues
/// and produce results in output queues. This class provides comprehensive configuration
/// for queue sizing, deduplication, backpressure, and performance tuning.
/// </para>
/// <para><b>Default Values</b></para>
/// <list type="bullet">
/// <item><description><b>QueueCapacity</b>: 4096 messages (optimized for high throughput)</description></item>
/// <item><description><b>DeduplicationWindowSize</b>: 1024 messages (maximum validated size)</description></item>
/// <item><description><b>BackpressureStrategy</b>: Block (wait for space)</description></item>
/// <item><description><b>EnablePriorityQueue</b>: false (FIFO ordering)</description></item>
/// </list>
/// </remarks>
public sealed class RingKernelLaunchOptions
{
    /// <summary>
    /// Default queue capacity for ring kernel message queues (4096 messages).
    /// </summary>
    /// <remarks>
    /// 4096 provides a good balance between memory usage and throughput:
    /// - Memory per queue: ~128KB for IRingKernelMessage types
    /// - Supports 2M+ messages/s throughput with 100-500ns latency
    /// - Power-of-2 for optimal modulo operations
    /// </remarks>
    public const int DefaultQueueCapacity = 4096;

    /// <summary>
    /// Default deduplication window size (1024 messages).
    /// </summary>
    /// <remarks>
    /// 1024 is the maximum validated size that balances:
    /// - Memory usage: ~32KB deduplication cache per queue
    /// - Coverage: Detects duplicates within last 1024 messages
    /// - Performance: O(1) lookup via hash table
    /// </remarks>
    public const int DefaultDeduplicationWindowSize = 1024;

    /// <summary>
    /// Gets or sets the maximum number of messages each queue can hold.
    /// </summary>
    /// <value>
    /// The queue capacity in messages. Default is 4096.
    /// Must be a power of 2 for optimal performance (16, 32, 64, ..., 65536).
    /// </value>
    /// <remarks>
    /// <para><b>Sizing Guidelines</b></para>
    /// <list type="bullet">
    /// <item><description><b>Low latency</b> (sub-microsecond): 256-1024</description></item>
    /// <item><description><b>Balanced</b> (production default): 4096</description></item>
    /// <item><description><b>High throughput</b> (batch processing): 16384-65536</description></item>
    /// </list>
    /// <para>
    /// Larger queues consume more memory but provide better burst handling.
    /// Memory usage: ~32 bytes × capacity for IRingKernelMessage types.
    /// </para>
    /// </remarks>
    public int QueueCapacity { get; set; } = DefaultQueueCapacity;

    /// <summary>
    /// Gets or sets the number of recent messages to check for duplicates.
    /// </summary>
    /// <value>
    /// The deduplication window size in messages. Default is 1024.
    /// Valid range: 16-1024 (enforced by MessageQueueOptions.Validate()).
    /// </value>
    /// <remarks>
    /// <para><b>Deduplication Behavior</b></para>
    /// <list type="bullet">
    /// <item><description>Messages with duplicate MessageId within window are rejected</description></item>
    /// <item><description>Implemented via circular buffer hash table (O(1) lookup)</description></item>
    /// <item><description>Window size affects memory: ~32 bytes × window size per queue</description></item>
    /// </list>
    /// <para><b>Sizing Trade-offs</b></para>
    /// <list type="bullet">
    /// <item><description><b>Smaller window</b> (16-256): Lower memory, faster duplicates may pass</description></item>
    /// <item><description><b>Larger window</b> (512-1024): Higher memory, better duplicate detection</description></item>
    /// </list>
    /// <para>
    /// <b>Note</b>: Deduplication window size is clamped to QueueCapacity if QueueCapacity &lt; 1024.
    /// For high-capacity queues (&gt;1024), deduplication covers the most recent 1024 messages.
    /// </para>
    /// </remarks>
    public int DeduplicationWindowSize { get; set; } = DefaultDeduplicationWindowSize;

    /// <summary>
    /// Gets or sets the backpressure strategy when queues are full.
    /// </summary>
    /// <value>
    /// The backpressure strategy. Default is <see cref="BackpressureStrategy.Block"/>.
    /// </value>
    /// <remarks>
    /// <para><b>Strategy Comparison</b></para>
    /// <list type="table">
    /// <listheader>
    /// <term>Strategy</term>
    /// <description>Behavior</description>
    /// </listheader>
    /// <item>
    /// <term>Block</term>
    /// <description>Wait for space (best for guaranteed delivery)</description>
    /// </item>
    /// <item>
    /// <term>Reject</term>
    /// <description>Return false immediately (best for latency-sensitive)</description>
    /// </item>
    /// <item>
    /// <term>DropOldest</term>
    /// <description>Overwrite oldest message (best for real-time streams)</description>
    /// </item>
    /// <item>
    /// <term>DropNew</term>
    /// <description>Discard new message (best for preserving historical data)</description>
    /// </item>
    /// </list>
    /// <para>
    /// <b>Production Recommendation</b>: Use <see cref="BackpressureStrategy.Block"/> for Orleans.GpuBridge
    /// to ensure actor requests are not lost during GPU computation.
    /// </para>
    /// </remarks>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Block;

    /// <summary>
    /// Gets or sets whether to use priority-based message ordering.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to enable priority queue; <see langword="false"/> for FIFO. Default is <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// <para><b>Priority Queue Behavior</b></para>
    /// <list type="bullet">
    /// <item><description>Messages dequeued in priority order (0 = highest, 255 = lowest)</description></item>
    /// <item><description>Same-priority messages dequeued in FIFO order</description></item>
    /// <item><description>Slight performance overhead: ~10-20% vs FIFO</description></item>
    /// </list>
    /// <para><b>Use Cases</b></para>
    /// <list type="bullet">
    /// <item><description><b>Enable</b>: Critical actor requests need priority over batch operations</description></item>
    /// <item><description><b>Disable</b>: Uniform priority, maximize throughput</description></item>
    /// </list>
    /// </remarks>
    public bool EnablePriorityQueue { get; set; }

    /// <summary>
    /// Gets or sets the CUDA stream priority for Ring Kernel execution.
    /// </summary>
    /// <value>
    /// The stream priority level. Default is <see cref="RingKernelStreamPriority.Normal"/>.
    /// </value>
    /// <remarks>
    /// <para><b>Stream Priority Behavior</b></para>
    /// <list type="bullet">
    /// <item><description><b>High</b>: GPU scheduler prioritizes this kernel for low-latency responses (use for critical operations)</description></item>
    /// <item><description><b>Normal</b>: Default priority for typical workloads</description></item>
    /// <item><description><b>Low</b>: Deprioritized for background processing that can tolerate higher latency</description></item>
    /// </list>
    /// <para><b>Use Cases</b></para>
    /// <list type="bullet">
    /// <item><description><b>High</b>: Actor request processing, real-time data streams, latency-sensitive operations</description></item>
    /// <item><description><b>Normal</b>: General purpose computation, balanced workloads</description></item>
    /// <item><description><b>Low</b>: Batch processing, background analytics, non-critical tasks</description></item>
    /// </list>
    /// <para>
    /// <b>Note</b>: Stream priority affects GPU scheduling but does not guarantee execution order.
    /// Higher priority streams get preferential access to GPU resources when multiple streams compete.
    /// </para>
    /// </remarks>
    public RingKernelStreamPriority StreamPriority { get; set; } = RingKernelStreamPriority.Normal;

    /// <summary>
    /// Validates the launch options and throws if any values are invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if:
    /// - QueueCapacity is less than 16 or greater than 1048576 (1M)
    /// - QueueCapacity is not a power of 2
    /// - DeduplicationWindowSize is less than 16 or greater than 1024
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method performs comprehensive validation before kernel launch:
    /// </para>
    /// <list type="number">
    /// <item><description><b>Queue Capacity</b>: 16 ≤ capacity ≤ 1M, power-of-2</description></item>
    /// <item><description><b>Deduplication Window</b>: 16 ≤ window ≤ 1024</description></item>
    /// <item><description><b>Consistency</b>: Window ≤ capacity (auto-clamped)</description></item>
    /// </list>
    /// <para>
    /// <b>Note</b>: DeduplicationWindowSize is automatically clamped to QueueCapacity
    /// if QueueCapacity &lt; DeduplicationWindowSize. This ensures smaller queues
    /// have proportional deduplication windows.
    /// </para>
    /// </remarks>
    public void Validate()
    {
        // Validate queue capacity
        if (QueueCapacity < 16 || QueueCapacity > 1048576)
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueueCapacity),
                QueueCapacity,
                "QueueCapacity must be between 16 and 1048576 (1M).");
        }

        // Ensure capacity is power of 2 for optimal ring buffer operations
        if ((QueueCapacity & (QueueCapacity - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueueCapacity),
                QueueCapacity,
                "QueueCapacity must be a power of 2 (16, 32, 64, 128, 256, 512, 1024, 2048, 4096, ...).");
        }

        // Validate deduplication window size
        if (DeduplicationWindowSize < 16 || DeduplicationWindowSize > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DeduplicationWindowSize),
                DeduplicationWindowSize,
                "DeduplicationWindowSize must be between 16 and 1024.");
        }

        // Auto-clamp deduplication window to capacity for small queues
        if (DeduplicationWindowSize > QueueCapacity)
        {
            DeduplicationWindowSize = QueueCapacity;
        }
    }

    /// <summary>
    /// Creates a <see cref="MessageQueueOptions"/> instance from these launch options.
    /// </summary>
    /// <returns>A new <see cref="MessageQueueOptions"/> with values from this instance.</returns>
    /// <remarks>
    /// This method is used internally by ring kernel runtimes to create message queues
    /// with the configured options. It ensures consistent translation from launch options
    /// to queue options.
    /// </remarks>
    public MessageQueueOptions ToMessageQueueOptions()
    {
        return new MessageQueueOptions
        {
            Capacity = QueueCapacity,
            DeduplicationWindowSize = DeduplicationWindowSize,
            BackpressureStrategy = BackpressureStrategy,
            EnablePriorityQueue = EnablePriorityQueue
        };
    }

    /// <summary>
    /// Creates a new instance with default values optimized for Orleans.GpuBridge production use.
    /// </summary>
    /// <returns>A new <see cref="RingKernelLaunchOptions"/> with production defaults.</returns>
    /// <remarks>
    /// <para><b>Production Defaults</b></para>
    /// <list type="bullet">
    /// <item><description><b>QueueCapacity</b>: 4096 (handles burst traffic, 2M+ msg/s)</description></item>
    /// <item><description><b>DeduplicationWindowSize</b>: 1024 (covers recent messages)</description></item>
    /// <item><description><b>BackpressureStrategy</b>: Block (no message loss)</description></item>
    /// <item><description><b>EnablePriorityQueue</b>: false (maximize throughput)</description></item>
    /// </list>
    /// <para>
    /// These defaults are validated for:
    /// - 100-500ns latency targets
    /// - 2M+ messages/s throughput
    /// - Sub-10ms startup times
    /// - RTX 2000 Ada GPU (CC 8.9)
    /// </para>
    /// </remarks>
    public static RingKernelLaunchOptions ProductionDefaults()
    {
        return new RingKernelLaunchOptions
        {
            QueueCapacity = DefaultQueueCapacity,
            DeduplicationWindowSize = DefaultDeduplicationWindowSize,
            BackpressureStrategy = BackpressureStrategy.Block,
            EnablePriorityQueue = false
        };
    }

    /// <summary>
    /// Creates a new instance optimized for low-latency scenarios (sub-microsecond).
    /// </summary>
    /// <returns>A new <see cref="RingKernelLaunchOptions"/> with low-latency defaults.</returns>
    /// <remarks>
    /// <para><b>Low-Latency Defaults</b></para>
    /// <list type="bullet">
    /// <item><description><b>QueueCapacity</b>: 256 (minimal memory footprint)</description></item>
    /// <item><description><b>DeduplicationWindowSize</b>: 256 (proportional to capacity)</description></item>
    /// <item><description><b>BackpressureStrategy</b>: Reject (fail-fast)</description></item>
    /// <item><description><b>EnablePriorityQueue</b>: false (FIFO is fastest)</description></item>
    /// </list>
    /// <para>
    /// Use for latency-critical applications where queue full = temporary backoff is acceptable.
    /// </para>
    /// </remarks>
    public static RingKernelLaunchOptions LowLatencyDefaults()
    {
        return new RingKernelLaunchOptions
        {
            QueueCapacity = 256,
            DeduplicationWindowSize = 256,
            BackpressureStrategy = BackpressureStrategy.Reject,
            EnablePriorityQueue = false
        };
    }

    /// <summary>
    /// Creates a new instance optimized for high-throughput batch processing.
    /// </summary>
    /// <returns>A new <see cref="RingKernelLaunchOptions"/> with high-throughput defaults.</returns>
    /// <remarks>
    /// <para><b>High-Throughput Defaults</b></para>
    /// <list type="bullet">
    /// <item><description><b>QueueCapacity</b>: 16384 (large burst buffer)</description></item>
    /// <item><description><b>DeduplicationWindowSize</b>: 1024 (maximum window)</description></item>
    /// <item><description><b>BackpressureStrategy</b>: Block (no loss)</description></item>
    /// <item><description><b>EnablePriorityQueue</b>: false (maximize throughput)</description></item>
    /// </list>
    /// <para>
    /// Use for batch data processing where high memory usage is acceptable for throughput gains.
    /// </para>
    /// </remarks>
    public static RingKernelLaunchOptions HighThroughputDefaults()
    {
        return new RingKernelLaunchOptions
        {
            QueueCapacity = 16384,
            DeduplicationWindowSize = 1024,
            BackpressureStrategy = BackpressureStrategy.Block,
            EnablePriorityQueue = false
        };
    }
}
