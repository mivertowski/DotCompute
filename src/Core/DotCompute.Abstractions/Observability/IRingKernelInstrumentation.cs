// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotCompute.Abstractions.Observability;

/// <summary>
/// Defines OpenTelemetry-compatible instrumentation for Ring Kernels.
/// </summary>
/// <remarks>
/// Provides standardized telemetry for distributed tracing, metrics, and logging
/// across all Ring Kernel operations. Implements W3C Trace Context propagation
/// for cross-service correlation.
/// </remarks>
public interface IRingKernelInstrumentation : IDisposable
{
    /// <summary>
    /// Gets the ActivitySource for Ring Kernel distributed tracing.
    /// </summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>
    /// Gets the Meter for Ring Kernel metrics.
    /// </summary>
    public Meter Meter { get; }

    /// <summary>
    /// Gets whether instrumentation is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Starts an activity for a Ring Kernel lifecycle operation.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="kind">Activity kind (default: Internal).</param>
    /// <returns>The started activity, or null if sampling is disabled.</returns>
    public Activity? StartKernelActivity(
        string kernelId,
        string operationName,
        ActivityKind kind = ActivityKind.Internal);

    /// <summary>
    /// Starts an activity for message processing.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="messageType">Type of message being processed.</param>
    /// <param name="messageId">Unique message identifier.</param>
    /// <param name="parentContext">Optional parent activity context.</param>
    /// <returns>The started activity, or null if sampling is disabled.</returns>
    public Activity? StartMessageActivity(
        string kernelId,
        string messageType,
        string messageId,
        ActivityContext? parentContext = null);

    /// <summary>
    /// Records a kernel launch event.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="gridSize">Grid size (thread blocks).</param>
    /// <param name="blockSize">Block size (threads per block).</param>
    /// <param name="backend">Backend type (CPU, CUDA, OpenCL, Metal).</param>
    public void RecordKernelLaunch(
        string kernelId,
        int gridSize,
        int blockSize,
        string backend);

    /// <summary>
    /// Records a kernel termination event.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="uptime">Total kernel uptime.</param>
    /// <param name="messagesProcessed">Total messages processed.</param>
    /// <param name="terminationReason">Reason for termination.</param>
    public void RecordKernelTermination(
        string kernelId,
        TimeSpan uptime,
        long messagesProcessed,
        string terminationReason);

    /// <summary>
    /// Records message throughput metrics.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="messagesPerSecond">Current throughput.</param>
    /// <param name="queueDepth">Current queue depth.</param>
    public void RecordThroughput(
        string kernelId,
        double messagesPerSecond,
        int queueDepth);

    /// <summary>
    /// Records message latency metrics.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="latencyNanos">Message processing latency in nanoseconds.</param>
    public void RecordMessageLatency(
        string kernelId,
        long latencyNanos);

    /// <summary>
    /// Records a message drop event.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="reason">Reason for dropping the message.</param>
    public void RecordMessageDropped(
        string kernelId,
        string reason);

    /// <summary>
    /// Records a kernel error event.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="errorCode">Error code from kernel.</param>
    /// <param name="errorMessage">Error description.</param>
    public void RecordKernelError(
        string kernelId,
        int errorCode,
        string errorMessage);

    /// <summary>
    /// Records kernel health status.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="isHealthy">Whether kernel is healthy.</param>
    /// <param name="lastProcessedTimestamp">Last message processed timestamp.</param>
    public void RecordHealthStatus(
        string kernelId,
        bool isHealthy,
        DateTimeOffset lastProcessedTimestamp);

    /// <summary>
    /// Adds an event to the current activity.
    /// </summary>
    /// <param name="activity">The activity to add event to.</param>
    /// <param name="eventName">Event name.</param>
    /// <param name="attributes">Event attributes.</param>
    public void AddEvent(
        Activity? activity,
        string eventName,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>
    /// Sets the activity status based on operation result.
    /// </summary>
    /// <param name="activity">The activity to update.</param>
    /// <param name="success">Whether operation succeeded.</param>
    /// <param name="description">Optional status description.</param>
    public void SetActivityStatus(
        Activity? activity,
        bool success,
        string? description = null);
}

/// <summary>
/// Configuration options for Ring Kernel instrumentation.
/// </summary>
public sealed class RingKernelInstrumentationOptions
{
    /// <summary>
    /// Gets or sets whether instrumentation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the service name for telemetry.
    /// </summary>
    public string ServiceName { get; set; } = "DotCompute.RingKernels";

