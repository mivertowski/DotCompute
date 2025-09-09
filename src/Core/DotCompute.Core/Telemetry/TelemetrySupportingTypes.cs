using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Core.Telemetry.System;
using DotCompute.Core.Telemetry.Options;

namespace DotCompute.Core.Telemetry;

// Supporting types for DistributedTracer.cs
public sealed class DistributedTracingOptions
{
    public int MaxTracesPerExport { get; set; } = 1000;
    public int TraceRetentionHours { get; set; } = 24;
    public bool EnableSampling { get; set; } = true;
    public double SamplingRate { get; set; } = 0.1; // 10% sampling
}

public enum TraceExportFormat
{
    OpenTelemetry,
    Jaeger,
    Zipkin,
    Custom
}

public sealed class TraceContext
{
    public string TraceId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public Activity? Activity { get; set; }
    public SpanContext? ParentSpanContext { get; set; }
    public Dictionary<string, object?> Tags { get; set; } = [];
    public ConcurrentBag<SpanData> Spans { get; set; } = [];
    public ConcurrentDictionary<string, DeviceOperationTrace> DeviceOperations { get; set; } = new();
}

public sealed class SpanContext
{
    public string SpanId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string SpanName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public SpanKind SpanKind { get; set; } = SpanKind.Internal;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public SpanStatus Status { get; set; } = SpanStatus.Ok;
    public string? StatusMessage { get; set; }
    public Dictionary<string, object?> Attributes { get; set; } = [];
    public List<SpanEvent> Events { get; set; } = [];
    public string? ParentSpanId { get; set; }
    public Activity? Activity { get; set; }
}

public sealed class SpanData
{
    public string SpanId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string SpanName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public SpanKind SpanKind { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public SpanStatus Status { get; set; }
    public string? StatusMessage { get; set; }
    public Dictionary<string, object?> Attributes { get; set; } = [];
    public List<SpanEvent> Events { get; set; } = [];
    public string? ParentSpanId { get; set; }
}

public sealed class SpanEvent
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<string, object?> Attributes { get; set; } = [];
}

public sealed class DeviceOperationTrace
{
    public string DeviceId { get; set; } = string.Empty;
    public int OperationCount { get; set; }
    public DateTimeOffset FirstOperationTime { get; set; }
    public DateTimeOffset LastOperationTime { get; set; }
    public ConcurrentBag<SpanContext> ActiveSpans { get; set; } = [];
}

public sealed class TraceData
{
    public string TraceId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TraceStatus Status { get; set; }
    public List<SpanData> Spans { get; set; } = [];
    public Dictionary<string, DeviceOperationTrace> DeviceOperations { get; set; } = [];
    public Dictionary<string, object?> Tags { get; set; } = [];
    public TraceAnalysis? Analysis { get; set; }
}

public sealed class TraceAnalysis
{
    public string TraceId { get; set; } = string.Empty;
    public DateTimeOffset AnalysisTimestamp { get; set; }
    public TimeSpan TotalOperationTime { get; set; }
    public int SpanCount { get; set; }
    public int DeviceCount { get; set; }
    public List<SpanData> CriticalPath { get; set; } = [];
    public double CriticalPathDuration { get; set; }
    public Dictionary<string, double> DeviceUtilization { get; set; } = [];
    public List<PerformanceBottleneck> Bottlenecks { get; set; } = [];
    public Dictionary<string, object> MemoryAccessPatterns { get; set; } = [];
    public double ParallelismEfficiency { get; set; }
    public double DeviceEfficiency { get; set; }
    public List<string> OptimizationRecommendations { get; set; } = [];
}

public sealed class KernelPerformanceData
{
    public double ThroughputOpsPerSecond { get; set; }
    public double OccupancyPercentage { get; set; }
    public double CacheHitRate { get; set; }
    public double InstructionThroughput { get; set; }
    public double MemoryBandwidthGBPerSecond { get; set; }
}

public enum SpanKind
{
    Internal,
    Server,
    Client,
    Producer,
    Consumer
}

public enum SpanStatus
{
    Ok,
    Error,
    Timeout,
    Cancelled
}

public enum TraceStatus
{
    Ok,
    Error,
    Timeout,
    PartialFailure
}

// Supporting types for PerformanceProfiler.cs
public sealed class PerformanceProfilerOptions
{
    public int MaxConcurrentProfiles { get; set; } = 10;
    public bool EnableContinuousProfiling { get; set; } = true;
    public int SamplingIntervalMs { get; set; } = 100;
    public bool AllowOrphanedRecords { get; set; }
}


public sealed class ActiveProfile
{
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public ProfileOptions Options { get; set; } = new();
    public ConcurrentBag<KernelExecutionProfile> KernelExecutions { get; set; } = [];
    public ConcurrentBag<MemoryOperationProfile> MemoryOperations { get; set; } = [];
    public ConcurrentDictionary<string, DeviceProfileMetrics> DeviceMetrics { get; set; } = new();
    public ConcurrentQueue<SystemSnapshot> SystemSnapshots { get; set; } = new();
}

public sealed class PerformanceProfile
{
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public ProfileStatus Status { get; set; }
    public string? Message { get; set; }
    public int TotalKernelExecutions { get; set; }
    public int TotalMemoryOperations { get; set; }
    public int DevicesInvolved { get; set; }
    public ProfileAnalysis? Analysis { get; set; }
    public List<KernelExecutionProfile> KernelExecutions { get; set; } = [];
    public List<MemoryOperationProfile> MemoryOperations { get; set; } = [];
    public Dictionary<string, DeviceProfileMetrics> DeviceMetrics { get; set; } = [];
}

