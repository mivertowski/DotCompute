// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Comprehensive metrics for memory access operations including performance counters,
/// bandwidth utilization, and access patterns
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MemoryAccessMetrics(
    long bytesTransferred,
    TimeSpan duration,
    int accessCount = 1,
    MemoryAccessPattern accessPattern = MemoryAccessPattern.Sequential,
    double cacheHitRate = 0.0,
    int cacheMisses = 0,
    long latencyNanoseconds = 0,
    MemoryType sourceMemoryType = MemoryType.Host,
    MemoryType destinationMemoryType = MemoryType.Device,
    int alignmentBytes = 1,
    bool isCoalesced = false,
    long peakMemoryUsage = 0,
    int fragmentationEvents = 0)
{
    /// <summary>
    /// Total bytes transferred
    /// </summary>
    public readonly long BytesTransferred = bytesTransferred;

    /// <summary>
    /// Duration of the memory operation
    /// </summary>
    public readonly TimeSpan Duration = duration;

    /// <summary>
    /// Calculated bandwidth in bytes per second
    /// </summary>
    public readonly double BandwidthBytesPerSecond = duration.TotalSeconds > 0

            ? bytesTransferred / duration.TotalSeconds

            : 0.0;

    /// <summary>
    /// Number of memory access operations
    /// </summary>
    public readonly int AccessCount = accessCount;

    /// <summary>
    /// Memory access pattern type
    /// </summary>
    public readonly MemoryAccessPattern AccessPattern = accessPattern;

    /// <summary>
    /// Cache hit rate (0.0 to 1.0)
    /// </summary>
    public readonly double CacheHitRate = Math.Clamp(cacheHitRate, 0.0, 1.0);

    /// <summary>
    /// Number of cache misses
    /// </summary>
    public readonly int CacheMisses = cacheMisses;

    /// <summary>
    /// Memory latency in nanoseconds
    /// </summary>
    public readonly long LatencyNanoseconds = latencyNanoseconds;

    /// <summary>
    /// Memory efficiency score (0.0 to 1.0)
    /// </summary>
    public readonly double EfficiencyScore = CalculateEfficiencyScore(
            cacheHitRate, isCoalesced, accessPattern, alignmentBytes);

    /// <summary>
    /// Source memory type
    /// </summary>
    public readonly MemoryType SourceMemoryType = sourceMemoryType;

    /// <summary>
    /// Destination memory type
    /// </summary>
    public readonly MemoryType DestinationMemoryType = destinationMemoryType;

    /// <summary>
    /// Memory alignment in bytes
    /// </summary>
    public readonly int AlignmentBytes = alignmentBytes;

    /// <summary>
    /// Whether the operation was coalesced
    /// </summary>
    public readonly bool IsCoalesced = isCoalesced;

    /// <summary>
    /// Peak memory usage during operation
    /// </summary>
    public readonly long PeakMemoryUsage = peakMemoryUsage;

    /// <summary>
    /// Number of memory fragmentation events
    /// </summary>
    public readonly int FragmentationEvents = fragmentationEvents;

    /// <summary>
    /// Bandwidth in megabytes per second
    /// </summary>
    public double BandwidthMBPerSecond => BandwidthBytesPerSecond / (1024.0 * 1024.0);

    /// <summary>
    /// Bandwidth in gigabytes per second
    /// </summary>
    public double BandwidthGBPerSecond => BandwidthBytesPerSecond / (1024.0 * 1024.0 * 1024.0);

    /// <summary>
    /// Average bytes per access
    /// </summary>
    public double AverageBytesPerAccess => AccessCount > 0 ? (double)BytesTransferred / AccessCount : 0.0;

    /// <summary>
    /// Latency in microseconds
    /// </summary>
    public double LatencyMicroseconds => LatencyNanoseconds / 1000.0;

    /// <summary>
    /// Whether this is a high-performance access (>80% efficiency)
    /// </summary>
    public bool IsHighPerformance => EfficiencyScore > 0.8;

    /// <summary>
    /// Whether this is a cross-device memory transfer
    /// </summary>
    public bool IsCrossDevice => SourceMemoryType != DestinationMemoryType;

    /// <summary>
    /// Creates empty metrics
    /// </summary>
    public static MemoryAccessMetrics Empty => new(0, TimeSpan.Zero);

    /// <summary>
    /// Creates metrics from a simple transfer
    /// </summary>
    public static MemoryAccessMetrics FromTransfer(long bytes, TimeSpan duration) => new(bytes, duration);

    /// <summary>
    /// Creates metrics from a measured operation
    /// </summary>
    public static MemoryAccessMetrics FromMeasurement(
        long bytes,

        Stopwatch stopwatch,

        MemoryAccessPattern pattern = MemoryAccessPattern.Sequential) => new(bytes, stopwatch.Elapsed, accessPattern: pattern);

    /// <summary>
    /// Combines multiple metrics into aggregate metrics
    /// </summary>
    public static MemoryAccessMetrics Combine(params MemoryAccessMetrics[] metrics)
    {
        if (metrics.Length == 0)
        {
            return Empty;
        }


        if (metrics.Length == 1)
        {
            return metrics[0];
        }


        long totalBytes = 0;
        var totalDuration = TimeSpan.Zero;
        var totalAccess = 0;
        var totalCacheMisses = 0;
        long totalLatency = 0;
        double weightedCacheHitRate = 0;
        long totalPeakMemory = 0;
        var totalFragmentation = 0;

        foreach (var metric in metrics)
        {
            totalBytes += metric.BytesTransferred;
            totalDuration = totalDuration.Add(metric.Duration);
            totalAccess += metric.AccessCount;
            totalCacheMisses += metric.CacheMisses;
            totalLatency += metric.LatencyNanoseconds;
            weightedCacheHitRate += metric.CacheHitRate * metric.BytesTransferred;
            totalPeakMemory = Math.Max(totalPeakMemory, metric.PeakMemoryUsage);
            totalFragmentation += metric.FragmentationEvents;
        }

        weightedCacheHitRate = totalBytes > 0 ? weightedCacheHitRate / totalBytes : 0;

        return new MemoryAccessMetrics(
            totalBytes,
            totalDuration,
            totalAccess,
            MemoryAccessPattern.Mixed,
            weightedCacheHitRate,
            totalCacheMisses,
            totalLatency / metrics.Length, // Average latency
            metrics[0].SourceMemoryType,
            metrics[0].DestinationMemoryType,
            metrics[0].AlignmentBytes,
            metrics.All(m => m.IsCoalesced),
            totalPeakMemory,
            totalFragmentation);
    }

    private static double CalculateEfficiencyScore(
        double cacheHitRate,

        bool isCoalesced,

        MemoryAccessPattern pattern,
        int alignment)
    {
        var score = 0.0;

        // Cache hit rate contributes 40%
        score += cacheHitRate * 0.4;

        // Coalesced access contributes 30%
        score += isCoalesced ? 0.3 : 0.0;

        // Access pattern contributes 20%
        score += pattern switch
        {
            MemoryAccessPattern.Sequential => 0.2,
            MemoryAccessPattern.Strided => 0.15,
            MemoryAccessPattern.Random => 0.05,
            MemoryAccessPattern.Mixed => 0.1,
            _ => 0.0
        };

        // Alignment contributes 10%
        if (alignment >= 32)
        {
            score += 0.1;
        }

        else if (alignment >= 16)
        {
            score += 0.07;
        }

        else if (alignment >= 8)
        {
            score += 0.05;
        }

        else if (alignment >= 4)
        {
            score += 0.03;
        }


        return Math.Clamp(score, 0.0, 1.0);
    }

    public override string ToString()
        => $"Bytes={BytesTransferred:N0}, Duration={Duration.TotalMilliseconds:F2}ms, " +
        $"Bandwidth={BandwidthMBPerSecond:F1}MB/s, Efficiency={EfficiencyScore:P1}";

    public string ToDetailedString()
        => $"Transfer: {BytesTransferred:N0} bytes in {Duration.TotalMilliseconds:F2}ms\n" +
        $"Bandwidth: {BandwidthMBPerSecond:F1} MB/s ({BandwidthGBPerSecond:F2} GB/s)\n" +
        $"Access: {AccessCount} operations, {AverageBytesPerAccess:F0} bytes/op\n" +
        $"Pattern: {AccessPattern}, Coalesced: {IsCoalesced}\n" +
        $"Cache: {CacheHitRate:P1} hit rate, {CacheMisses} misses\n" +
        $"Latency: {LatencyMicroseconds:F2}μs\n" +
        $"Memory: {SourceMemoryType} → {DestinationMemoryType}\n" +
        $"Efficiency: {EfficiencyScore:P1} ({(IsHighPerformance ? "High" : "Standard")})";
}