    /// <summary>
    /// Gets or sets the service version.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the sampling rate (0.0 to 1.0).
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets whether to record message-level activities.
    /// </summary>
    /// <remarks>
    /// Enabling message-level activities provides detailed tracing but
    /// increases overhead. Disable for high-throughput production scenarios.
    /// </remarks>
    public bool RecordMessageActivities { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to record detailed latency histograms.
    /// </summary>
    public bool RecordLatencyHistograms { get; set; } = true;

    /// <summary>
    /// Gets or sets the histogram bucket boundaries for latency in milliseconds.
    /// </summary>
    public IReadOnlyList<double> LatencyBuckets { get; set; } =
    [
        0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10
    ];

    /// <summary>
    /// Gets or sets custom tags to add to all telemetry.
    /// </summary>
    public IDictionary<string, string> GlobalTags { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets whether to propagate trace context in messages.
    /// </summary>
    public bool PropagateTraceContext { get; set; } = true;

    /// <summary>
    /// Gets or sets the health check interval.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the stuck kernel threshold for health checks.
    /// </summary>
    public TimeSpan StuckKernelThreshold { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Semantic conventions for Ring Kernel telemetry.
/// </summary>
/// <remarks>
/// Follows OpenTelemetry semantic conventions where applicable,
/// with extensions for GPU compute-specific attributes.
/// </remarks>
public static class RingKernelSemanticConventions
{
    // Resource attributes
    /// <summary>Service name attribute.</summary>
    public const string ServiceName = "service.name";
    /// <summary>Service version attribute.</summary>
    public const string ServiceVersion = "service.version";

    // Ring Kernel attributes
    /// <summary>Ring kernel identifier.</summary>
    public const string KernelId = "ring_kernel.id";
    /// <summary>Kernel state (launched, active, terminated).</summary>
    public const string KernelState = "ring_kernel.state";
    /// <summary>Grid size (thread blocks).</summary>
    public const string GridSize = "ring_kernel.grid_size";
    /// <summary>Block size (threads per block).</summary>
    public const string BlockSize = "ring_kernel.block_size";
    /// <summary>Backend type (CPU, CUDA, OpenCL, Metal).</summary>
    public const string Backend = "ring_kernel.backend";
    /// <summary>Kernel uptime in seconds.</summary>
    public const string Uptime = "ring_kernel.uptime";

    // Message attributes
    /// <summary>Message identifier.</summary>
    public const string MessageId = "ring_kernel.message.id";
    /// <summary>Message type name.</summary>
    public const string MessageType = "ring_kernel.message.type";
    /// <summary>Message priority.</summary>
    public const string MessagePriority = "ring_kernel.message.priority";
    /// <summary>Message processing latency in nanoseconds.</summary>
    public const string MessageLatencyNanos = "ring_kernel.message.latency_ns";

    // Queue attributes
    /// <summary>Queue name.</summary>
    public const string QueueName = "ring_kernel.queue.name";
    /// <summary>Queue depth (pending messages).</summary>
    public const string QueueDepth = "ring_kernel.queue.depth";
    /// <summary>Queue capacity.</summary>
    public const string QueueCapacity = "ring_kernel.queue.capacity";

    // Error attributes
    /// <summary>Error code.</summary>
    public const string ErrorCode = "ring_kernel.error.code";
    /// <summary>Error message.</summary>
    public const string ErrorMessage = "ring_kernel.error.message";
    /// <summary>Drop reason.</summary>
    public const string DropReason = "ring_kernel.drop.reason";

    // Termination attributes
    /// <summary>Termination reason.</summary>
    public const string TerminationReason = "ring_kernel.termination.reason";
    /// <summary>Total messages processed.</summary>
    public const string MessagesProcessed = "ring_kernel.messages.processed";
    /// <summary>Total messages dropped.</summary>
    public const string MessagesDropped = "ring_kernel.messages.dropped";

    // Metric names
    /// <summary>Counter: Total kernel launches.</summary>
    public const string MetricKernelLaunches = "ring_kernel.launches";
    /// <summary>Counter: Total kernel terminations.</summary>
    public const string MetricKernelTerminations = "ring_kernel.terminations";
    /// <summary>Counter: Total messages processed.</summary>
    public const string MetricMessagesProcessed = "ring_kernel.messages.processed.total";
    /// <summary>Counter: Total messages dropped.</summary>
    public const string MetricMessagesDropped = "ring_kernel.messages.dropped.total";
    /// <summary>Gauge: Current queue depth.</summary>
    public const string MetricQueueDepth = "ring_kernel.queue.depth";
    /// <summary>Gauge: Current active kernels.</summary>
    public const string MetricActiveKernels = "ring_kernel.active";
    /// <summary>Histogram: Message processing latency.</summary>
    public const string MetricMessageLatency = "ring_kernel.message.latency";
    /// <summary>Gauge: Throughput (messages per second).</summary>
    public const string MetricThroughput = "ring_kernel.throughput";
    /// <summary>Counter: Total errors.</summary>
    public const string MetricErrors = "ring_kernel.errors.total";
    /// <summary>Gauge: Kernel health status (0=unhealthy, 1=healthy).</summary>
    public const string MetricHealthStatus = "ring_kernel.health";

    // Span names
    /// <summary>Span: Kernel launch operation.</summary>
    public const string SpanKernelLaunch = "ring_kernel.launch";
    /// <summary>Span: Kernel activation.</summary>
    public const string SpanKernelActivate = "ring_kernel.activate";
    /// <summary>Span: Kernel deactivation.</summary>
    public const string SpanKernelDeactivate = "ring_kernel.deactivate";
    /// <summary>Span: Kernel termination.</summary>
    public const string SpanKernelTerminate = "ring_kernel.terminate";
    /// <summary>Span: Message processing.</summary>
    public const string SpanMessageProcess = "ring_kernel.message.process";
    /// <summary>Span: Message send.</summary>
    public const string SpanMessageSend = "ring_kernel.message.send";
    /// <summary>Span: Message receive.</summary>
    public const string SpanMessageReceive = "ring_kernel.message.receive";
}
