using System;

namespace DotCompute.Memory.Benchmarks;

/// <summary>
/// Comprehensive memory benchmark results.
/// </summary>
public sealed class MemoryBenchmarkResults
{
    /// <summary>
    /// Transfer bandwidth benchmark results.
    /// </summary>
    public TransferBandwidthResults TransferBandwidth { get; set; } = new();
    
    /// <summary>
    /// Allocation overhead benchmark results.
    /// </summary>
    public AllocationOverheadResults AllocationOverhead { get; set; } = new();
    
    /// <summary>
    /// Memory usage pattern benchmark results.
    /// </summary>
    public MemoryUsagePatternResults MemoryUsagePatterns { get; set; } = new();
    
    /// <summary>
    /// Pool performance benchmark results.
    /// </summary>
    public PoolPerformanceResults PoolPerformance { get; set; } = new();
    
    /// <summary>
    /// Unified buffer performance benchmark results.
    /// </summary>
    public UnifiedBufferPerformanceResults UnifiedBufferPerformance { get; set; } = new();
    
    /// <summary>
    /// Overall performance summary.
    /// </summary>
    public PerformanceSummary Summary => new()
    {
        MaxBandwidthGBps = Math.Max(Math.Max(TransferBandwidth.HostToDeviceLarge.BandwidthGBps, 
                                           TransferBandwidth.DeviceToHostLarge.BandwidthGBps),
                                  TransferBandwidth.DeviceToDeviceLarge.BandwidthGBps),
        
        MinAllocationLatencyMs = Math.Min(Math.Min(AllocationOverhead.SingleAllocationSmall.AllocationTime.TotalMilliseconds,
                                                 AllocationOverhead.SingleAllocationMedium.AllocationTime.TotalMilliseconds),
                                        AllocationOverhead.SingleAllocationLarge.AllocationTime.TotalMilliseconds),
        
        PoolEfficiency = PoolPerformance.AllocationEfficiency.EfficiencyRatio,
        
        OverallScore = CalculateOverallScore()
    };
    
    private double CalculateOverallScore()
    {
        // Weighted score based on various performance metrics
        var bandwidthScore = Math.Min(Summary.MaxBandwidthGBps / 10.0, 1.0) * 0.3; // 30% weight
        var latencyScore = Math.Min(1000.0 / Summary.MinAllocationLatencyMs, 1.0) * 0.3; // 30% weight
        var poolScore = Summary.PoolEfficiency * 0.2; // 20% weight
        var coherenceScore = Math.Min(1000.0 / UnifiedBufferPerformance.MemoryCoherencePerformance.AverageCoherenceTime.TotalMilliseconds, 1.0) * 0.2; // 20% weight
        
        return (bandwidthScore + latencyScore + poolScore + coherenceScore) * 100.0;
    }
}

/// <summary>
/// Transfer bandwidth benchmark results.
/// </summary>
public sealed class TransferBandwidthResults
{
    public BandwidthMeasurement HostToDeviceSmall { get; set; }
    public BandwidthMeasurement HostToDeviceMedium { get; set; }
    public BandwidthMeasurement HostToDeviceLarge { get; set; }
    public BandwidthMeasurement DeviceToHostSmall { get; set; }
    public BandwidthMeasurement DeviceToHostMedium { get; set; }
    public BandwidthMeasurement DeviceToHostLarge { get; set; }
    public BandwidthMeasurement DeviceToDeviceSmall { get; set; }
    public BandwidthMeasurement DeviceToDeviceMedium { get; set; }
    public BandwidthMeasurement DeviceToDeviceLarge { get; set; }
}

/// <summary>
/// Allocation overhead benchmark results.
/// </summary>
public sealed class AllocationOverheadResults
{
    public AllocationMeasurement SingleAllocationSmall { get; set; }
    public AllocationMeasurement SingleAllocationMedium { get; set; }
    public AllocationMeasurement SingleAllocationLarge { get; set; }
    public AllocationMeasurement BulkAllocationSmall { get; set; }
    public AllocationMeasurement BulkAllocationMedium { get; set; }
    public AllocationMeasurement BulkAllocationLarge { get; set; }
}

