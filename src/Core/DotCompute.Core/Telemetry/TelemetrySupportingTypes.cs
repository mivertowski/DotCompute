using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Core.Telemetry.System;
using DotCompute.Core.Telemetry.Options;

namespace DotCompute.Core.Telemetry;
/// <summary>
/// A class that represents distributed tracing options.
/// </summary>

// Supporting types for DistributedTracer.cs
public sealed class DistributedTracingOptions
{
    /// <summary>
    /// Gets or sets the max traces per export.
    /// </summary>
    /// <value>The max traces per export.</value>
    public int MaxTracesPerExport { get; set; } = 1000;
    /// <summary>
    /// Gets or sets the trace retention hours.
    /// </summary>
    /// <value>The trace retention hours.</value>
    public int TraceRetentionHours { get; set; } = 24;
    /// <summary>
    /// Gets or sets the enable sampling.
    /// </summary>
    /// <value>The enable sampling.</value>
    public bool EnableSampling { get; set; } = true;
    /// <summary>
    /// Gets or sets the sampling rate.
    /// </summary>
    /// <value>The sampling rate.</value>
    public double SamplingRate { get; set; } = 0.1; // 10% sampling
}
/// <summary>
/// An trace export format enumeration.
/// </summary>

public enum TraceExportFormat
{
    OpenTelemetry,
    Jaeger,
    Zipkin,
    Custom
}
/// <summary>
/// A class that represents trace context.
/// </summary>

public sealed class TraceContext
{
    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    /// <value>The trace id.</value>
    public string TraceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    /// <value>The correlation id.</value>
    public string CorrelationId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    /// <value>The operation name.</value>
    public string OperationName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    /// <value>The activity.</value>
    public Activity? Activity { get; set; }
    /// <summary>
    /// Gets or sets the parent span context.
    /// </summary>
    /// <value>The parent span context.</value>
    public SpanContext? ParentSpanContext { get; set; }
    /// <summary>
    /// Gets or sets the tags.
    /// </summary>
    /// <value>The tags.</value>
    public Dictionary<string, object?> Tags { get; } = [];
    /// <summary>
    /// Gets or sets the spans.
    /// </summary>
    /// <value>The spans.</value>
    public ConcurrentBag<SpanData> Spans { get; set; } = [];
    /// <summary>
    /// Gets or sets the device operations.
    /// </summary>
    /// <value>The device operations.</value>
    public ConcurrentDictionary<string, DeviceOperationTrace> DeviceOperations { get; set; } = new();
}
/// <summary>
/// A class that represents span context.
/// </summary>

public sealed class SpanContext
{
    /// <summary>
    /// Gets or sets the span identifier.
    /// </summary>
    /// <value>The span id.</value>
    public string SpanId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    /// <value>The trace id.</value>
    public string TraceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    /// <value>The correlation id.</value>
    public string CorrelationId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the span name.
    /// </summary>
    /// <value>The span name.</value>
    public string SpanName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the span kind.
    /// </summary>
    /// <value>The span kind.</value>
    public SpanKind SpanKind { get; set; } = SpanKind.Internal;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset? EndTime { get; set; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan Duration { get; set; }
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <value>The status.</value>
    public SpanStatus Status { get; set; } = SpanStatus.Ok;
    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    /// <value>The status message.</value>
    public string? StatusMessage { get; set; }
    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    /// <value>The attributes.</value>
    public Dictionary<string, object?> Attributes { get; } = [];
    /// <summary>
    /// Gets or sets the events.
    /// </summary>
    /// <value>The events.</value>
    public IList<SpanEvent> Events { get; } = [];
    /// <summary>
    /// Gets or sets the parent span identifier.
    /// </summary>
    /// <value>The parent span id.</value>
    public string? ParentSpanId { get; set; }
    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    /// <value>The activity.</value>
    public Activity? Activity { get; set; }
}
/// <summary>
/// A class that represents span data.
/// </summary>

public sealed class SpanData
{
    /// <summary>
    /// Gets or sets the span identifier.
    /// </summary>
    /// <value>The span id.</value>
    public string SpanId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    /// <value>The trace id.</value>
    public string TraceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    /// <value>The correlation id.</value>
    public string CorrelationId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the span name.
    /// </summary>
    /// <value>The span name.</value>
    public string SpanName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the span kind.
    /// </summary>
    /// <value>The span kind.</value>
    public SpanKind SpanKind { get; set; }
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset EndTime { get; set; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan Duration { get; set; }
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <value>The status.</value>
    public SpanStatus Status { get; set; }
    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    /// <value>The status message.</value>
    public string? StatusMessage { get; set; }
    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    /// <value>The attributes.</value>
    public Dictionary<string, object?> Attributes { get; } = [];
    /// <summary>
    /// Gets or sets the events.
    /// </summary>
    /// <value>The events.</value>
    public IList<SpanEvent> Events { get; } = [];
    /// <summary>
    /// Gets or sets the parent span identifier.
    /// </summary>
    /// <value>The parent span id.</value>
    public string? ParentSpanId { get; set; }
}
/// <summary>
/// A class that represents span event.
/// </summary>

public sealed class SpanEvent
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    /// <value>The attributes.</value>
    public Dictionary<string, object?> Attributes { get; } = [];
}
/// <summary>
/// A class that represents device operation trace.
/// </summary>

public sealed class DeviceOperationTrace
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the operation count.
    /// </summary>
    /// <value>The operation count.</value>
    public int OperationCount { get; set; }
    /// <summary>
    /// Gets or sets the first operation time.
    /// </summary>
    /// <value>The first operation time.</value>
    public DateTimeOffset FirstOperationTime { get; set; }
    /// <summary>
    /// Gets or sets the last operation time.
    /// </summary>
    /// <value>The last operation time.</value>
    public DateTimeOffset LastOperationTime { get; set; }
    /// <summary>
    /// Gets or sets the active spans.
    /// </summary>
    /// <value>The active spans.</value>
    public ConcurrentBag<SpanContext> ActiveSpans { get; set; } = [];
}
/// <summary>
/// A class that represents trace data.
/// </summary>

