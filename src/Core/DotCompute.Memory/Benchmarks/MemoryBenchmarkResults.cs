// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

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
    /// <summary>
    /// Gets or sets the host to device small.
    /// </summary>
    /// <value>
    /// The host to device small.
    /// </value>
    public BandwidthMeasurement HostToDeviceSmall { get; set; }

    /// <summary>
    /// Gets or sets the host to device medium.
    /// </summary>
    /// <value>
    /// The host to device medium.
    /// </value>
    public BandwidthMeasurement HostToDeviceMedium { get; set; }

    /// <summary>
    /// Gets or sets the host to device large.
    /// </summary>
    /// <value>
    /// The host to device large.
    /// </value>
    public BandwidthMeasurement HostToDeviceLarge { get; set; }

    /// <summary>
    /// Gets or sets the device to host small.
    /// </summary>
    /// <value>
    /// The device to host small.
    /// </value>
    public BandwidthMeasurement DeviceToHostSmall { get; set; }

    /// <summary>
    /// Gets or sets the device to host medium.
    /// </summary>
    /// <value>
    /// The device to host medium.
    /// </value>
    public BandwidthMeasurement DeviceToHostMedium { get; set; }

    /// <summary>
    /// Gets or sets the device to host large.
    /// </summary>
    /// <value>
    /// The device to host large.
    /// </value>
    public BandwidthMeasurement DeviceToHostLarge { get; set; }

    /// <summary>
    /// Gets or sets the device to device small.
    /// </summary>
    /// <value>
    /// The device to device small.
    /// </value>
    public BandwidthMeasurement DeviceToDeviceSmall { get; set; }

    /// <summary>
    /// Gets or sets the device to device medium.
    /// </summary>
    /// <value>
    /// The device to device medium.
    /// </value>
    public BandwidthMeasurement DeviceToDeviceMedium { get; set; }

    /// <summary>
    /// Gets or sets the device to device large.
    /// </summary>
    /// <value>
    /// The device to device large.
    /// </value>
    public BandwidthMeasurement DeviceToDeviceLarge { get; set; }
}

/// <summary>
/// Allocation overhead benchmark results.
/// </summary>
public sealed class AllocationOverheadResults
{
    /// <summary>
    /// Gets or sets the single allocation small.
    /// </summary>
    /// <value>
    /// The single allocation small.
    /// </value>
    public AllocationMeasurement SingleAllocationSmall { get; set; }

    /// <summary>
    /// Gets or sets the single allocation medium.
    /// </summary>
    /// <value>
    /// The single allocation medium.
    /// </value>
    public AllocationMeasurement SingleAllocationMedium { get; set; }

    /// <summary>
    /// Gets or sets the single allocation large.
    /// </summary>
    /// <value>
    /// The single allocation large.
    /// </value>
    public AllocationMeasurement SingleAllocationLarge { get; set; }

    /// <summary>
    /// Gets or sets the bulk allocation small.
    /// </summary>
    /// <value>
    /// The bulk allocation small.
    /// </value>
    public AllocationMeasurement BulkAllocationSmall { get; set; }

    /// <summary>
    /// Gets or sets the bulk allocation medium.
    /// </summary>
    /// <value>
    /// The bulk allocation medium.
    /// </value>
    public AllocationMeasurement BulkAllocationMedium { get; set; }

    /// <summary>
    /// Gets or sets the bulk allocation large.
    /// </summary>
    /// <value>
    /// The bulk allocation large.
    /// </value>
    public AllocationMeasurement BulkAllocationLarge { get; set; }
}

/// <summary>
/// Memory usage pattern benchmark results.
/// </summary>
public sealed class MemoryUsagePatternResults
{
    /// <summary>
    /// Gets or sets the fragmentation impact.
    /// </summary>
    /// <value>
    /// The fragmentation impact.
    /// </value>
    public FragmentationMeasurement FragmentationImpact { get; set; }

    /// <summary>
    /// Gets or sets the concurrent allocation.
    /// </summary>
    /// <value>
    /// The concurrent allocation.
    /// </value>
    public ConcurrentAllocationMeasurement ConcurrentAllocation { get; set; }

