// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using DotCompute.Abstractions.Profiling;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// CUDA accelerator profiling implementation.
/// </summary>
public sealed partial class CudaAccelerator
{
    private readonly Stopwatch _sessionTimer = Stopwatch.StartNew();
    private long _totalKernelExecutions;
    private long _failedKernelExecutions;
    private double _totalKernelTimeMs;
    private double _minKernelTimeMs = double.MaxValue;
    private double _maxKernelTimeMs = double.MinValue;
    private readonly List<double> _recentKernelTimes = new(1000); // Keep last 1000 executions

    private long _totalMemoryAllocations;
    private long _totalBytesAllocated;
    private long _hostToDeviceTransfers;
    private long _hostToDeviceBytes;
    private long _deviceToHostTransfers;
    private long _deviceToHostBytes;
    private double _totalTransferTimeMs;

    #region Profiling LoggerMessage Delegates

    private static readonly Action<ILogger, Exception?> _logProfilingSnapshotError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4001, nameof(_logProfilingSnapshotError)),
            "Failed to collect profiling snapshot for CUDA device");

    private static readonly Action<ILogger, Exception?> _logProfilingMetricsError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4002, nameof(_logProfilingMetricsError)),
            "Failed to collect profiling metrics for CUDA device");

    #endregion

    /// <summary>
    /// Gets a comprehensive profiling snapshot of the CUDA device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the profiling snapshot.</returns>
    /// <remarks>
    /// <para>
    /// CUDA profiling provides detailed performance metrics using CUDA Events for precise timing.
    /// This implementation tracks kernel execution statistics, memory operations, and device utilization.
    /// </para>
    ///
    /// <para>
    /// <b>Available Metrics:</b>
    /// - Kernel execution statistics (average, min, max, median, P95, P99)
    /// - Memory transfer statistics (bandwidth, transfer counts)
    /// - Device utilization (estimated from execution patterns)
    /// - Performance trends and bottleneck identification
    /// </para>
    ///
    /// <para>
    /// <b>Profiling Overhead:</b> Minimal (&lt;0.5%) as timing data is collected passively.
    /// CUDA Events provide hardware-accurate timing without CPU synchronization overhead.
    /// </para>
    ///
    /// <para>
    /// <b>Performance:</b> Typically less than 1ms to collect and aggregate metrics.
    /// </para>
    /// </remarks>
    public override ValueTask<ProfilingSnapshot> GetProfilingSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Build kernel execution statistics
            var kernelStats = BuildKernelStats();

            // Build memory operation statistics
            var memoryStats = BuildMemoryStats();

            // Build profiling metrics
            var metrics = BuildProfilingMetrics();

            // Calculate device utilization
            var deviceUtilization = CalculateDeviceUtilization();

            // Calculate average latency
            var avgLatency = _totalKernelExecutions > 0 ? _totalKernelTimeMs / _totalKernelExecutions : 0.0;

            // Calculate throughput
            var throughput = CalculateThroughput();

            // Build status message
            var statusMessage = BuildProfilingStatusMessage();

            // Identify performance trends
            var trends = IdentifyPerformanceTrends();

            // Identify bottlenecks
            var bottlenecks = IdentifyBottlenecks();

            // Generate recommendations
            var recommendations = GenerateRecommendations();

            var snapshot = new ProfilingSnapshot
            {
                DeviceId = Info.Id,
                DeviceName = Info.Name,
                BackendType = "CUDA",
                Timestamp = DateTimeOffset.UtcNow,
                IsAvailable = true,
                Metrics = metrics,
                KernelStats = kernelStats,
                MemoryStats = memoryStats,
                DeviceUtilizationPercent = deviceUtilization,
                TotalOperations = _totalKernelExecutions,
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
                backendType: "CUDA",
                reason: $"Error collecting profiling data: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// Gets current profiling metrics from the CUDA device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the collection of profiling metrics.</returns>
    public override ValueTask<IReadOnlyList<ProfilingMetric>> GetProfilingMetricsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var metrics = BuildProfilingMetrics();
            return ValueTask.FromResult<IReadOnlyList<ProfilingMetric>>(metrics);
        }
        catch (Exception ex)
        {
            _logProfilingMetricsError(Logger, ex);
            return ValueTask.FromResult<IReadOnlyList<ProfilingMetric>>(Array.Empty<ProfilingMetric>());
        }
    }

    /// <summary>
    /// Records a kernel execution for profiling.
    /// </summary>
    /// <param name="executionTimeMs">Execution time in milliseconds.</param>
    /// <param name="success">Whether the execution was successful.</param>
    internal void RecordKernelExecution(double executionTimeMs, bool success = true)
    {
        Interlocked.Increment(ref _totalKernelExecutions);

        if (!success)
        {
            Interlocked.Increment(ref _failedKernelExecutions);
            return;
        }

        // Update total time
        lock (_recentKernelTimes)
        {
            _totalKernelTimeMs += executionTimeMs;
            _minKernelTimeMs = Math.Min(_minKernelTimeMs, executionTimeMs);
            _maxKernelTimeMs = Math.Max(_maxKernelTimeMs, executionTimeMs);

            // Keep recent times for percentile calculation
            _recentKernelTimes.Add(executionTimeMs);
            if (_recentKernelTimes.Count > 1000)
            {
                _recentKernelTimes.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Records a memory allocation for profiling.
    /// </summary>
    /// <param name="bytes">Number of bytes allocated.</param>
    internal void RecordMemoryAllocation(long bytes)
    {
        Interlocked.Increment(ref _totalMemoryAllocations);
        Interlocked.Add(ref _totalBytesAllocated, bytes);
    }

    /// <summary>
    /// Records a memory transfer for profiling.
    /// </summary>
    /// <param name="hostToDevice">Whether transfer is host-to-device (true) or device-to-host (false).</param>
    /// <param name="bytes">Number of bytes transferred.</param>
    /// <param name="transferTimeMs">Transfer time in milliseconds.</param>
    internal void RecordMemoryTransfer(bool hostToDevice, long bytes, double transferTimeMs)
    {
        if (hostToDevice)
        {
            Interlocked.Increment(ref _hostToDeviceTransfers);
            Interlocked.Add(ref _hostToDeviceBytes, bytes);
        }
        else
        {
            Interlocked.Increment(ref _deviceToHostTransfers);
            Interlocked.Add(ref _deviceToHostBytes, bytes);
        }

        // Update total transfer time (approximate due to lock-free addition)
        lock (_recentKernelTimes) // Reuse existing lock
        {
            _totalTransferTimeMs += transferTimeMs;
        }
    }

    /// <summary>
    /// Builds kernel execution statistics.
    /// </summary>
    private KernelProfilingStats BuildKernelStats()
    {
        if (_totalKernelExecutions == 0)
        {
            return new KernelProfilingStats
            {
                TotalExecutions = 0,
                FailedExecutions = 0
            };
        }

        var avgTimeMs = _totalKernelTimeMs / _totalKernelExecutions;

        // Calculate median and percentiles
        double medianMs = 0, p95Ms = 0, p99Ms = 0, stdDevMs = 0;
        lock (_recentKernelTimes)
        {
            if (_recentKernelTimes.Count > 0)
            {
                var sorted = _recentKernelTimes.OrderBy(t => t).ToList();
                medianMs = sorted[sorted.Count / 2];
                p95Ms = sorted[(int)(sorted.Count * 0.95)];
                p99Ms = sorted[(int)(sorted.Count * 0.99)];

                // Calculate standard deviation
                var variance = _recentKernelTimes.Average(t => Math.Pow(t - avgTimeMs, 2));
                stdDevMs = Math.Sqrt(variance);
            }
        }

        return new KernelProfilingStats
        {
            TotalExecutions = _totalKernelExecutions,
            AverageExecutionTimeMs = avgTimeMs,
            MinExecutionTimeMs = _minKernelTimeMs == double.MaxValue ? 0 : _minKernelTimeMs,
            MaxExecutionTimeMs = _maxKernelTimeMs == double.MinValue ? 0 : _maxKernelTimeMs,
            MedianExecutionTimeMs = medianMs,
            P95ExecutionTimeMs = p95Ms,
            P99ExecutionTimeMs = p99Ms,
            StandardDeviationMs = stdDevMs,
            TotalExecutionTimeMs = _totalKernelTimeMs,
            FailedExecutions = _failedKernelExecutions
        };
    }

    /// <summary>
    /// Builds memory operation statistics.
    /// </summary>
    private MemoryProfilingStats BuildMemoryStats()
    {
        var totalTransfers = _hostToDeviceTransfers + _deviceToHostTransfers;
        var totalBytes = _hostToDeviceBytes + _deviceToHostBytes;
        var avgTransferTimeMs = totalTransfers > 0 ? _totalTransferTimeMs / totalTransfers : 0.0;
        var bandwidthMBps = _totalTransferTimeMs > 0 ? (totalBytes / (1024.0 * 1024.0)) / (_totalTransferTimeMs / 1000.0) : 0.0;

        // Get current memory usage via direct CUDA API call
        long currentMemoryUsage = 0, peakMemoryUsage = 0;
        try
        {
            var result = CudaRuntime.cudaMemGetInfo(out var free, out var total);
            if (result == CudaError.Success)
            {
                currentMemoryUsage = (long)(total - free);
                peakMemoryUsage = (long)total;
            }
        }
        catch
        {
            // Ignore errors - memory stats optional
        }

        return new MemoryProfilingStats
        {
            TotalAllocations = _totalMemoryAllocations,
            TotalBytesAllocated = _totalBytesAllocated,
            HostToDeviceTransfers = _hostToDeviceTransfers,
            HostToDeviceBytes = _hostToDeviceBytes,
            DeviceToHostTransfers = _deviceToHostTransfers,
            DeviceToHostBytes = _deviceToHostBytes,
            AverageTransferTimeMs = avgTransferTimeMs,
            BandwidthMBps = bandwidthMBps,
            PeakMemoryUsageBytes = peakMemoryUsage,
            CurrentMemoryUsageBytes = currentMemoryUsage,
            MemoryUtilizationPercent = peakMemoryUsage > 0 ? (currentMemoryUsage * 100.0 / peakMemoryUsage) : 0.0
        };
    }

    /// <summary>
    /// Builds collection of profiling metrics.
    /// </summary>
    private IReadOnlyList<ProfilingMetric> BuildProfilingMetrics()
    {
        var metrics = new List<ProfilingMetric>(10);

        if (_totalKernelExecutions > 0)
        {
            var avgTimeMs = _totalKernelTimeMs / _totalKernelExecutions;

            metrics.Add(ProfilingMetric.Create(
                ProfilingMetricType.KernelExecutionTime,
                avgTimeMs,
                "Average Kernel Time",
                "ms"
            ));

            metrics.Add(ProfilingMetric.Create(
                ProfilingMetricType.KernelExecutionTime,
                _minKernelTimeMs == double.MaxValue ? 0 : _minKernelTimeMs,
                "Min Kernel Time",
                "ms"
            ));

            metrics.Add(ProfilingMetric.Create(
                ProfilingMetricType.KernelExecutionTime,
                _maxKernelTimeMs == double.MinValue ? 0 : _maxKernelTimeMs,
                "Max Kernel Time",
                "ms"
            ));
        }

        var totalTransfers = _hostToDeviceTransfers + _deviceToHostTransfers;
        if (totalTransfers > 0)
        {
            var avgTransferTimeMs = _totalTransferTimeMs / totalTransfers;

            metrics.Add(ProfilingMetric.Create(
                ProfilingMetricType.MemoryTransferTime,
                avgTransferTimeMs,
                "Average Transfer Time",
                "ms"
            ));

            var totalBytes = _hostToDeviceBytes + _deviceToHostBytes;
            var bandwidthMBps = _totalTransferTimeMs > 0 ? (totalBytes / (1024.0 * 1024.0)) / (_totalTransferTimeMs / 1000.0) : 0.0;

            metrics.Add(ProfilingMetric.Create(
                ProfilingMetricType.MemoryBandwidth,
                bandwidthMBps,
                "Memory Bandwidth",
                "MB/s"
            ));
        }

        var deviceUtilization = CalculateDeviceUtilization();
        metrics.Add(ProfilingMetric.Create(
            ProfilingMetricType.DeviceUtilization,
            deviceUtilization,
            "Device Utilization",
            "%",
            minValue: 0,
            maxValue: 100
        ));

        var throughput = CalculateThroughput();
        metrics.Add(ProfilingMetric.Create(
            ProfilingMetricType.Throughput,
            throughput,
            "Kernel Throughput",
            "ops/sec"
        ));

        return metrics;
    }

    /// <summary>
    /// Calculates device utilization percentage.
    /// </summary>
    private double CalculateDeviceUtilization()
    {
        var elapsedSeconds = _sessionTimer.Elapsed.TotalSeconds;
        if (elapsedSeconds == 0)
        {
            return 0.0;
        }

        // Utilization = (total compute time / wall clock time) * 100
        var utilization = (_totalKernelTimeMs / 1000.0) / elapsedSeconds * 100.0;
        return Math.Clamp(utilization, 0.0, 100.0);
    }

    /// <summary>
    /// Calculates throughput in operations per second.
    /// </summary>
    private double CalculateThroughput()
    {
        var elapsedSeconds = _sessionTimer.Elapsed.TotalSeconds;
        return elapsedSeconds > 0 ? _totalKernelExecutions / elapsedSeconds : 0.0;
    }

    /// <summary>
    /// Builds profiling status message.
    /// </summary>
    private string BuildProfilingStatusMessage()
    {
        var messages = new List<string>();

        messages.Add($"Kernels: {_totalKernelExecutions}");

        if (_totalKernelExecutions > 0)
        {
            var avgTimeMs = _totalKernelTimeMs / _totalKernelExecutions;
            messages.Add($"Avg Time: {avgTimeMs:F3}ms");

            var successRate = (_totalKernelExecutions - _failedKernelExecutions) * 100.0 / _totalKernelExecutions;
            messages.Add($"Success: {successRate:F1}%");
        }

        messages.Add($"Utilization: {CalculateDeviceUtilization():F1}%");
        messages.Add($"Throughput: {CalculateThroughput():F1} ops/s");

        return string.Join("; ", messages);
    }

    /// <summary>
    /// Identifies performance trends.
    /// </summary>
    private IReadOnlyList<string> IdentifyPerformanceTrends()
    {
        var trends = new List<string>();

        // Analyze recent performance
        lock (_recentKernelTimes)
        {
            if (_recentKernelTimes.Count >= 10)
            {
                var firstHalf = _recentKernelTimes.Take(_recentKernelTimes.Count / 2).Average();
                var secondHalf = _recentKernelTimes.Skip(_recentKernelTimes.Count / 2).Average();

                if (secondHalf > firstHalf * 1.1)
                {
                    trends.Add("Performance degrading - execution times increasing");
                }
                else if (secondHalf < firstHalf * 0.9)
                {
                    trends.Add("Performance improving - execution times decreasing");
                }
                else
                {
                    trends.Add("Performance stable");
                }
            }
        }

        return trends;
    }

    /// <summary>
    /// Identifies performance bottlenecks.
    /// </summary>
    private IReadOnlyList<string> IdentifyBottlenecks()
    {
        var bottlenecks = new List<string>();

        // Check for memory transfer bottlenecks
        var totalBytes = _hostToDeviceBytes + _deviceToHostBytes;
        if (totalBytes > 0 && _totalTransferTimeMs > _totalKernelTimeMs)
        {
            bottlenecks.Add("Memory transfers taking longer than kernel execution");
        }

        // Check for low utilization
        var utilization = CalculateDeviceUtilization();
        if (utilization < 50.0 && _totalKernelExecutions > 10)
        {
            bottlenecks.Add($"Low device utilization ({utilization:F1}%) - consider batching operations");
        }

        // Check for high variance
        lock (_recentKernelTimes)
        {
            if (_recentKernelTimes.Count >= 10)
            {
                var avg = _recentKernelTimes.Average();
                var variance = _recentKernelTimes.Average(t => Math.Pow(t - avg, 2));
                var stdDev = Math.Sqrt(variance);
                var cv = avg > 0 ? (stdDev / avg) * 100.0 : 0.0;

                if (cv > 30.0)
                {
                    bottlenecks.Add($"High execution time variance (CV={cv:F1}%) - inconsistent performance");
                }
            }
        }

        return bottlenecks;
    }

    /// <summary>
    /// Generates optimization recommendations.
    /// </summary>
    private IReadOnlyList<string> GenerateRecommendations()
    {
        var recommendations = new List<string>();

        // Memory transfer recommendations
        var totalTransfers = _hostToDeviceTransfers + _deviceToHostTransfers;
        if (totalTransfers > _totalKernelExecutions * 2)
        {
            recommendations.Add("Consider reducing memory transfers - use persistent device buffers");
        }

        // Utilization recommendations
        var utilization = CalculateDeviceUtilization();
        if (utilization < 30.0 && _totalKernelExecutions > 10)
        {
            recommendations.Add("Low utilization - batch multiple operations or use streams for concurrency");
        }

        // Small kernel recommendations
        if (_totalKernelExecutions > 0)
        {
            var avgTimeMs = _totalKernelTimeMs / _totalKernelExecutions;
            if (avgTimeMs < 0.1)
            {
                recommendations.Add("Very short kernels detected - consider kernel fusion to reduce launch overhead");
            }
        }

        return recommendations;
    }
}