public sealed class TraceData
{
    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    /// <value>The trace id.</value>
    public string TraceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    /// <value>The correlation id.</value>
    public string CorrelationId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    /// <value>The operation name.</value>
    public string OperationName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset EndTime { get; set; }
    /// <summary>
    /// Gets or sets the total duration.
    /// </summary>
    /// <value>The total duration.</value>
    public TimeSpan TotalDuration { get; set; }
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <value>The status.</value>
    public TraceStatus Status { get; set; }
    /// <summary>
    /// Gets or sets the spans.
    /// </summary>
    /// <value>The spans.</value>
    public IList<SpanData> Spans { get; } = [];
    /// <summary>
    /// Gets or sets the device operations.
    /// </summary>
    /// <value>The device operations.</value>
    public Dictionary<string, DeviceOperationTrace> DeviceOperations { get; } = [];
    /// <summary>
    /// Gets or sets the tags.
    /// </summary>
    /// <value>The tags.</value>
    public Dictionary<string, object?> Tags { get; } = [];
    /// <summary>
    /// Gets or sets the analysis.
    /// </summary>
    /// <value>The analysis.</value>
    public TraceAnalysis? Analysis { get; set; }
}
/// <summary>
/// A class that represents trace analysis.
/// </summary>

public sealed class TraceAnalysis
{
    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    /// <value>The trace id.</value>
    public string TraceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the analysis timestamp.
    /// </summary>
    /// <value>The analysis timestamp.</value>
    public DateTimeOffset AnalysisTimestamp { get; set; }
    /// <summary>
    /// Gets or sets the total operation time.
    /// </summary>
    /// <value>The total operation time.</value>
    public TimeSpan TotalOperationTime { get; set; }
    /// <summary>
    /// Gets or sets the span count.
    /// </summary>
    /// <value>The span count.</value>
    public int SpanCount { get; set; }
    /// <summary>
    /// Gets or sets the device count.
    /// </summary>
    /// <value>The device count.</value>
    public int DeviceCount { get; set; }
    /// <summary>
    /// Gets or sets the critical path.
    /// </summary>
    /// <value>The critical path.</value>
    public IList<SpanData> CriticalPath { get; } = [];
    /// <summary>
    /// Gets or sets the critical path duration.
    /// </summary>
    /// <value>The critical path duration.</value>
    public double CriticalPathDuration { get; set; }
    /// <summary>
    /// Gets or sets the device utilization.
    /// </summary>
    /// <value>The device utilization.</value>
    public Dictionary<string, double> DeviceUtilization { get; } = [];
    /// <summary>
    /// Gets or sets the bottlenecks.
    /// </summary>
    /// <value>The bottlenecks.</value>
    public IList<PerformanceBottleneck> Bottlenecks { get; } = [];
    /// <summary>
    /// Gets or sets the memory access patterns.
    /// </summary>
    /// <value>The memory access patterns.</value>
    public Dictionary<string, object> MemoryAccessPatterns { get; } = [];
    /// <summary>
    /// Gets or sets the parallelism efficiency.
    /// </summary>
    /// <value>The parallelism efficiency.</value>
    public double ParallelismEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the device efficiency.
    /// </summary>
    /// <value>The device efficiency.</value>
    public double DeviceEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the optimization recommendations.
    /// </summary>
    /// <value>The optimization recommendations.</value>
    public IList<string> OptimizationRecommendations { get; } = [];
}
/// <summary>
/// A class that represents kernel performance data.
/// </summary>