    /// <summary>
    /// Gets or sets the memory pressure handling.
    /// </summary>
    /// <value>
    /// The memory pressure handling.
    /// </value>
    public MemoryPressureMeasurement MemoryPressureHandling { get; set; }
}

/// <summary>
/// Pool performance benchmark results.
/// </summary>
public sealed class PoolPerformanceResults
{
    /// <summary>
    /// Gets or sets the allocation efficiency.
    /// </summary>
    /// <value>
    /// The allocation efficiency.
    /// </value>
    public PoolEfficiencyMeasurement AllocationEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the reuse rate.
    /// </summary>
    /// <value>
    /// The reuse rate.
    /// </value>
    public PoolReuseMeasurement ReuseRate { get; set; }

    /// <summary>
    /// Gets or sets the memory overhead.
    /// </summary>
    /// <value>
    /// The memory overhead.
    /// </value>
    public PoolMemoryOverheadMeasurement MemoryOverhead { get; set; }
}

/// <summary>
/// Unified buffer performance benchmark results.
/// </summary>
public sealed class UnifiedBufferPerformanceResults
{
    /// <summary>
    /// Gets or sets the lazy synchronize efficiency.
    /// </summary>
    /// <value>
    /// The lazy synchronize efficiency.
    /// </value>
    public LazySyncMeasurement LazySyncEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the state transition overhead.
    /// </summary>
    /// <value>
    /// The state transition overhead.
    /// </value>
    public StateTransitionMeasurement StateTransitionOverhead { get; set; }

    /// <summary>
    /// Gets or sets the memory coherence performance.
    /// </summary>
    /// <value>
    /// The memory coherence performance.
    /// </value>
    public CoherenceMeasurement MemoryCoherencePerformance { get; set; }
}

