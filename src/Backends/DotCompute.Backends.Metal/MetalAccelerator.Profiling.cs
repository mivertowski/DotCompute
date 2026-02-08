// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using DotCompute.Abstractions.Profiling;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Accelerators;

/// <summary>
/// Metal accelerator profiling implementation.
/// </summary>
public sealed partial class MetalAccelerator
{
    // LoggerMessage delegates for profiling
    private static readonly Action<ILogger, Exception?> _logProfilingSnapshotError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(7100, nameof(GetProfilingSnapshotAsync)),
            "Failed to collect Metal profiling snapshot");

    private static readonly Action<ILogger, Exception?> _logProfilingMetricsError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(7101, nameof(GetProfilingMetricsAsync)),
            "Failed to collect Metal profiling metrics");

    /// <summary>
    /// Gets a comprehensive profiling snapshot of Metal accelerator performance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the profiling snapshot.</returns>
    public override ValueTask<ProfilingSnapshot> GetProfilingSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Get all metrics from the performance profiler
            var allMetrics = _profiler.GetAllMetrics();

            // Build kernel statistics from profiler data
            var kernelStats = BuildKernelStatsFromProfiler(allMetrics);

            // Build memory statistics (Metal has less granular memory tracking than CUDA)
            var memoryStats = BuildMemoryStats();

            // Build individual profiling metrics
            var metrics = BuildProfilingMetrics(allMetrics);

            // Calculate derived metrics
            var deviceUtilization = CalculateDeviceUtilization(allMetrics);
            var totalOperations = kernelStats?.TotalExecutions ?? 0;
            var avgLatency = kernelStats?.AverageExecutionTimeMs ?? 0.0;
            var throughput = CalculateThroughput(allMetrics);

            // Build status message
            var statusMessage = BuildProfilingStatusMessage(kernelStats, totalOperations);

            // Analyze performance trends and bottlenecks
            var trends = IdentifyPerformanceTrends(allMetrics);
            var bottlenecks = IdentifyBottlenecks(kernelStats, deviceUtilization);
            var recommendations = GenerateRecommendations(kernelStats, deviceUtilization, allMetrics);

            var snapshot = new ProfilingSnapshot
            {
                DeviceId = Info.Id,
                DeviceName = Info.Name,
                BackendType = "Metal",
                Timestamp = DateTimeOffset.UtcNow,
                IsAvailable = true,
                Metrics = metrics,
                KernelStats = kernelStats,
                MemoryStats = memoryStats,
                DeviceUtilizationPercent = deviceUtilization,
                TotalOperations = totalOperations,
                AverageLatencyMs = avgLatency,
                ThroughputOpsPerSecond = throughput,
                StatusMessage = statusMessage,
                PerformanceTrends = trends,
                IdentifiedBottlenecks = bottlenecks,
                Recommendations = recommendations
            };

            return ValueTask.FromResult(snapshot);
        }
        catch (Exception ex)
        {
            _logProfilingSnapshotError(Logger, ex);
            return ValueTask.FromResult(ProfilingSnapshot.CreateUnavailable(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "Metal",
                reason: $"Error collecting profiling data: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// Gets current profiling metrics from the Metal device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the collection of profiling metrics.</returns>
    public override ValueTask<IReadOnlyList<ProfilingMetric>> GetProfilingMetricsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var allMetrics = _profiler.GetAllMetrics();
            var metrics = BuildProfilingMetrics(allMetrics);
            return ValueTask.FromResult<IReadOnlyList<ProfilingMetric>>(metrics);
        }
        catch (Exception ex)
        {
            _logProfilingMetricsError(Logger, ex);
            return ValueTask.FromResult<IReadOnlyList<ProfilingMetric>>(Array.Empty<ProfilingMetric>());
        }
    }

    /// <summary>
    /// Builds kernel profiling statistics from Metal performance profiler data.
    /// </summary>
    private static KernelProfilingStats? BuildKernelStatsFromProfiler(Dictionary<string, Utilities.MetalOperationMetrics> allMetrics)
    {
        // Filter for kernel execution operations
        var kernelMetrics = allMetrics.Values
            .Where(m => m.OperationName.Contains("kernel", StringComparison.OrdinalIgnoreCase) ||
                       m.OperationName.Contains("execute", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (kernelMetrics.Count == 0)
        {
            return null;
        }

        var totalExecutions = kernelMetrics.Sum(m => m.ExecutionCount);
        var totalSuccessful = kernelMetrics.Sum(m => m.SuccessfulExecutions);
        var failedExecutions = totalExecutions - totalSuccessful;

        var totalTimeMs = kernelMetrics.Sum(m => m.TotalTime.TotalMilliseconds);
        var avgTimeMs = totalExecutions > 0 ? totalTimeMs / totalExecutions : 0.0;
        var minTimeMs = kernelMetrics.Count > 0 ? kernelMetrics.Min(m => m.MinTime.TotalMilliseconds) : 0.0;
        var maxTimeMs = kernelMetrics.Count > 0 ? kernelMetrics.Max(m => m.MaxTime.TotalMilliseconds) : 0.0;

        // Calculate percentiles and standard deviation from variance data
        var allVariances = kernelMetrics.Select(m => m.TimeVariance).ToList();
        var avgVariance = allVariances.Count > 0 ? allVariances.Average() : 0.0;
        var stdDevMs = Math.Sqrt(avgVariance);

        // Approximate percentiles (Metal doesn't store individual times, but we can estimate)
        var medianMs = avgTimeMs; // Approximate
        var p95Ms = avgTimeMs + (1.645 * stdDevMs); // Approximate using normal distribution
        var p99Ms = avgTimeMs + (2.326 * stdDevMs); // Approximate using normal distribution

        return new KernelProfilingStats
        {
            TotalExecutions = totalExecutions,
            AverageExecutionTimeMs = avgTimeMs,
            MinExecutionTimeMs = minTimeMs,
            MaxExecutionTimeMs = maxTimeMs,
            MedianExecutionTimeMs = medianMs,
            P95ExecutionTimeMs = p95Ms,
            P99ExecutionTimeMs = p99Ms,
            StandardDeviationMs = stdDevMs,
            TotalExecutionTimeMs = totalTimeMs,
            FailedExecutions = failedExecutions
        };
    }

    /// <summary>
    /// Builds memory profiling statistics (simplified for Metal).
    /// </summary>
    private MemoryProfilingStats? BuildMemoryStats()
    {
        try
        {
            // Metal has limited memory profiling compared to CUDA
            // We track allocations through the memory manager
            long totalAllocations = 0;
            long totalBytes = 0;
            long currentMemory = 0;
            long peakMemory = 0;

            if (Memory is Memory.MetalMemoryManager metalMemory)
            {
                // Get memory statistics from Metal memory manager
                var stats = metalMemory.Statistics;
                totalAllocations = stats.AllocationCount;
                totalBytes = stats.TotalAllocated;
                currentMemory = stats.CurrentUsage;
                peakMemory = stats.PeakUsage;
            }

            return new MemoryProfilingStats
            {
                TotalAllocations = totalAllocations,
                TotalBytesAllocated = totalBytes,
                HostToDeviceTransfers = 0, // Metal unified memory doesn't track transfers the same way
                HostToDeviceBytes = 0,
                DeviceToHostTransfers = 0,
                DeviceToHostBytes = 0,
                AverageTransferTimeMs = 0.0,
                BandwidthMBps = 0.0,
                PeakMemoryUsageBytes = peakMemory,
                CurrentMemoryUsageBytes = currentMemory,
                MemoryUtilizationPercent = peakMemory > 0 ? (currentMemory * 100.0 / peakMemory) : 0.0
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds collection of profiling metrics.
    /// </summary>
    private static IReadOnlyList<ProfilingMetric> BuildProfilingMetrics(Dictionary<string, Utilities.MetalOperationMetrics> allMetrics)
    {
        var metrics = new List<ProfilingMetric>(10);

        var kernelMetrics = allMetrics.Values.Where(m =>
            m.OperationName.Contains("kernel", StringComparison.OrdinalIgnoreCase) ||
            m.OperationName.Contains("execute", StringComparison.OrdinalIgnoreCase)).ToList();

        if (kernelMetrics.Count > 0)
        {
            var totalExec = kernelMetrics.Sum(m => m.ExecutionCount);
            var totalTime = kernelMetrics.Sum(m => m.TotalTime.TotalMilliseconds);
            var avgTime = totalExec > 0 ? totalTime / totalExec : 0.0;

            metrics.Add(ProfilingMetric.Create(
                ProfilingMetricType.KernelExecutionTime,
                avgTime,
                "Average Kernel Time",
                "ms"
            ));

            metrics.Add(ProfilingMetric.Create(
                ProfilingMetricType.Custom,
                totalExec,
                "Total Kernel Executions",
                "count"
            ));
        }

        // Add device utilization metric
        var utilization = CalculateDeviceUtilization(allMetrics);
        metrics.Add(ProfilingMetric.Create(
            ProfilingMetricType.DeviceUtilization,
            utilization,
            "Device Utilization",
            "percent",
            0.0,
            100.0
        ));

        // Add throughput metric
        var throughput = CalculateThroughput(allMetrics);
        metrics.Add(ProfilingMetric.Create(
            ProfilingMetricType.Throughput,
            throughput,
            "Operation Throughput",
            "ops/sec"
        ));

        return metrics;
    }

    /// <summary>
    /// Builds profiling status message.
    /// </summary>
    private static string BuildProfilingStatusMessage(KernelProfilingStats? kernelStats, long totalOperations)
    {
        if (kernelStats == null || totalOperations == 0)
        {
            return "No profiling data available - no operations executed yet";
        }

        var successRate = kernelStats.SuccessRate * 100.0;
        return $"Profiling active: {totalOperations:N0} operations, {successRate:F1}% success rate, {kernelStats.AverageExecutionTimeMs:F2}ms avg latency";
    }

    /// <summary>
    /// Calculates device utilization percentage (approximation for Metal).
    /// </summary>
    private static double CalculateDeviceUtilization(Dictionary<string, Utilities.MetalOperationMetrics> allMetrics)
    {
        // Metal doesn't provide direct GPU utilization like CUDA
        // We approximate based on operation activity
        if (allMetrics.Count == 0)
        {
            return 0.0;
        }

        var totalOps = allMetrics.Values.Sum(m => m.ExecutionCount);
        var recentActivityScore = Math.Min(totalOps / 100.0, 1.0) * 100.0; // Scale to 0-100%
        return recentActivityScore;
    }

    /// <summary>
    /// Calculates throughput in operations per second.
    /// </summary>
    private static double CalculateThroughput(Dictionary<string, Utilities.MetalOperationMetrics> allMetrics)
    {
        if (allMetrics.Count == 0)
        {
            return 0.0;
        }

        var totalOps = allMetrics.Values.Sum(m => m.ExecutionCount);
        var totalTime = allMetrics.Values.Sum(m => m.TotalTime.TotalSeconds);

        return totalTime > 0 ? totalOps / totalTime : 0.0;
    }

    /// <summary>
    /// Identifies performance trends from profiling data.
    /// </summary>
    private static IReadOnlyList<string> IdentifyPerformanceTrends(Dictionary<string, Utilities.MetalOperationMetrics> allMetrics)
    {
        var trends = new List<string>();

        if (allMetrics.Count == 0)
        {
            return trends;
        }

        // Find slowest operations
        var slowest = allMetrics.Values.OrderByDescending(m => m.AverageTime.TotalMilliseconds).Take(3);
        foreach (var op in slowest)
        {
            if (op.AverageTime.TotalMilliseconds > 10.0)
            {
                trends.Add($"Operation '{op.OperationName}' averages {op.AverageTime.TotalMilliseconds:F2}ms");
            }
        }

        // Check for high variance operations
        var highVariance = allMetrics.Values.Where(m => Math.Sqrt(m.TimeVariance) > m.AverageTime.TotalMilliseconds * 0.5);
        foreach (var op in highVariance)
        {
            trends.Add($"High variance detected in '{op.OperationName}' (inconsistent performance)");
        }

        return trends;
    }

    /// <summary>
    /// Identifies performance bottlenecks.
    /// </summary>
    private static IReadOnlyList<string> IdentifyBottlenecks(KernelProfilingStats? kernelStats, double utilization)
    {
        var bottlenecks = new List<string>();

        if (kernelStats == null)
        {
            return bottlenecks;
        }

        // Low utilization
        if (utilization < 30.0 && kernelStats.TotalExecutions > 10)
        {
            bottlenecks.Add($"Low device utilization ({utilization:F1}%) - consider batching operations");
        }

        // High variance
        var cv = kernelStats.StandardDeviationMs / kernelStats.AverageExecutionTimeMs * 100.0;
        if (cv > 30.0)
        {
            bottlenecks.Add($"High execution time variance (CV={cv:F1}%) - inconsistent performance");
        }

        // Low success rate
        if (kernelStats.SuccessRate < 0.95)
        {
            bottlenecks.Add($"Low success rate ({kernelStats.SuccessRate:P1}) - frequent operation failures");
        }

        return bottlenecks;
    }

    /// <summary>
    /// Generates performance recommendations.
    /// </summary>
    private static IReadOnlyList<string> GenerateRecommendations(
        KernelProfilingStats? kernelStats,
        double utilization,
        Dictionary<string, Utilities.MetalOperationMetrics> allMetrics)
    {
        var recommendations = new List<string>();

        if (kernelStats == null)
        {
            return recommendations;
        }

        // Utilization recommendations
        if (utilization < 50.0 && kernelStats.TotalExecutions > 10)
        {
            recommendations.Add("Consider batching small operations to improve GPU utilization");
            recommendations.Add("Use command buffer encoding to reduce CPU overhead");
        }

        // Performance consistency recommendations
        var cv = kernelStats.StandardDeviationMs / kernelStats.AverageExecutionTimeMs * 100.0;
        if (cv > 30.0)
        {
            recommendations.Add("High variance suggests thermal throttling or background interference");
            recommendations.Add("Consider using consistent workload sizes and pre-warming kernels");
        }

        // Operation-specific recommendations
        var slowOperations = allMetrics.Values
            .Where(m => m.AverageTime.TotalMilliseconds > 100.0)
            .OrderByDescending(m => m.TotalTime.TotalMilliseconds)
            .Take(2);

        foreach (var op in slowOperations)
        {
            recommendations.Add($"Optimize '{op.OperationName}' - consuming {op.TotalTime.TotalMilliseconds:F0}ms total");
        }

        return recommendations;
    }
}