/// <summary>
/// Memory usage pattern benchmark results.
/// </summary>
public sealed class MemoryUsagePatternResults
{
    public FragmentationMeasurement FragmentationImpact { get; set; }
    public ConcurrentAllocationMeasurement ConcurrentAllocation { get; set; }
    public MemoryPressureMeasurement MemoryPressureHandling { get; set; }
}

/// <summary>
/// Pool performance benchmark results.
/// </summary>
public sealed class PoolPerformanceResults
{
    public PoolEfficiencyMeasurement AllocationEfficiency { get; set; }
    public PoolReuseMeasurement ReuseRate { get; set; }
    public PoolMemoryOverheadMeasurement MemoryOverhead { get; set; }
}

/// <summary>
/// Unified buffer performance benchmark results.
/// </summary>
public sealed class UnifiedBufferPerformanceResults
{
    public LazySyncMeasurement LazySyncEfficiency { get; set; }
    public StateTransitionMeasurement StateTransitionOverhead { get; set; }
    public CoherenceMeasurement MemoryCoherencePerformance { get; set; }
}

/// <summary>
/// Bandwidth measurement result.
/// </summary>
public readonly struct BandwidthMeasurement
{
    public long TotalBytes { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public double BandwidthGBps { get; init; }
    public int IterationCount { get; init; }
    
    public double LatencyMs => ElapsedTime.TotalMilliseconds / IterationCount;
    public double ThroughputMBps => BandwidthGBps * 1024.0;
}

/// <summary>
/// Allocation measurement result.
/// </summary>
public readonly struct AllocationMeasurement
{
    public TimeSpan AllocationTime { get; init; }
    public TimeSpan DeallocationTime { get; init; }
    public int AllocationCount { get; init; }
    public long TotalBytes { get; init; }
    public double AllocationsPerSecond { get; init; }
    public double DeallocationsPerSecond { get; init; }
    
    public double AverageAllocationLatencyMs => AllocationTime.TotalMilliseconds / AllocationCount;
    public double AverageDeallocationLatencyMs => DeallocationTime.TotalMilliseconds / AllocationCount;
    public double TotalLatencyMs => AllocationTime.TotalMilliseconds + DeallocationTime.TotalMilliseconds;
}

/// <summary>
/// Fragmentation measurement result.
/// </summary>
public readonly struct FragmentationMeasurement
{
    public TimeSpan FragmentationSetupTime { get; init; }
    public TimeSpan FragmentedAllocationTime { get; init; }
    public int SuccessfulAllocations { get; init; }
    public double FragmentationLevel { get; init; }
    
    public double FragmentationImpactRatio => FragmentedAllocationTime.TotalMilliseconds / FragmentationSetupTime.TotalMilliseconds;
    public double AllocationSuccessRate => SuccessfulAllocations / 50.0; // Expected 50 allocations
}

/// <summary>
/// Concurrent allocation measurement result.
/// </summary>
public readonly struct ConcurrentAllocationMeasurement
{
    public int ThreadCount { get; init; }
    public TimeSpan TotalTime { get; init; }
    public int TotalAllocations { get; init; }
    public int TotalErrors { get; init; }
    public double AllocationsPerSecond { get; init; }
    
    public double ErrorRate => (double)TotalErrors / ThreadCount;
    public double ScalingEfficiency => AllocationsPerSecond / ThreadCount;
}

/// <summary>
/// Memory pressure measurement result.
/// </summary>
public readonly struct MemoryPressureMeasurement
{
    public TimeSpan TimeToReachPressure { get; init; }
    public int AllocationsAtPressure { get; init; }
    public double MemoryPressureLevel { get; init; }
    public long AvailableMemoryAtPressure { get; init; }
    
    public double PressureBuilupRate => AllocationsAtPressure / TimeToReachPressure.TotalSeconds;
    public double MemoryUtilization => 1.0 - (double)AvailableMemoryAtPressure / (AvailableMemoryAtPressure + AllocationsAtPressure * 1024 * 1024);
}

/// <summary>
/// Pool efficiency measurement result.
/// </summary>
public readonly struct PoolEfficiencyMeasurement
{
    public TimeSpan AllocationTime { get; init; }
    public int AllocationCount { get; init; }
    public double EfficiencyRatio { get; init; }
    public long TotalRetainedBytes { get; init; }
    
    public double AllocationsPerSecond => AllocationCount / AllocationTime.TotalSeconds;
    public double AverageAllocationLatencyMs => AllocationTime.TotalMilliseconds / AllocationCount;
}

/// <summary>
/// Pool reuse measurement result.
/// </summary>
public readonly struct PoolReuseMeasurement
{
    public TimeSpan ReuseTime { get; init; }
    public int ReuseCount { get; init; }
    public double ReuseRate { get; init; }
    public double ReusePerSecond { get; init; }
    
    public double ReuseLatencyMs => ReuseTime.TotalMilliseconds / ReuseCount;
}

/// <summary>
/// Pool memory overhead measurement result.
/// </summary>
public readonly struct PoolMemoryOverheadMeasurement
{
    public long RetainedBytes { get; init; }
    public long AllocatedBytes { get; init; }
    public double OverheadRatio { get; init; }
    public int BucketCount { get; init; }
    
    public double MemoryEfficiency => 1.0 - OverheadRatio;
    public double AverageRetainedPerBucket => (double)RetainedBytes / BucketCount;
}

/// <summary>
/// Lazy synchronization measurement result.
/// </summary>
public readonly struct LazySyncMeasurement
{
    public TimeSpan HostAllocationTime { get; init; }
    public TimeSpan DeviceAllocationTime { get; init; }
    public TimeSpan LazySyncTime { get; init; }
    public double SyncEfficiencyRatio { get; init; }
    
    public TimeSpan TotalSetupTime => HostAllocationTime + DeviceAllocationTime;
    public double SyncOverheadRatio => LazySyncTime.TotalMilliseconds / TotalSetupTime.TotalMilliseconds;
}

/// <summary>
/// State transition measurement result.
/// </summary>
public readonly struct StateTransitionMeasurement
{
    public (BufferState From, BufferState To, TimeSpan Duration)[] Transitions { get; init; }
    public TimeSpan AverageTransitionTime { get; init; }
    public int TotalTransitions { get; init; }
    
    public double TransitionEfficiency => 1.0 / AverageTransitionTime.TotalMilliseconds;
}

/// <summary>
/// Memory coherence measurement result.
/// </summary>
public readonly struct CoherenceMeasurement
{
    public TimeSpan TotalCoherenceTime { get; init; }
    public int CoherenceOperations { get; init; }
    public TimeSpan AverageCoherenceTime { get; init; }
    
    public double CoherenceOperationsPerSecond => CoherenceOperations / TotalCoherenceTime.TotalSeconds;
    public double CoherenceEfficiency => 1.0 / AverageCoherenceTime.TotalMilliseconds;
}

/// <summary>
/// Overall performance summary.
/// </summary>
public readonly struct PerformanceSummary
{
    public double MaxBandwidthGBps { get; init; }
    public double MinAllocationLatencyMs { get; init; }
    public double PoolEfficiency { get; init; }
    public double OverallScore { get; init; }
    
    public string PerformanceGrade => OverallScore switch
    {
        >= 90 => "A+ (Excellent)",
        >= 80 => "A (Very Good)",
        >= 70 => "B (Good)",
        >= 60 => "C (Average)",
        >= 50 => "D (Below Average)",
        _ => "F (Poor)"
    };
}