/// <summary>
/// Bandwidth measurement result.
/// </summary>
public readonly struct BandwidthMeasurement : IEquatable<BandwidthMeasurement>
{
    /// <summary>
    /// Gets the total bytes.
    /// </summary>
    /// <value>
    /// The total bytes.
    /// </value>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Gets the elapsed time.
    /// </summary>
    /// <value>
    /// The elapsed time.
    /// </value>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Gets the bandwidth g BPS.
    /// </summary>
    /// <value>
    /// The bandwidth g BPS.
    /// </value>
    public double BandwidthGBps { get; init; }

    /// <summary>
    /// Gets the iteration count.
    /// </summary>
    /// <value>
    /// The iteration count.
    /// </value>
    public int IterationCount { get; init; }

    /// <summary>
    /// Gets the latency ms.
    /// </summary>
    /// <value>
    /// The latency ms.
    /// </value>
    public double LatencyMs => ElapsedTime.TotalMilliseconds / IterationCount;

    /// <summary>
    /// Gets the throughput m BPS.
    /// </summary>
    /// <value>
    /// The throughput m BPS.
    /// </value>
    public double ThroughputMBps => BandwidthGBps * 1024.0;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is BandwidthMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(BandwidthMeasurement other)
    {
        return TotalBytes == other.TotalBytes &&
               ElapsedTime.Equals(other.ElapsedTime) &&
               BandwidthGBps.Equals(other.BandwidthGBps) &&
               IterationCount == other.IterationCount;
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(TotalBytes, ElapsedTime, BandwidthGBps, IterationCount);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(BandwidthMeasurement left, BandwidthMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(BandwidthMeasurement left, BandwidthMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Allocation measurement result.
/// </summary>
public readonly struct AllocationMeasurement : IEquatable<AllocationMeasurement>
{
    /// <summary>
    /// Gets the allocation time.
    /// </summary>
    /// <value>
    /// The allocation time.
    /// </value>
    public TimeSpan AllocationTime { get; init; }

    /// <summary>
    /// Gets the deallocation time.
    /// </summary>
    /// <value>
    /// The deallocation time.
    /// </value>
    public TimeSpan DeallocationTime { get; init; }

    /// <summary>
    /// Gets the allocation count.
    /// </summary>
    /// <value>
    /// The allocation count.
    /// </value>
    public int AllocationCount { get; init; }

    /// <summary>
    /// Gets the total bytes.
    /// </summary>
    /// <value>
    /// The total bytes.
    /// </value>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Gets the allocations per second.
    /// </summary>
    /// <value>
    /// The allocations per second.
    /// </value>
    public double AllocationsPerSecond { get; init; }

    /// <summary>
    /// Gets the deallocations per second.
    /// </summary>
    /// <value>
    /// The deallocations per second.
    /// </value>
    public double DeallocationsPerSecond { get; init; }

    /// <summary>
    /// Gets the average allocation latency ms.
    /// </summary>
    /// <value>
    /// The average allocation latency ms.
    /// </value>
    public double AverageAllocationLatencyMs => AllocationTime.TotalMilliseconds / AllocationCount;

    /// <summary>
    /// Gets the average deallocation latency ms.
    /// </summary>
    /// <value>
    /// The average deallocation latency ms.
    /// </value>
    public double AverageDeallocationLatencyMs => DeallocationTime.TotalMilliseconds / AllocationCount;

    /// <summary>
    /// Gets the total latency ms.
    /// </summary>
    /// <value>
    /// The total latency ms.
    /// </value>
    public double TotalLatencyMs => AllocationTime.TotalMilliseconds + DeallocationTime.TotalMilliseconds;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is AllocationMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(AllocationMeasurement other)
    {
        return AllocationTime.Equals(other.AllocationTime) &&
               DeallocationTime.Equals(other.DeallocationTime) &&
               AllocationCount == other.AllocationCount &&
               TotalBytes == other.TotalBytes &&
               AllocationsPerSecond.Equals(other.AllocationsPerSecond) &&
               DeallocationsPerSecond.Equals(other.DeallocationsPerSecond);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(
        HashCode.Combine(AllocationTime, DeallocationTime, AllocationCount),
        HashCode.Combine(TotalBytes, AllocationsPerSecond, DeallocationsPerSecond));

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(AllocationMeasurement left, AllocationMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(AllocationMeasurement left, AllocationMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Fragmentation measurement result.
/// </summary>
public readonly struct FragmentationMeasurement : IEquatable<FragmentationMeasurement>
{
    /// <summary>
    /// Gets the fragmentation setup time.
    /// </summary>
    /// <value>
    /// The fragmentation setup time.
    /// </value>
    public TimeSpan FragmentationSetupTime { get; init; }

    /// <summary>
    /// Gets the fragmented allocation time.
    /// </summary>
    /// <value>
    /// The fragmented allocation time.
    /// </value>
    public TimeSpan FragmentedAllocationTime { get; init; }

    /// <summary>
    /// Gets the successful allocations.
    /// </summary>
    /// <value>
    /// The successful allocations.
    /// </value>
    public int SuccessfulAllocations { get; init; }

    /// <summary>
    /// Gets the fragmentation level.
    /// </summary>
    /// <value>
    /// The fragmentation level.
    /// </value>
    public double FragmentationLevel { get; init; }

    /// <summary>
    /// Gets the fragmentation impact ratio.
    /// </summary>
    /// <value>
    /// The fragmentation impact ratio.
    /// </value>
    public double FragmentationImpactRatio => FragmentedAllocationTime.TotalMilliseconds / FragmentationSetupTime.TotalMilliseconds;

    /// <summary>
    /// Gets the allocation success rate.
    /// </summary>
    /// <value>
    /// The allocation success rate.
    /// </value>
    public double AllocationSuccessRate => SuccessfulAllocations / 50.0; // Expected 50 allocations

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is FragmentationMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(FragmentationMeasurement other)
    {
        return FragmentationSetupTime.Equals(other.FragmentationSetupTime) &&
               FragmentedAllocationTime.Equals(other.FragmentedAllocationTime) &&
               SuccessfulAllocations == other.SuccessfulAllocations &&
               FragmentationLevel.Equals(other.FragmentationLevel);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(FragmentationSetupTime, FragmentedAllocationTime, SuccessfulAllocations, FragmentationLevel);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(FragmentationMeasurement left, FragmentationMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(FragmentationMeasurement left, FragmentationMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Concurrent allocation measurement result.
/// </summary>
public readonly struct ConcurrentAllocationMeasurement : IEquatable<ConcurrentAllocationMeasurement>
{
    /// <summary>
    /// Gets the thread count.
    /// </summary>
    /// <value>
    /// The thread count.
    /// </value>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gets the total time.
    /// </summary>
    /// <value>
    /// The total time.
    /// </value>
    public TimeSpan TotalTime { get; init; }

    /// <summary>
    /// Gets the total allocations.
    /// </summary>
    /// <value>
    /// The total allocations.
    /// </value>
    public int TotalAllocations { get; init; }

    /// <summary>
    /// Gets the total errors.
    /// </summary>
    /// <value>
    /// The total errors.
    /// </value>
    public int TotalErrors { get; init; }

    /// <summary>
    /// Gets the allocations per second.
    /// </summary>
    /// <value>
    /// The allocations per second.
    /// </value>
    public double AllocationsPerSecond { get; init; }

    /// <summary>
    /// Gets the error rate.
    /// </summary>
    /// <value>
    /// The error rate.
    /// </value>
    public double ErrorRate => (double)TotalErrors / ThreadCount;

    /// <summary>
    /// Gets the scaling efficiency.
    /// </summary>
    /// <value>
    /// The scaling efficiency.
    /// </value>
    public double ScalingEfficiency => AllocationsPerSecond / ThreadCount;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is ConcurrentAllocationMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(ConcurrentAllocationMeasurement other)
    {
        return ThreadCount == other.ThreadCount &&
               TotalTime.Equals(other.TotalTime) &&
               TotalAllocations == other.TotalAllocations &&
               TotalErrors == other.TotalErrors &&
               AllocationsPerSecond.Equals(other.AllocationsPerSecond);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(ThreadCount, TotalTime, TotalAllocations, TotalErrors, AllocationsPerSecond);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(ConcurrentAllocationMeasurement left, ConcurrentAllocationMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(ConcurrentAllocationMeasurement left, ConcurrentAllocationMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Memory pressure measurement result.
/// </summary>
public readonly struct MemoryPressureMeasurement : IEquatable<MemoryPressureMeasurement>
{
    /// <summary>
    /// Gets the time to reach pressure.
    /// </summary>
    /// <value>
    /// The time to reach pressure.
    /// </value>
    public TimeSpan TimeToReachPressure { get; init; }

    /// <summary>
    /// Gets the allocations at pressure.
    /// </summary>
    /// <value>
    /// The allocations at pressure.
    /// </value>
    public int AllocationsAtPressure { get; init; }

    /// <summary>
    /// Gets the memory pressure level.
    /// </summary>
    /// <value>
    /// The memory pressure level.
    /// </value>
    public double MemoryPressureLevel { get; init; }

    /// <summary>
    /// Gets the available memory at pressure.
    /// </summary>
    /// <value>
    /// The available memory at pressure.
    /// </value>
    public long AvailableMemoryAtPressure { get; init; }

    /// <summary>
    /// Gets the pressure builup rate.
    /// </summary>
    /// <value>
    /// The pressure builup rate.
    /// </value>
    public double PressureBuilupRate => AllocationsAtPressure / TimeToReachPressure.TotalSeconds;

    /// <summary>
    /// Gets the memory utilization.
    /// </summary>
    /// <value>
    /// The memory utilization.
    /// </value>
    public double MemoryUtilization => 1.0 - (double)AvailableMemoryAtPressure / (AvailableMemoryAtPressure + AllocationsAtPressure * 1024 * 1024);

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is MemoryPressureMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(MemoryPressureMeasurement other) => TimeToReachPressure.Equals(other.TimeToReachPressure) && AllocationsAtPressure == other.AllocationsAtPressure && MemoryPressureLevel.Equals(other.MemoryPressureLevel) && AvailableMemoryAtPressure == other.AvailableMemoryAtPressure;

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(TimeToReachPressure, AllocationsAtPressure, MemoryPressureLevel, AvailableMemoryAtPressure);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(MemoryPressureMeasurement left, MemoryPressureMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(MemoryPressureMeasurement left, MemoryPressureMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Pool efficiency measurement result.
/// </summary>
public readonly struct PoolEfficiencyMeasurement : IEquatable<PoolEfficiencyMeasurement>
{
    /// <summary>
    /// Gets the allocation time.
    /// </summary>
    /// <value>
    /// The allocation time.
    /// </value>
    public TimeSpan AllocationTime { get; init; }

    /// <summary>
    /// Gets the allocation count.
    /// </summary>
    /// <value>
    /// The allocation count.
    /// </value>
    public int AllocationCount { get; init; }

    /// <summary>
    /// Gets the efficiency ratio.
    /// </summary>
    /// <value>
    /// The efficiency ratio.
    /// </value>
    public double EfficiencyRatio { get; init; }

    /// <summary>
    /// Gets the total retained bytes.
    /// </summary>
    /// <value>
    /// The total retained bytes.
    /// </value>
    public long TotalRetainedBytes { get; init; }

    /// <summary>
    /// Gets the allocations per second.
    /// </summary>
    /// <value>
    /// The allocations per second.
    /// </value>
    public double AllocationsPerSecond => AllocationCount / AllocationTime.TotalSeconds;

    /// <summary>
    /// Gets the average allocation latency ms.
    /// </summary>
    /// <value>
    /// The average allocation latency ms.
    /// </value>
    public double AverageAllocationLatencyMs => AllocationTime.TotalMilliseconds / AllocationCount;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is PoolEfficiencyMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(PoolEfficiencyMeasurement other) => AllocationTime.Equals(other.AllocationTime) && AllocationCount == other.AllocationCount && EfficiencyRatio.Equals(other.EfficiencyRatio) && TotalRetainedBytes == other.TotalRetainedBytes;

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(AllocationTime, AllocationCount, EfficiencyRatio, TotalRetainedBytes);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(PoolEfficiencyMeasurement left, PoolEfficiencyMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(PoolEfficiencyMeasurement left, PoolEfficiencyMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Pool reuse measurement result.
/// </summary>
public readonly struct PoolReuseMeasurement : IEquatable<PoolReuseMeasurement>
{
    /// <summary>
    /// Gets the reuse time.
    /// </summary>
    /// <value>
    /// The reuse time.
    /// </value>
    public TimeSpan ReuseTime { get; init; }

    /// <summary>
    /// Gets the reuse count.
    /// </summary>
    /// <value>
    /// The reuse count.
    /// </value>
    public int ReuseCount { get; init; }

    /// <summary>
    /// Gets the reuse rate.
    /// </summary>
    /// <value>
    /// The reuse rate.
    /// </value>
    public double ReuseRate { get; init; }

    /// <summary>
    /// Gets the reuse per second.
    /// </summary>
    /// <value>
    /// The reuse per second.
    /// </value>
    public double ReusePerSecond { get; init; }

    /// <summary>
    /// Gets the reuse latency ms.
    /// </summary>
    /// <value>
    /// The reuse latency ms.
    /// </value>
    public double ReuseLatencyMs => ReuseTime.TotalMilliseconds / ReuseCount;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is PoolReuseMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(PoolReuseMeasurement other)
    {
        return ReuseTime.Equals(other.ReuseTime) &&
               ReuseCount == other.ReuseCount &&
               ReuseRate.Equals(other.ReuseRate) &&
               ReusePerSecond.Equals(other.ReusePerSecond);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(ReuseTime, ReuseCount, ReuseRate, ReusePerSecond);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(PoolReuseMeasurement left, PoolReuseMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(PoolReuseMeasurement left, PoolReuseMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Pool memory overhead measurement result.
/// </summary>
public readonly struct PoolMemoryOverheadMeasurement : IEquatable<PoolMemoryOverheadMeasurement>
{
    /// <summary>
    /// Gets the retained bytes.
    /// </summary>
    /// <value>
    /// The retained bytes.
    /// </value>
    public long RetainedBytes { get; init; }

    /// <summary>
    /// Gets the allocated bytes.
    /// </summary>
    /// <value>
    /// The allocated bytes.
    /// </value>
    public long AllocatedBytes { get; init; }

    /// <summary>
    /// Gets the overhead ratio.
    /// </summary>
    /// <value>
    /// The overhead ratio.
    /// </value>
    public double OverheadRatio { get; init; }

    /// <summary>
    /// Gets the bucket count.
    /// </summary>
    /// <value>
    /// The bucket count.
    /// </value>
    public int BucketCount { get; init; }

    /// <summary>
    /// Gets the memory efficiency.
    /// </summary>
    /// <value>
    /// The memory efficiency.
    /// </value>
    public double MemoryEfficiency => 1.0 - OverheadRatio;

    /// <summary>
    /// Gets the average retained per bucket.
    /// </summary>
    /// <value>
    /// The average retained per bucket.
    /// </value>
    public double AverageRetainedPerBucket => (double)RetainedBytes / BucketCount;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is PoolMemoryOverheadMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(PoolMemoryOverheadMeasurement other) => RetainedBytes == other.RetainedBytes && AllocatedBytes == other.AllocatedBytes && OverheadRatio.Equals(other.OverheadRatio) && BucketCount == other.BucketCount;

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(RetainedBytes, AllocatedBytes, OverheadRatio, BucketCount);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(PoolMemoryOverheadMeasurement left, PoolMemoryOverheadMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(PoolMemoryOverheadMeasurement left, PoolMemoryOverheadMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Lazy synchronization measurement result.
/// </summary>
public readonly struct LazySyncMeasurement : IEquatable<LazySyncMeasurement>
{
    /// <summary>
    /// Gets the host allocation time.
    /// </summary>
    /// <value>
    /// The host allocation time.
    /// </value>
    public TimeSpan HostAllocationTime { get; init; }

    /// <summary>
    /// Gets the device allocation time.
    /// </summary>
    /// <value>
    /// The device allocation time.
    /// </value>
    public TimeSpan DeviceAllocationTime { get; init; }

    /// <summary>
    /// Gets the lazy synchronize time.
    /// </summary>
    /// <value>
    /// The lazy synchronize time.
    /// </value>
    public TimeSpan LazySyncTime { get; init; }

    /// <summary>
    /// Gets the synchronize efficiency ratio.
    /// </summary>
    /// <value>
    /// The synchronize efficiency ratio.
    /// </value>
    public double SyncEfficiencyRatio { get; init; }

    /// <summary>
    /// Gets the total setup time.
    /// </summary>
    /// <value>
    /// The total setup time.
    /// </value>
    public TimeSpan TotalSetupTime => HostAllocationTime + DeviceAllocationTime;

    /// <summary>
    /// Gets the synchronize overhead ratio.
    /// </summary>
    /// <value>
    /// The synchronize overhead ratio.
    /// </value>
    public double SyncOverheadRatio => LazySyncTime.TotalMilliseconds / TotalSetupTime.TotalMilliseconds;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is LazySyncMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(LazySyncMeasurement other)
    {
        return HostAllocationTime.Equals(other.HostAllocationTime) &&
               DeviceAllocationTime.Equals(other.DeviceAllocationTime) &&
               LazySyncTime.Equals(other.LazySyncTime) &&
               SyncEfficiencyRatio.Equals(other.SyncEfficiencyRatio);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(HostAllocationTime, DeviceAllocationTime, LazySyncTime, SyncEfficiencyRatio);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(LazySyncMeasurement left, LazySyncMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(LazySyncMeasurement left, LazySyncMeasurement right) => !left.Equals(right);
}

/// <summary>
/// State transition measurement result.
/// </summary>
public readonly struct StateTransitionMeasurement : IEquatable<StateTransitionMeasurement>
{

    /// <summary>
    /// Gets the transitions.
    /// </summary>
    /// <value>
    /// The transitions.
    /// </value>
    public IReadOnlyList<(BufferState From, BufferState To, TimeSpan Duration)> Transitions { get; init; }

    /// <summary>
    /// Gets the average transition time.
    /// </summary>
    /// <value>
    /// The average transition time.
    /// </value>
    public TimeSpan AverageTransitionTime { get; init; }

    /// <summary>
    /// Gets the total transitions.
    /// </summary>
    /// <value>
    /// The total transitions.
    /// </value>
    public int TotalTransitions { get; init; }

    /// <summary>
    /// Gets the transition efficiency.
    /// </summary>
    /// <value>
    /// The transition efficiency.
    /// </value>
    public double TransitionEfficiency => 1.0 / AverageTransitionTime.TotalMilliseconds;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is StateTransitionMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(StateTransitionMeasurement other) => AverageTransitionTime.Equals(other.AverageTransitionTime) && TotalTransitions == other.TotalTransitions;

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(AverageTransitionTime, TotalTransitions);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(StateTransitionMeasurement left, StateTransitionMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(StateTransitionMeasurement left, StateTransitionMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Memory coherence measurement result.
/// </summary>
public readonly struct CoherenceMeasurement : IEquatable<CoherenceMeasurement>
{
    /// <summary>
    /// Gets the total coherence time.
    /// </summary>
    /// <value>
    /// The total coherence time.
    /// </value>
    public TimeSpan TotalCoherenceTime { get; init; }

    /// <summary>
    /// Gets the coherence operations.
    /// </summary>
    /// <value>
    /// The coherence operations.
    /// </value>
    public int CoherenceOperations { get; init; }

    /// <summary>
    /// Gets the average coherence time.
    /// </summary>
    /// <value>
    /// The average coherence time.
    /// </value>
    public TimeSpan AverageCoherenceTime { get; init; }

    /// <summary>
    /// Gets the coherence operations per second.
    /// </summary>
    /// <value>
    /// The coherence operations per second.
    /// </value>
    public double CoherenceOperationsPerSecond => CoherenceOperations / TotalCoherenceTime.TotalSeconds;

    /// <summary>
    /// Gets the coherence efficiency.
    /// </summary>
    /// <value>
    /// The coherence efficiency.
    /// </value>
    public double CoherenceEfficiency => 1.0 / AverageCoherenceTime.TotalMilliseconds;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is CoherenceMeasurement other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(CoherenceMeasurement other)
    {
        return TotalCoherenceTime.Equals(other.TotalCoherenceTime) &&
               CoherenceOperations == other.CoherenceOperations &&
               AverageCoherenceTime.Equals(other.AverageCoherenceTime);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(TotalCoherenceTime, CoherenceOperations, AverageCoherenceTime);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(CoherenceMeasurement left, CoherenceMeasurement right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(CoherenceMeasurement left, CoherenceMeasurement right) => !left.Equals(right);
}

/// <summary>
/// Overall performance summary.
/// </summary>
public readonly struct PerformanceSummary : IEquatable<PerformanceSummary>
{
    /// <summary>
    /// Gets the maximum bandwidth g BPS.
    /// </summary>
    /// <value>
    /// The maximum bandwidth g BPS.
    /// </value>
    public double MaxBandwidthGBps { get; init; }

    /// <summary>
    /// Gets the minimum allocation latency ms.
    /// </summary>
    /// <value>
    /// The minimum allocation latency ms.
    /// </value>
    public double MinAllocationLatencyMs { get; init; }

    /// <summary>
    /// Gets the pool efficiency.
    /// </summary>
    /// <value>
    /// The pool efficiency.
    /// </value>
    public double PoolEfficiency { get; init; }

    /// <summary>
    /// Gets the overall score.
    /// </summary>
    /// <value>
    /// The overall score.
    /// </value>
    public double OverallScore { get; init; }

    /// <summary>
    /// Gets the performance grade.
    /// </summary>
    /// <value>
    /// The performance grade.
    /// </value>
    public string PerformanceGrade => OverallScore switch
    {
        >= 90 => "A+ (Excellent)",
        >= 80 => "A (Very Good)",
        >= 70 => "B (Good)",
        >= 60 => "C (Average)",
        >= 50 => "D (Below Average)",
        _ => "F (Poor)"
    };

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///   <see langword="true" /> if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj) => obj is PerformanceSummary other && Equals(other);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
    /// </returns>
    public bool Equals(PerformanceSummary other)
    {
        return MaxBandwidthGBps.Equals(other.MaxBandwidthGBps) &&
               MinAllocationLatencyMs.Equals(other.MinAllocationLatencyMs) &&
               PoolEfficiency.Equals(other.PoolEfficiency) &&
               OverallScore.Equals(other.OverallScore);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public override int GetHashCode() => HashCode.Combine(MaxBandwidthGBps, MinAllocationLatencyMs, PoolEfficiency, OverallScore);

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(PerformanceSummary left, PerformanceSummary right) => left.Equals(right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(PerformanceSummary left, PerformanceSummary right) => !left.Equals(right);
}
