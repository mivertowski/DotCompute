// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;

namespace DotCompute.Abstractions.RingKernels.Profiling;

/// <summary>
/// High-performance latency histogram for tracking percentile distributions.
/// Uses a fixed-bucket logarithmic histogram for O(1) recording and O(n) percentile calculation.
/// </summary>
/// <remarks>
/// <para><b>Algorithm</b>:</para>
/// <para>
/// Uses HDR (High Dynamic Range) histogram approach with log-linear bucketing:
/// - Sub-microsecond: 100ns buckets (0-1μs)
/// - Microsecond range: 1μs buckets (1-100μs)
/// - Sub-millisecond: 100μs buckets (100μs-10ms)
/// - Millisecond range: 1ms buckets (10ms-1s)
/// - Second range: 100ms buckets (1s-10s)
/// </para>
///
/// <para><b>Performance</b>:</para>
/// <list type="bullet">
/// <item>Record latency: O(1) with ~10ns overhead</item>
/// <item>Get percentile: O(n) where n = number of buckets (500)</item>
/// <item>Memory footprint: ~4KB per histogram (500 × 8 bytes)</item>
/// </list>
///
/// <para><b>Thread Safety</b>:</para>
/// <para>
/// Recording uses Interlocked operations for thread-safe updates.
/// Percentile calculation is lock-free but may return stale data during concurrent updates.
/// </para>
/// </remarks>
public sealed class LatencyHistogram
{
    // Bucket configuration for HDR histogram
    // Range: 100ns to 10s with logarithmic distribution
    private const int SubMicroBuckets = 10;      // 0-1μs in 100ns steps
    private const int MicroBuckets = 99;         // 1-100μs in 1μs steps
    private const int SubMilliBuckets = 99;      // 100μs-10ms in 100μs steps
    private const int MilliBuckets = 99;         // 10ms-1s in 10ms steps
    private const int SecondBuckets = 90;        // 1s-10s in 100ms steps
    private const int OverflowBucket = 1;        // >10s

    private const int TotalBuckets = SubMicroBuckets + MicroBuckets + SubMilliBuckets + MilliBuckets + SecondBuckets + OverflowBucket;

    // Bucket boundaries in nanoseconds
    private const long NanosPerMicro = 1_000;
    private const long NanosPerMilli = 1_000_000;
    private const long NanosPerSecond = 1_000_000_000;

    private readonly long[] _buckets = new long[TotalBuckets];
    private long _totalCount;
    private long _totalSum;
    private long _minValue = long.MaxValue;
    private long _maxValue;

    /// <summary>
    /// Gets the total number of latency samples recorded.
    /// </summary>
    public long TotalCount => Volatile.Read(ref _totalCount);

    /// <summary>
    /// Gets the minimum latency recorded in nanoseconds.
    /// </summary>
    public long MinNanos => _minValue == long.MaxValue ? 0 : Volatile.Read(ref _minValue);

    /// <summary>
    /// Gets the maximum latency recorded in nanoseconds.
    /// </summary>
    public long MaxNanos => Volatile.Read(ref _maxValue);

    /// <summary>
    /// Gets the mean (average) latency in nanoseconds.
    /// </summary>
    public double MeanNanos
    {
        get
        {
            var count = TotalCount;
            return count > 0 ? (double)Volatile.Read(ref _totalSum) / count : 0;
        }
    }

    /// <summary>
    /// Records a latency sample in nanoseconds.
    /// Thread-safe via Interlocked operations.
    /// </summary>
    /// <param name="latencyNanos">Latency value in nanoseconds.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Record(long latencyNanos)
    {
        if (latencyNanos < 0)
        {
            return; // Ignore invalid values
        }

        var bucketIndex = GetBucketIndex(latencyNanos);
        Interlocked.Increment(ref _buckets[bucketIndex]);
        Interlocked.Increment(ref _totalCount);
        Interlocked.Add(ref _totalSum, latencyNanos);

        // Update min/max using compare-exchange loop
        UpdateMin(latencyNanos);
        UpdateMax(latencyNanos);
    }

    /// <summary>
    /// Records multiple latency samples from a batch.
    /// </summary>
    /// <param name="latencies">Collection of latency values in nanoseconds.</param>
    public void RecordBatch(ReadOnlySpan<long> latencies)
    {
        foreach (var latency in latencies)
        {
            Record(latency);
        }
    }

