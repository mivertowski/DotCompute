// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

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
    /// Launches a ring kernel with specified grid and block dimensions.
    /// </summary>
    /// <param name="kernelId">Unique kernel identifier.</param>
    /// <param name="gridSize">Number of thread blocks in the grid.</param>
    /// <param name="blockSize">Number of threads per block.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the launch operation.</returns>
    /// <remarks>
    /// Launch starts the kernel on the GPU but does not activate it.
    /// The kernel remains idle until ActivateAsync is called.
    /// This two-phase launch allows message queues and state to be
    /// initialized before the kernel begins processing.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if kernel ID is not found or grid/block sizes are invalid.
    /// </exception>
    public Task LaunchAsync(string kernelId, int gridSize, int blockSize,
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
    public Task<IMessageQueue<T>> CreateMessageQueueAsync<T>(int capacity,
        CancellationToken cancellationToken = default) where T : unmanaged;
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