public sealed class KernelExecutionProfile
{
    public string KernelName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public double ThroughputOpsPerSecond { get; set; }
    public double OccupancyPercentage { get; set; }
    public double InstructionThroughput { get; set; }
    public double MemoryBandwidthGBPerSecond { get; set; }
    public double CacheHitRate { get; set; }
    public double MemoryCoalescingEfficiency { get; set; }
    public int ComputeUnitsUsed { get; set; }
    public int RegistersPerThread { get; set; }
    public long SharedMemoryUsed { get; set; }
    public double WarpEfficiency { get; set; }
    public double BranchDivergence { get; set; }
    public double MemoryLatency { get; set; }
    public double PowerConsumption { get; set; }
}

public sealed class MemoryOperationProfile
{
    public string OperationType { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public long BytesTransferred { get; set; }
    public double BandwidthGBPerSecond { get; set; }
    public string AccessPattern { get; set; } = string.Empty;
    public double CoalescingEfficiency { get; set; }
    public double CacheHitRate { get; set; }
    public string MemorySegment { get; set; } = string.Empty;
    public string TransferDirection { get; set; } = string.Empty;
    public int QueueDepth { get; set; }
}

public sealed class DeviceProfileMetrics
{
    public double UtilizationPercentage { get; set; }
    public double TemperatureCelsius { get; set; }
    public long MemoryUsageBytes { get; set; }
    public double PowerConsumptionWatts { get; set; }
}

public sealed class SystemSnapshot
{
    public DateTimeOffset Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public int ThreadCount { get; set; }
}

public sealed class KernelExecutionMetrics
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public double ThroughputOpsPerSecond { get; set; }
    public double OccupancyPercentage { get; set; }
    public double InstructionThroughput { get; set; }
    public double MemoryBandwidthGBPerSecond { get; set; }
    public double CacheHitRate { get; set; }
    public double MemoryCoalescingEfficiency { get; set; }
    public int ComputeUnitsUsed { get; set; }
    public int RegistersPerThread { get; set; }
    public long SharedMemoryUsed { get; set; }
    public double WarpEfficiency { get; set; }
    public double BranchDivergence { get; set; }
    public double MemoryLatency { get; set; }
    public double PowerConsumption { get; set; }
}

public sealed class MemoryOperationMetrics
{
    public DateTimeOffset StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public long BytesTransferred { get; set; }
    public double BandwidthGBPerSecond { get; set; }
    public string AccessPattern { get; set; } = string.Empty;
    public double CoalescingEfficiency { get; set; }
    public double CacheHitRate { get; set; }
    public string MemorySegment { get; set; } = string.Empty;
    public string TransferDirection { get; set; } = string.Empty;
    public int QueueDepth { get; set; }
}

public sealed class ProfileAnalysis
{
    public DateTimeOffset AnalysisTimestamp { get; set; }
    public double TotalExecutionTime { get; set; }
    public double AverageKernelExecutionTime { get; set; }
    public double OverallThroughput { get; set; }
    public long TotalMemoryTransferred { get; set; }
    public double AverageMemoryBandwidth { get; set; }
    public double AverageOccupancy { get; set; }
    public double AverageCacheHitRate { get; set; }
    public double DeviceUtilizationEfficiency { get; set; }
    public double ParallelismEfficiency { get; set; }
    public List<string> IdentifiedBottlenecks { get; set; } = [];
    public List<string> OptimizationRecommendations { get; set; } = [];
}

public sealed class KernelAnalysisResult
{
    public string KernelName { get; set; } = string.Empty;
    public AnalysisStatus Status { get; set; }
    public string? Message { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public int ExecutionCount { get; set; }
    public double AverageExecutionTime { get; set; }
    public double MinExecutionTime { get; set; }
    public double MaxExecutionTime { get; set; }
    public double ExecutionTimeStdDev { get; set; }
    public double AverageThroughput { get; set; }
    public double AverageOccupancy { get; set; }
    public double AverageCacheHitRate { get; set; }
    public double AverageMemoryBandwidth { get; set; }
    public double AverageWarpEfficiency { get; set; }
    public double AverageBranchDivergence { get; set; }
    public double AverageMemoryCoalescing { get; set; }
    public Dictionary<string, int> DeviceDistribution { get; set; } = [];
    public PerformanceTrend PerformanceTrend { get; set; }
    public List<string> OptimizationRecommendations { get; set; } = [];
}

public sealed class MemoryAccessAnalysisResult
{
    public AnalysisStatus Status { get; set; }
    public string? Message { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public int TotalOperations { get; set; }
    public double AverageBandwidth { get; set; }
    public double PeakBandwidth { get; set; }
    public long TotalBytesTransferred { get; set; }
    public Dictionary<string, int> AccessPatternDistribution { get; set; } = [];
    public double AverageCoalescingEfficiency { get; set; }
    public double AverageCacheHitRate { get; set; }
    public Dictionary<string, int> TransferDirectionDistribution { get; set; } = [];
    public Dictionary<string, double> DeviceBandwidthUtilization { get; set; } = [];
    public Dictionary<string, MemorySegmentStats> MemorySegmentUsage { get; set; } = [];
    public List<string> OptimizationRecommendations { get; set; } = [];
}

public sealed class MemorySegmentStats
{
    public int OperationCount { get; set; }
    public long TotalBytes { get; set; }
    public double AverageBandwidth { get; set; }
}

public sealed class ProfileSample
{
    public DateTimeOffset Timestamp { get; set; }
    public int ActiveProfileCount { get; set; }
    public SystemPerformanceSnapshot SystemSnapshot { get; set; } = new();
}

public enum ProfileStatus
{
    Active,
    Completed,
    Error,
    NotFound,
    Cancelled
}

public enum AnalysisStatus
{
    Success,
    NoData,
    Error,
    InProgress
}

public enum PerformanceTrend
{
    Improving,
    Stable,
    Degrading
}