    /// <summary>
    /// Gets the latency value at a given percentile.
    /// </summary>
    /// <param name="percentile">Percentile (0-100), e.g., 50 for median, 99 for P99.</param>
    /// <returns>Latency in nanoseconds at the given percentile, or 0 if no data.</returns>
    public long GetPercentile(double percentile)
    {
        if (percentile is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 100");
        }

        var total = TotalCount;
        if (total == 0)
        {
            return 0;
        }

        var targetCount = (long)(total * percentile / 100.0);
        if (targetCount == 0)
        {
            targetCount = 1;
        }

        long cumulativeCount = 0;
        for (var i = 0; i < TotalBuckets; i++)
        {
            cumulativeCount += Volatile.Read(ref _buckets[i]);
            if (cumulativeCount >= targetCount)
            {
                return GetBucketMidpoint(i);
            }
        }

        return GetBucketMidpoint(TotalBuckets - 1);
    }

    /// <summary>
    /// Gets the P50 (median) latency in nanoseconds.
    /// </summary>
    public long P50Nanos => GetPercentile(50);

    /// <summary>
    /// Gets the P90 latency in nanoseconds.
    /// </summary>
    public long P90Nanos => GetPercentile(90);

    /// <summary>
    /// Gets the P95 latency in nanoseconds.
    /// </summary>
    public long P95Nanos => GetPercentile(95);

    /// <summary>
    /// Gets the P99 latency in nanoseconds.
    /// </summary>
    public long P99Nanos => GetPercentile(99);

    /// <summary>
    /// Gets the P99.9 latency in nanoseconds.
    /// </summary>
    public long P999Nanos => GetPercentile(99.9);

    /// <summary>
    /// Gets a snapshot of all percentile statistics.
    /// </summary>
    public LatencyPercentiles GetPercentiles() => new()
    {
        TotalCount = TotalCount,
        MinNanos = MinNanos,
        MaxNanos = MaxNanos,
        MeanNanos = MeanNanos,
        P50Nanos = P50Nanos,
        P90Nanos = P90Nanos,
        P95Nanos = P95Nanos,
        P99Nanos = P99Nanos,
        P999Nanos = P999Nanos
    };

