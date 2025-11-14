// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using MessageQueueOptions = DotCompute.Abstractions.Messaging.MessageQueueOptions;
using IRingKernelMessage = DotCompute.Abstractions.Messaging.IRingKernelMessage;

namespace DotCompute.Abstractions.RingKernels;

/// <summary>
/// Runtime interface for managing persistent ring kernels.
/// </summary>
/// <remarks>
/// The ring kernel runtime provides lifecycle management and communication
/// interfaces for persistent kernels. It handles:
/// - Kernel launch and grid configuration
/// - Activation/deactivation control
/// - Message routing between kernels
/// - Status monitoring and metrics collection
/// - Resource cleanup
///
/// Implementations are backend-specific (CUDA, OpenCL, Metal) with
/// a CPU simulation for testing.
/// </remarks>
public interface IRingKernelRuntime : IAsyncDisposable
{
    /// <summary>
    /// Launches a ring kernel with specified grid and block dimensions and configuration options.
    /// </summary>
    /// <param name="kernelId">Unique kernel identifier.</param>
    /// <param name="gridSize">Number of thread blocks in the grid.</param>
    /// <param name="blockSize">Number of threads per block.</param>
    /// <param name="options">
    /// Launch options for queue configuration, or <see langword="null"/> to use production defaults.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the launch operation.</returns>
    /// <remarks>
    /// <para>
    /// Launch starts the kernel on the GPU but does not activate it.
    /// The kernel remains idle until ActivateAsync is called.
    /// This two-phase launch allows message queues and state to be
    /// initialized before the kernel begins processing.
    /// </para>
    /// <para><b>Launch Options</b></para>
    /// <list type="bullet">
    /// <item><description><b>null</b>: Uses <see cref="RingKernelLaunchOptions.ProductionDefaults()"/></description></item>
    /// <item><description><b>Custom</b>: Configure queue capacity, deduplication, backpressure</description></item>
    /// </list>
    /// <para><b>Example: Custom Queue Configuration</b></para>
    /// <code>
    /// var options = new RingKernelLaunchOptions
    /// {
    ///     QueueCapacity = 16384,
    ///     DeduplicationWindowSize = 1024,
    ///     BackpressureStrategy = BackpressureStrategy.Block
    /// };
    /// await runtime.LaunchAsync("MyKernel", gridSize: 1, blockSize: 256, options, ct);
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if kernel ID is not found or grid/block sizes are invalid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if options validation fails (e.g., invalid queue capacity or deduplication window).
    /// </exception>
    [RequiresDynamicCode("Ring kernel launch uses reflection for queue creation")]
    [RequiresUnreferencedCode("Ring kernel runtime requires reflection to detect message types")]
    public Task LaunchAsync(string kernelId, int gridSize, int blockSize,
        RingKernelLaunchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a launched ring kernel to begin processing.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the activation operation.</returns>
    /// <remarks>
    /// For persistent kernels, activation causes the kernel to begin its
    /// main processing loop. For event-driven kernels, it enables event
    /// handling. The kernel continues running until deactivated or terminated.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the kernel has not been launched or is already active.
    /// </exception>
    public Task ActivateAsync(string kernelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a ring kernel (pause without termination).
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the deactivation operation.</returns>
    /// <remarks>
    /// Deactivation pauses the kernel's processing loop but keeps it
    /// resident on the GPU. State is preserved and the kernel can be
    /// reactivated without relaunching. Useful for dynamic load balancing
    /// or power management.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the kernel is not currently active.
    /// </exception>
    public Task DeactivateAsync(string kernelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates a ring kernel and cleans up resources.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the termination operation.</returns>
    /// <remarks>
    /// Termination sends a terminate message to the kernel, waits for
    /// graceful shutdown, and frees all associated resources (message queues,
    /// state buffers, etc.). The kernel must be relaunched to execute again.
    /// </remarks>
    public Task TerminateAsync(string kernelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to a ring kernel's input queue.
    /// </summary>
    /// <typeparam name="T">Message payload type (must be unmanaged).</typeparam>
    /// <param name="kernelId">Target kernel identifier.</param>
    /// <param name="message">Message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the message is enqueued.</returns>
    /// <remarks>
    /// Messages are enqueued in the kernel's input queue and processed
    /// asynchronously. If the queue is full, this method blocks until
    /// space is available or the operation is canceled.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the kernel ID is not found.
    /// </exception>
    public Task SendMessageAsync<T>(string kernelId, KernelMessage<T> message,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Receives a message from a ring kernel's output queue.
    /// </summary>
    /// <typeparam name="T">Message payload type (must be unmanaged).</typeparam>
    /// <param name="kernelId">Source kernel identifier.</param>
    /// <param name="timeout">Maximum wait time for a message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The received message, or null if the timeout expires.
    /// </returns>
    /// <remarks>
    /// This method blocks until a message is available in the kernel's
    /// output queue or the timeout expires. Use for host-side monitoring
    /// or result collection.
    /// </remarks>
    public Task<KernelMessage<T>?> ReceiveMessageAsync<T>(string kernelId, TimeSpan timeout = default,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Gets the current status of a ring kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The kernel's current status.</returns>
    public Task<RingKernelStatus> GetStatusAsync(string kernelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets performance metrics for a ring kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Performance metrics including throughput and latency.</returns>
    public Task<RingKernelMetrics> GetMetricsAsync(string kernelId,
        CancellationToken cancellationToken = default);

    // ===== Phase 1.5: Real-Time Telemetry API =====

    /// <summary>
    /// Gets the current real-time telemetry snapshot for a running ring kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A snapshot of the kernel's current telemetry data.</returns>
    /// <remarks>
    /// <para>
    /// This is a zero-copy operation that reads telemetry directly from
    /// GPU-accessible memory (&lt;1μs latency). The telemetry struct is
    /// updated atomically by the GPU kernel and polled by the CPU.
    /// </para>
    /// <para>
    /// Unlike GetMetricsAsync which provides aggregated historical metrics,
    /// GetTelemetryAsync provides real-time counters for:
    /// - Messages processed/dropped
    /// - Current queue depth
    /// - Processing latency (min/max/avg)
    /// - Last error code
    /// - GPU timestamp of last processed message
    /// </para>
    /// <para>
    /// Use this for:
    /// - Real-time health monitoring
    /// - Stuck kernel detection (via LastProcessedTimestamp)
    /// - Backpressure monitoring (via QueueDepth)
    /// - Performance profiling (via latency metrics)
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the kernel ID is not found.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if telemetry is not enabled for the kernel.
    /// </exception>
    public Task<RingKernelTelemetry> GetTelemetryAsync(
        string kernelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables real-time telemetry collection for a ring kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="enabled">True to enable telemetry; false to disable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when telemetry state is updated.</returns>
    /// <remarks>
    /// <para>
    /// Telemetry has minimal performance overhead (&lt;50ns per message)
    /// but can be disabled for maximum performance in production scenarios
    /// where monitoring is not required.
    /// </para>
    /// <para>
    /// When enabling telemetry on a running kernel:
    /// - Telemetry buffer is allocated if not already present
    /// - Counters are initialized to zero
    /// - Kernel begins updating telemetry on next message
    /// </para>
    /// <para>
    /// When disabling telemetry:
    /// - Kernel stops updating telemetry counters
    /// - Telemetry buffer remains allocated (can be re-enabled)
    /// - Last telemetry snapshot remains readable
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the kernel ID is not found.
    /// </exception>
    public Task SetTelemetryEnabledAsync(
        string kernelId,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all telemetry counters for a ring kernel to initial values.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when telemetry is reset.</returns>
    /// <remarks>
    /// <para>
    /// This operation resets:
    /// - MessagesProcessed to 0
    /// - MessagesDropped to 0
    /// - LastProcessedTimestamp to 0
    /// - TotalLatencyNanos to 0
    /// - MaxLatencyNanos to 0
    /// - MinLatencyNanos to ulong.MaxValue
    /// - ErrorCode to 0
    /// </para>
    /// <para>
    /// QueueDepth is not reset as it reflects current state, not cumulative metrics.
    /// </para>
    /// <para>
    /// Use this for:
    /// - Restarting measurements after configuration changes
    /// - Clearing telemetry between test runs
    /// - Benchmarking specific workload phases
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the kernel ID is not found.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if telemetry is not enabled for the kernel.
    /// </exception>
    public Task ResetTelemetryAsync(
        string kernelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all ring kernels managed by this runtime.
    /// </summary>
    /// <returns>Collection of kernel IDs.</returns>
    public Task<IReadOnlyCollection<string>> ListKernelsAsync();

    /// <summary>
    /// Creates a message queue for inter-kernel communication.
    /// </summary>
    /// <typeparam name="T">Message payload type.</typeparam>
    /// <param name="capacity">Queue capacity (must be power of 2).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An initialized message queue.</returns>
    [Obsolete("Use CreateNamedMessageQueueAsync with MessageQueueOptions instead")]
    public Task<IMessageQueue<T>> CreateMessageQueueAsync<T>(int capacity,
        CancellationToken cancellationToken = default) where T : unmanaged;

    // ===== Phase 1.3: Named Message Queue Management =====

    /// <summary>
    /// Creates a named message queue with advanced configuration options.
    /// </summary>
    /// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <param name="queueName">Unique queue identifier.</param>
    /// <param name="options">Queue configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An initialized message queue.</returns>
    /// <remarks>
    /// Named queues support:
    /// - Priority-based ordering
    /// - Message deduplication
    /// - Multiple backpressure strategies
    /// - Timeout-based message expiration
    /// - Lock-free concurrent access
    ///
    /// Queue names must be unique within the runtime. Attempting to create
    /// a queue with an existing name throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if queueName is null or empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a queue with the specified name already exists.
    /// </exception>
    public Task<Messaging.IMessageQueue<T>> CreateNamedMessageQueueAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        string queueName,
        MessageQueueOptions options,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage;

    /// <summary>
    /// Gets an existing named message queue.
    /// </summary>
    /// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <param name="queueName">Queue identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The message queue if found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// Use this method to retrieve queues created by other components or
    /// to check queue existence before creation.
    /// </remarks>
    public Task<Messaging.IMessageQueue<T>?> GetNamedMessageQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage;

    /// <summary>
    /// Sends a message to a named queue.
    /// </summary>
    /// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <param name="queueName">Target queue identifier.</param>
    /// <param name="message">Message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// true if the message was successfully enqueued; otherwise, false.
    /// </returns>
    /// <remarks>
    /// Behavior depends on the queue's backpressure strategy:
    /// - Block: Waits for space to become available
    /// - Reject: Returns false immediately if full
    /// - DropOldest: Removes oldest message to make space
    /// - DropNew: Silently discards the new message
    ///
    /// This method is a convenience wrapper around GetNamedMessageQueueAsync
    /// followed by TryEnqueue. For high-throughput scenarios, cache the queue
    /// reference and enqueue directly.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if queueName is not found.
    /// </exception>
    public Task<bool> SendToNamedQueueAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage;

    /// <summary>
    /// Receives a message from a named queue.
    /// </summary>
    /// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <param name="queueName">Source queue identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The next message in the queue, or null if the queue is empty.
    /// </returns>
    /// <remarks>
    /// This is a non-blocking operation. For blocking waits, use
    /// GetNamedMessageQueueAsync and poll the queue directly.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if queueName is not found.
    /// </exception>
    public Task<T?> ReceiveFromNamedQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage;

    /// <summary>
    /// Destroys a named message queue and releases resources.
    /// </summary>
    /// <param name="queueName">Queue identifier to destroy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// true if the queue was found and destroyed; otherwise, false.
    /// </returns>
    /// <remarks>
    /// Destroying a queue:
    /// - Discards all pending messages
    /// - Releases memory resources
    /// - Removes the queue from the registry
    ///
    /// Any subsequent operations on the queue will fail. Ensure all
    /// producers and consumers have finished before destroying.
    /// </remarks>
    public Task<bool> DestroyNamedMessageQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all named message queues in the runtime.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A read-only collection of queue names.
    /// </returns>
    public Task<IReadOnlyCollection<string>> ListNamedMessageQueuesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the current status of a ring kernel.
/// </summary>
public struct RingKernelStatus : IEquatable<RingKernelStatus>
{
    /// <summary>
    /// Kernel unique identifier.
    /// </summary>
    public string KernelId { get; init; }

    /// <summary>
    /// Whether the kernel has been launched.
    /// </summary>
    public bool IsLaunched { get; init; }

    /// <summary>
    /// Whether the kernel is currently active (processing).
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Whether termination has been requested.
    /// </summary>
    public bool IsTerminating { get; init; }

    /// <summary>
    /// Number of messages pending in input queue.
    /// </summary>
    public int MessagesPending { get; init; }

    /// <summary>
    /// Total messages processed since launch.
    /// </summary>
    public long MessagesProcessed { get; init; }

    /// <summary>
    /// Current grid size (number of thread blocks).
    /// </summary>
    public int GridSize { get; init; }

    /// <summary>
    /// Current block size (threads per block).
    /// </summary>
    public int BlockSize { get; init; }

    /// <summary>
    /// Time since kernel was launched.
    /// </summary>
    public TimeSpan Uptime { get; init; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RingKernelStatus other && Equals(other);
    /// <inheritdoc/>
    public bool Equals(RingKernelStatus other) =>
        KernelId == other.KernelId &&
        IsLaunched == other.IsLaunched &&
        IsActive == other.IsActive &&
        IsTerminating == other.IsTerminating &&
        MessagesPending == other.MessagesPending &&
        MessagesProcessed == other.MessagesProcessed &&
        GridSize == other.GridSize &&
        BlockSize == other.BlockSize &&
        Uptime == other.Uptime;
    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(KernelId, IsLaunched, IsActive, IsTerminating,
            MessagesPending, MessagesProcessed, GridSize, BlockSize);
    /// <summary>
    /// Determines whether two instances are equal.
    /// </summary>
    public static bool operator ==(RingKernelStatus left, RingKernelStatus right) =>
        left.Equals(right);
    /// <summary>
    /// Determines whether two instances are not equal.
    /// </summary>
    public static bool operator !=(RingKernelStatus left, RingKernelStatus right) =>
        !left.Equals(right);
}

/// <summary>
/// Performance metrics for a ring kernel.
/// </summary>
public struct RingKernelMetrics : IEquatable<RingKernelMetrics>
{
    /// <summary>
    /// Total number of kernel launches.
    /// </summary>
    public long LaunchCount { get; init; }

    /// <summary>
    /// Total messages sent by this kernel.
    /// </summary>
    public long MessagesSent { get; init; }

    /// <summary>
    /// Total messages received by this kernel.
    /// </summary>
    public long MessagesReceived { get; init; }

    /// <summary>
    /// Average message processing time (milliseconds).
    /// </summary>
    public double AvgProcessingTimeMs { get; init; }

    /// <summary>
    /// Message throughput (messages per second).
    /// </summary>
    public double ThroughputMsgsPerSec { get; init; }

    /// <summary>
    /// Average input queue utilization (0.0 to 1.0).
    /// </summary>
    public double InputQueueUtilization { get; init; }

    /// <summary>
    /// Average output queue utilization (0.0 to 1.0).
    /// </summary>
    public double OutputQueueUtilization { get; init; }

    /// <summary>
    /// Peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryBytes { get; init; }

    /// <summary>
    /// Current memory usage in bytes.
    /// </summary>
    public long CurrentMemoryBytes { get; init; }

    /// <summary>
    /// GPU utilization percentage (0.0 to 100.0).
    /// </summary>
    /// <remarks>
    /// This is an approximate measure of how efficiently the kernel
    /// is using GPU resources. Values below 50% may indicate the kernel
    /// is memory-bound or has insufficient parallelism.
    /// </remarks>
    public double GpuUtilizationPercent { get; init; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RingKernelMetrics other && Equals(other);
    /// <inheritdoc/>
    public bool Equals(RingKernelMetrics other) =>
        LaunchCount == other.LaunchCount &&
        MessagesSent == other.MessagesSent &&
        MessagesReceived == other.MessagesReceived &&
        AvgProcessingTimeMs == other.AvgProcessingTimeMs &&
        ThroughputMsgsPerSec == other.ThroughputMsgsPerSec &&
        InputQueueUtilization == other.InputQueueUtilization &&
        OutputQueueUtilization == other.OutputQueueUtilization &&
        PeakMemoryBytes == other.PeakMemoryBytes &&
        CurrentMemoryBytes == other.CurrentMemoryBytes &&
        GpuUtilizationPercent == other.GpuUtilizationPercent;
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(LaunchCount);
        hash.Add(MessagesSent);
        hash.Add(MessagesReceived);
        hash.Add(AvgProcessingTimeMs);
        hash.Add(ThroughputMsgsPerSec);
        hash.Add(InputQueueUtilization);
        hash.Add(OutputQueueUtilization);
        hash.Add(PeakMemoryBytes);
        hash.Add(CurrentMemoryBytes);
        hash.Add(GpuUtilizationPercent);
        return hash.ToHashCode();
    }
    /// <summary>
    /// Determines whether two instances are equal.
    /// </summary>
    public static bool operator ==(RingKernelMetrics left, RingKernelMetrics right) =>
        left.Equals(right);
    /// <summary>
    /// Determines whether two instances are not equal.
    /// </summary>
    public static bool operator !=(RingKernelMetrics left, RingKernelMetrics right) =>
        !left.Equals(right);
}