public sealed class KernelPerformanceData
{
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public double ThroughputOpsPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the occupancy percentage.
    /// </summary>
    /// <value>The occupancy percentage.</value>
    public double OccupancyPercentage { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the instruction throughput.
    /// </summary>
    /// <value>The instruction throughput.</value>
    public double InstructionThroughput { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth g b per second.
    /// </summary>
    /// <value>The memory bandwidth g b per second.</value>
    public double MemoryBandwidthGBPerSecond { get; set; }
}
/// <summary>
/// An span kind enumeration.
/// </summary>

public enum SpanKind
{
    Internal,
    Server,
    Client,
    Producer,
    Consumer
}
/// <summary>
/// An span status enumeration.
/// </summary>

public enum SpanStatus
{
    Ok,
    Error,
    Timeout,
    Cancelled
}
/// <summary>
/// An trace status enumeration.
/// </summary>

public enum TraceStatus
{
    Ok,
    Error,
    Timeout,
    PartialFailure
}
/// <summary>
/// A class that represents performance profiler options.
/// </summary>

// Supporting types for PerformanceProfiler.cs
public sealed class PerformanceProfilerOptions
{
    /// <summary>
    /// Gets or sets the max concurrent profiles.
    /// </summary>
    /// <value>The max concurrent profiles.</value>
    public int MaxConcurrentProfiles { get; set; } = 10;
    /// <summary>
    /// Gets or sets the enable continuous profiling.
    /// </summary>
    /// <value>The enable continuous profiling.</value>
    public bool EnableContinuousProfiling { get; set; } = true;
    /// <summary>
    /// Gets or sets the sampling interval ms.
    /// </summary>
    /// <value>The sampling interval ms.</value>
    public int SamplingIntervalMs { get; set; } = 100;
    /// <summary>
    /// Gets or sets the allow orphaned records.
    /// </summary>
    /// <value>The allow orphaned records.</value>
    public bool AllowOrphanedRecords { get; set; }
}
/// <summary>
/// A class that represents active profile.
/// </summary>


public sealed class ActiveProfile
{
    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    /// <value>The correlation id.</value>
    public string CorrelationId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public ProfileOptions Options { get; set; } = new();
    /// <summary>
    /// Gets or sets the kernel executions.
    /// </summary>
    /// <value>The kernel executions.</value>
    public ConcurrentBag<KernelExecutionProfile> KernelExecutions { get; set; } = [];
    /// <summary>
    /// Gets or sets the memory operations.
    /// </summary>
    /// <value>The memory operations.</value>
    public ConcurrentBag<MemoryOperationProfile> MemoryOperations { get; set; } = [];
    /// <summary>
    /// Gets or sets the device metrics.
    /// </summary>
    /// <value>The device metrics.</value>
    public ConcurrentDictionary<string, DeviceProfileMetrics> DeviceMetrics { get; set; } = new();
    /// <summary>
    /// Gets or sets the system snapshots.
    /// </summary>
    /// <value>The system snapshots.</value>
    public ConcurrentQueue<SystemSnapshot> SystemSnapshots { get; set; } = new();
}
/// <summary>
/// A class that represents performance profile.
/// </summary>

public sealed class PerformanceProfile
{
    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    /// <value>The correlation id.</value>
    public string CorrelationId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset? EndTime { get; set; }
    /// <summary>
    /// Gets or sets the total duration.
    /// </summary>
    /// <value>The total duration.</value>
    public TimeSpan TotalDuration { get; set; }
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <value>The status.</value>
    public ProfileStatus Status { get; set; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public string? Message { get; set; }
    /// <summary>
    /// Gets or sets the total kernel executions.
    /// </summary>
    /// <value>The total kernel executions.</value>
    public int TotalKernelExecutions { get; set; }
    /// <summary>
    /// Gets or sets the total memory operations.
    /// </summary>
    /// <value>The total memory operations.</value>
    public int TotalMemoryOperations { get; set; }
    /// <summary>
    /// Gets or sets the devices involved.
    /// </summary>
    /// <value>The devices involved.</value>
    public int DevicesInvolved { get; set; }
    /// <summary>
    /// Gets or sets the analysis.
    /// </summary>
    /// <value>The analysis.</value>
    public ProfileAnalysis? Analysis { get; set; }
    /// <summary>
    /// Gets or sets the kernel executions.
    /// </summary>
    /// <value>The kernel executions.</value>
    public IList<KernelExecutionProfile> KernelExecutions { get; } = [];
    /// <summary>
    /// Gets or sets the memory operations.
    /// </summary>
    /// <value>The memory operations.</value>
    public IList<MemoryOperationProfile> MemoryOperations { get; } = [];
    /// <summary>
    /// Gets or sets the device metrics.
    /// </summary>
    /// <value>The device metrics.</value>
    public Dictionary<string, DeviceProfileMetrics> DeviceMetrics { get; } = [];
}
/// <summary>
/// A class that represents kernel execution profile.
/// </summary>

public sealed class KernelExecutionProfile
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset EndTime { get; set; }
    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    /// <value>The execution time.</value>
    public TimeSpan ExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public double ThroughputOpsPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the occupancy percentage.
    /// </summary>
    /// <value>The occupancy percentage.</value>
    public double OccupancyPercentage { get; set; }
    /// <summary>
    /// Gets or sets the instruction throughput.
    /// </summary>
    /// <value>The instruction throughput.</value>
    public double InstructionThroughput { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth g b per second.
    /// </summary>
    /// <value>The memory bandwidth g b per second.</value>
    public double MemoryBandwidthGBPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the memory coalescing efficiency.
    /// </summary>
    /// <value>The memory coalescing efficiency.</value>
    public double MemoryCoalescingEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the compute units used.
    /// </summary>
    /// <value>The compute units used.</value>
    public int ComputeUnitsUsed { get; set; }
    /// <summary>
    /// Gets or sets the registers per thread.
    /// </summary>
    /// <value>The registers per thread.</value>
    public int RegistersPerThread { get; set; }
    /// <summary>
    /// Gets or sets the shared memory used.
    /// </summary>
    /// <value>The shared memory used.</value>
    public long SharedMemoryUsed { get; set; }
    /// <summary>
    /// Gets or sets the warp efficiency.
    /// </summary>
    /// <value>The warp efficiency.</value>
    public double WarpEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the branch divergence.
    /// </summary>
    /// <value>The branch divergence.</value>
    public double BranchDivergence { get; set; }
    /// <summary>
    /// Gets or sets the memory latency.
    /// </summary>
    /// <value>The memory latency.</value>
    public double MemoryLatency { get; set; }
    /// <summary>
    /// Gets or sets the power consumption.
    /// </summary>
    /// <value>The power consumption.</value>
    public double PowerConsumption { get; set; }
}
/// <summary>
/// A class that represents memory operation profile.
/// </summary>

public sealed class MemoryOperationProfile
{
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    /// <value>The operation type.</value>
    public string OperationType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan Duration { get; set; }
    /// <summary>
    /// Gets or sets the bytes transferred.
    /// </summary>
    /// <value>The bytes transferred.</value>
    public long BytesTransferred { get; set; }
    /// <summary>
    /// Gets or sets the bandwidth g b per second.
    /// </summary>
    /// <value>The bandwidth g b per second.</value>
    public double BandwidthGBPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the access pattern.
    /// </summary>
    /// <value>The access pattern.</value>
    public string AccessPattern { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the coalescing efficiency.
    /// </summary>
    /// <value>The coalescing efficiency.</value>
    public double CoalescingEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the memory segment.
    /// </summary>
    /// <value>The memory segment.</value>
    public string MemorySegment { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the transfer direction.
    /// </summary>
    /// <value>The transfer direction.</value>
    public string TransferDirection { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the queue depth.
    /// </summary>
    /// <value>The queue depth.</value>
    public int QueueDepth { get; set; }
}
/// <summary>
/// A class that represents device profile metrics.
/// </summary>

public sealed class DeviceProfileMetrics
{
    /// <summary>
    /// Gets or sets the utilization percentage.
    /// </summary>
    /// <value>The utilization percentage.</value>
    public double UtilizationPercentage { get; set; }
    /// <summary>
    /// Gets or sets the temperature celsius.
    /// </summary>
    /// <value>The temperature celsius.</value>
    public double TemperatureCelsius { get; set; }
    /// <summary>
    /// Gets or sets the memory usage bytes.
    /// </summary>
    /// <value>The memory usage bytes.</value>
    public long MemoryUsageBytes { get; set; }
    /// <summary>
    /// Gets or sets the power consumption watts.
    /// </summary>
    /// <value>The power consumption watts.</value>
    public double PowerConsumptionWatts { get; set; }
}
/// <summary>
/// A class that represents system snapshot.
/// </summary>

public sealed class SystemSnapshot
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the cpu usage.
    /// </summary>
    /// <value>The cpu usage.</value>
    public double CpuUsage { get; set; }
    /// <summary>
    /// Gets or sets the memory usage.
    /// </summary>
    /// <value>The memory usage.</value>
    public long MemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the thread count.
    /// </summary>
    /// <value>The thread count.</value>
    public int ThreadCount { get; set; }
}
/// <summary>
/// A class that represents kernel execution metrics.
/// </summary>

public sealed class KernelExecutionMetrics
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset EndTime { get; set; }
    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    /// <value>The execution time.</value>
    public TimeSpan ExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public double ThroughputOpsPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the occupancy percentage.
    /// </summary>
    /// <value>The occupancy percentage.</value>
    public double OccupancyPercentage { get; set; }
    /// <summary>
    /// Gets or sets the instruction throughput.
    /// </summary>
    /// <value>The instruction throughput.</value>
    public double InstructionThroughput { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth g b per second.
    /// </summary>
    /// <value>The memory bandwidth g b per second.</value>
    public double MemoryBandwidthGBPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the memory coalescing efficiency.
    /// </summary>
    /// <value>The memory coalescing efficiency.</value>
    public double MemoryCoalescingEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the compute units used.
    /// </summary>
    /// <value>The compute units used.</value>
    public int ComputeUnitsUsed { get; set; }
    /// <summary>
    /// Gets or sets the registers per thread.
    /// </summary>
    /// <value>The registers per thread.</value>
    public int RegistersPerThread { get; set; }
    /// <summary>
    /// Gets or sets the shared memory used.
    /// </summary>
    /// <value>The shared memory used.</value>
    public long SharedMemoryUsed { get; set; }
    /// <summary>
    /// Gets or sets the warp efficiency.
    /// </summary>
    /// <value>The warp efficiency.</value>
    public double WarpEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the branch divergence.
    /// </summary>
    /// <value>The branch divergence.</value>
    public double BranchDivergence { get; set; }
    /// <summary>
    /// Gets or sets the memory latency.
    /// </summary>
    /// <value>The memory latency.</value>
    public double MemoryLatency { get; set; }
    /// <summary>
    /// Gets or sets the power consumption.
    /// </summary>
    /// <value>The power consumption.</value>
    public double PowerConsumption { get; set; }
}
/// <summary>
/// A class that represents memory operation metrics.
/// </summary>

public sealed class MemoryOperationMetrics
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan Duration { get; set; }
    /// <summary>
    /// Gets or sets the bytes transferred.
    /// </summary>
    /// <value>The bytes transferred.</value>
    public long BytesTransferred { get; set; }
    /// <summary>
    /// Gets or sets the bandwidth g b per second.
    /// </summary>
    /// <value>The bandwidth g b per second.</value>
    public double BandwidthGBPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the access pattern.
    /// </summary>
    /// <value>The access pattern.</value>
    public string AccessPattern { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the coalescing efficiency.
    /// </summary>
    /// <value>The coalescing efficiency.</value>
    public double CoalescingEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the memory segment.
    /// </summary>
    /// <value>The memory segment.</value>
    public string MemorySegment { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the transfer direction.
    /// </summary>
    /// <value>The transfer direction.</value>
    public string TransferDirection { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the queue depth.
    /// </summary>
    /// <value>The queue depth.</value>
    public int QueueDepth { get; set; }
}
/// <summary>
/// A class that represents profile analysis.
/// </summary>

public sealed class ProfileAnalysis
{
    /// <summary>
    /// Gets or sets the analysis timestamp.
    /// </summary>
    /// <value>The analysis timestamp.</value>
    public DateTimeOffset AnalysisTimestamp { get; set; }
    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    /// <value>The total execution time.</value>
    public double TotalExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the average kernel execution time.
    /// </summary>
    /// <value>The average kernel execution time.</value>
    public double AverageKernelExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the overall throughput.
    /// </summary>
    /// <value>The overall throughput.</value>
    public double OverallThroughput { get; set; }
    /// <summary>
    /// Gets or sets the total memory transferred.
    /// </summary>
    /// <value>The total memory transferred.</value>
    public long TotalMemoryTransferred { get; set; }
    /// <summary>
    /// Gets or sets the average memory bandwidth.
    /// </summary>
    /// <value>The average memory bandwidth.</value>
    public double AverageMemoryBandwidth { get; set; }
    /// <summary>
    /// Gets or sets the average occupancy.
    /// </summary>
    /// <value>The average occupancy.</value>
    public double AverageOccupancy { get; set; }
    /// <summary>
    /// Gets or sets the average cache hit rate.
    /// </summary>
    /// <value>The average cache hit rate.</value>
    public double AverageCacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the device utilization efficiency.
    /// </summary>
    /// <value>The device utilization efficiency.</value>
    public double DeviceUtilizationEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the parallelism efficiency.
    /// </summary>
    /// <value>The parallelism efficiency.</value>
    public double ParallelismEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the identified bottlenecks.
    /// </summary>
    /// <value>The identified bottlenecks.</value>
    public IList<string> IdentifiedBottlenecks { get; } = [];
    /// <summary>
    /// Gets or sets the optimization recommendations.
    /// </summary>
    /// <value>The optimization recommendations.</value>
    public IList<string> OptimizationRecommendations { get; } = [];
}
/// <summary>
/// A class that represents kernel analysis result.
/// </summary>

public sealed class KernelAnalysisResult
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <value>The status.</value>
    public AnalysisStatus Status { get; set; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public string? Message { get; set; }
    /// <summary>
    /// Gets or sets the time window.
    /// </summary>
    /// <value>The time window.</value>
    public TimeSpan TimeWindow { get; set; }
    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    /// <value>The execution count.</value>
    public int ExecutionCount { get; set; }
    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    /// <value>The average execution time.</value>
    public double AverageExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the min execution time.
    /// </summary>
    /// <value>The min execution time.</value>
    public double MinExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the max execution time.
    /// </summary>
    /// <value>The max execution time.</value>
    public double MaxExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the execution time std dev.
    /// </summary>
    /// <value>The execution time std dev.</value>
    public double ExecutionTimeStdDev { get; set; }
    /// <summary>
    /// Gets or sets the average throughput.
    /// </summary>
    /// <value>The average throughput.</value>
    public double AverageThroughput { get; set; }
    /// <summary>
    /// Gets or sets the average occupancy.
    /// </summary>
    /// <value>The average occupancy.</value>
    public double AverageOccupancy { get; set; }
    /// <summary>
    /// Gets or sets the average cache hit rate.
    /// </summary>
    /// <value>The average cache hit rate.</value>
    public double AverageCacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the average memory bandwidth.
    /// </summary>
    /// <value>The average memory bandwidth.</value>
    public double AverageMemoryBandwidth { get; set; }
    /// <summary>
    /// Gets or sets the average warp efficiency.
    /// </summary>
    /// <value>The average warp efficiency.</value>
    public double AverageWarpEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the average branch divergence.
    /// </summary>
    /// <value>The average branch divergence.</value>
    public double AverageBranchDivergence { get; set; }
    /// <summary>
    /// Gets or sets the average memory coalescing.
    /// </summary>
    /// <value>The average memory coalescing.</value>
    public double AverageMemoryCoalescing { get; set; }
    /// <summary>
    /// Gets or sets the device distribution.
    /// </summary>
    /// <value>The device distribution.</value>
    public Dictionary<string, int> DeviceDistribution { get; } = [];
    /// <summary>
    /// Gets or sets the performance trend.
    /// </summary>
    /// <value>The performance trend.</value>
    public PerformanceTrend PerformanceTrend { get; set; }
    /// <summary>
    /// Gets or sets the optimization recommendations.
    /// </summary>
    /// <value>The optimization recommendations.</value>
    public IList<string> OptimizationRecommendations { get; } = [];
}
/// <summary>
/// A class that represents memory access analysis result.
/// </summary>

public sealed class MemoryAccessAnalysisResult
{
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <value>The status.</value>
    public AnalysisStatus Status { get; set; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public string? Message { get; set; }
    /// <summary>
    /// Gets or sets the time window.
    /// </summary>
    /// <value>The time window.</value>
    public TimeSpan TimeWindow { get; set; }
    /// <summary>
    /// Gets or sets the total operations.
    /// </summary>
    /// <value>The total operations.</value>
    public int TotalOperations { get; set; }
    /// <summary>
    /// Gets or sets the average bandwidth.
    /// </summary>
    /// <value>The average bandwidth.</value>
    public double AverageBandwidth { get; set; }
    /// <summary>
    /// Gets or sets the peak bandwidth.
    /// </summary>
    /// <value>The peak bandwidth.</value>
    public double PeakBandwidth { get; set; }
    /// <summary>
    /// Gets or sets the total bytes transferred.
    /// </summary>
    /// <value>The total bytes transferred.</value>
    public long TotalBytesTransferred { get; set; }
    /// <summary>
    /// Gets or sets the access pattern distribution.
    /// </summary>
    /// <value>The access pattern distribution.</value>
    public Dictionary<string, int> AccessPatternDistribution { get; } = [];
    /// <summary>
    /// Gets or sets the average coalescing efficiency.
    /// </summary>
    /// <value>The average coalescing efficiency.</value>
    public double AverageCoalescingEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the average cache hit rate.
    /// </summary>
    /// <value>The average cache hit rate.</value>
    public double AverageCacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the transfer direction distribution.
    /// </summary>
    /// <value>The transfer direction distribution.</value>
    public Dictionary<string, int> TransferDirectionDistribution { get; } = [];
    /// <summary>
    /// Gets or sets the device bandwidth utilization.
    /// </summary>
    /// <value>The device bandwidth utilization.</value>
    public Dictionary<string, double> DeviceBandwidthUtilization { get; } = [];
    /// <summary>
    /// Gets or sets the memory segment usage.
    /// </summary>
    /// <value>The memory segment usage.</value>
    public Dictionary<string, MemorySegmentStats> MemorySegmentUsage { get; } = [];
    /// <summary>
    /// Gets or sets the optimization recommendations.
    /// </summary>
    /// <value>The optimization recommendations.</value>
    public IList<string> OptimizationRecommendations { get; } = [];
}
/// <summary>
/// A class that represents memory segment stats.
/// </summary>

public sealed class MemorySegmentStats
{
    /// <summary>
    /// Gets or sets the operation count.
    /// </summary>
    /// <value>The operation count.</value>
    public int OperationCount { get; set; }
    /// <summary>
    /// Gets or sets the total bytes.
    /// </summary>
    /// <value>The total bytes.</value>
    public long TotalBytes { get; set; }
    /// <summary>
    /// Gets or sets the average bandwidth.
    /// </summary>
    /// <value>The average bandwidth.</value>
    public double AverageBandwidth { get; set; }
}
/// <summary>
/// A class that represents profile sample.
/// </summary>

public sealed class ProfileSample
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the active profile count.
    /// </summary>
    /// <value>The active profile count.</value>
    public int ActiveProfileCount { get; set; }
    /// <summary>
    /// Gets or sets the system snapshot.
    /// </summary>
    /// <value>The system snapshot.</value>
    public SystemPerformanceSnapshot SystemSnapshot { get; set; } = new();
}
/// <summary>
/// An profile status enumeration.
/// </summary>

public enum ProfileStatus
{
    Active,
    Completed,
    Error,
    NotFound,
    Cancelled
}
/// <summary>
/// An analysis status enumeration.
/// </summary>

public enum AnalysisStatus
{
    Success,
    NoData,
    Error,
    InProgress
}
/// <summary>
/// An performance trend enumeration.
/// </summary>

public enum PerformanceTrend
{
    Improving,
    Stable,
    Degrading
}