    /// <summary>
    /// Resets all histogram data to initial state.
    /// Not thread-safe - should only be called when no concurrent updates are occurring.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_buckets);
        _totalCount = 0;
        _totalSum = 0;
        _minValue = long.MaxValue;
        _maxValue = 0;
    }

    /// <summary>
    /// Merges another histogram's data into this one.
    /// Useful for aggregating per-kernel histograms.
    /// </summary>
    /// <param name="other">Histogram to merge from.</param>
    public void Merge(LatencyHistogram other)
    {
        ArgumentNullException.ThrowIfNull(other);

        for (var i = 0; i < TotalBuckets; i++)
        {
            Interlocked.Add(ref _buckets[i], Volatile.Read(ref other._buckets[i]));
        }

        Interlocked.Add(ref _totalCount, other.TotalCount);
        Interlocked.Add(ref _totalSum, Volatile.Read(ref other._totalSum));
        UpdateMin(other.MinNanos);
        UpdateMax(other.MaxNanos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBucketIndex(long nanos)
    {
        // Sub-microsecond: 0-1μs in 100ns buckets (indices 0-9)
        if (nanos < NanosPerMicro)
        {
            return (int)(nanos / 100);
        }

        // Microsecond: 1-100μs in 1μs buckets (indices 10-108)
        if (nanos < 100 * NanosPerMicro)
        {
            return SubMicroBuckets + (int)((nanos - NanosPerMicro) / NanosPerMicro);
        }

        // Sub-millisecond: 100μs-10ms in 100μs buckets (indices 109-207)
        if (nanos < 10 * NanosPerMilli)
        {
            return SubMicroBuckets + MicroBuckets + (int)((nanos - 100 * NanosPerMicro) / (100 * NanosPerMicro));
        }

        // Millisecond: 10ms-1s in 10ms buckets (indices 208-306)
        if (nanos < NanosPerSecond)
        {
            return SubMicroBuckets + MicroBuckets + SubMilliBuckets + (int)((nanos - 10 * NanosPerMilli) / (10 * NanosPerMilli));
        }

        // Second: 1s-10s in 100ms buckets (indices 307-396)
        if (nanos < 10 * NanosPerSecond)
        {
            return SubMicroBuckets + MicroBuckets + SubMilliBuckets + MilliBuckets + (int)((nanos - NanosPerSecond) / (100 * NanosPerMilli));
        }

        // Overflow: >10s (index 397)
        return TotalBuckets - 1;
    }

    private static long GetBucketMidpoint(int index)
    {
        // Sub-microsecond range
        if (index < SubMicroBuckets)
        {
            return index * 100 + 50;
        }

        // Microsecond range
        if (index < SubMicroBuckets + MicroBuckets)
        {
            var microIndex = index - SubMicroBuckets;
            return NanosPerMicro + microIndex * NanosPerMicro + NanosPerMicro / 2;
        }

        // Sub-millisecond range
        if (index < SubMicroBuckets + MicroBuckets + SubMilliBuckets)
        {
            var subMilliIndex = index - SubMicroBuckets - MicroBuckets;
            return 100 * NanosPerMicro + subMilliIndex * 100 * NanosPerMicro + 50 * NanosPerMicro;
        }

        // Millisecond range
        if (index < SubMicroBuckets + MicroBuckets + SubMilliBuckets + MilliBuckets)
        {
            var milliIndex = index - SubMicroBuckets - MicroBuckets - SubMilliBuckets;
            return 10 * NanosPerMilli + milliIndex * 10 * NanosPerMilli + 5 * NanosPerMilli;
        }

        // Second range
        if (index < TotalBuckets - 1)
        {
            var secondIndex = index - SubMicroBuckets - MicroBuckets - SubMilliBuckets - MilliBuckets;
            return NanosPerSecond + secondIndex * 100 * NanosPerMilli + 50 * NanosPerMilli;
        }

        // Overflow - return 10 seconds
        return 10 * NanosPerSecond;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMin(long value)
    {
        var current = Volatile.Read(ref _minValue);
        while (value < current)
        {
            var original = Interlocked.CompareExchange(ref _minValue, value, current);
            if (original == current)
            {
                break;
            }

            current = original;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMax(long value)
    {
        var current = Volatile.Read(ref _maxValue);
        while (value > current)
        {
            var original = Interlocked.CompareExchange(ref _maxValue, value, current);
            if (original == current)
            {
                break;
            }

            current = original;
        }
    }
}

/// <summary>
/// Immutable snapshot of latency percentile statistics.
/// </summary>
public readonly record struct LatencyPercentiles
{
    /// <summary>Total number of samples recorded.</summary>
    public required long TotalCount { get; init; }

    /// <summary>Minimum latency in nanoseconds.</summary>
    public required long MinNanos { get; init; }

    /// <summary>Maximum latency in nanoseconds.</summary>
    public required long MaxNanos { get; init; }

    /// <summary>Mean (average) latency in nanoseconds.</summary>
    public required double MeanNanos { get; init; }

    /// <summary>50th percentile (median) latency in nanoseconds.</summary>
    public required long P50Nanos { get; init; }

    /// <summary>90th percentile latency in nanoseconds.</summary>
    public required long P90Nanos { get; init; }

    /// <summary>95th percentile latency in nanoseconds.</summary>
    public required long P95Nanos { get; init; }

    /// <summary>99th percentile latency in nanoseconds.</summary>
    public required long P99Nanos { get; init; }

    /// <summary>99.9th percentile latency in nanoseconds.</summary>
    public required long P999Nanos { get; init; }

    /// <summary>
    /// Gets the minimum latency in microseconds.
    /// </summary>
    public double MinMicros => MinNanos / 1000.0;

    /// <summary>
    /// Gets the maximum latency in microseconds.
    /// </summary>
    public double MaxMicros => MaxNanos / 1000.0;

    /// <summary>
    /// Gets the mean latency in microseconds.
    /// </summary>
    public double MeanMicros => MeanNanos / 1000.0;

    /// <summary>
    /// Gets the P50 latency in microseconds.
    /// </summary>
    public double P50Micros => P50Nanos / 1000.0;

    /// <summary>
    /// Gets the P99 latency in microseconds.
    /// </summary>
    public double P99Micros => P99Nanos / 1000.0;

    /// <summary>
    /// Gets the P999 latency in microseconds.
    /// </summary>
    public double P999Micros => P999Nanos / 1000.0;
}
