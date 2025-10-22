// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Execution;
using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// System-level performance counter integration for Metal backend
/// </summary>
public sealed class MetalPerformanceCounters : IDisposable
{
    private readonly ILogger<MetalPerformanceCounters> _logger;
    private readonly MetalPerformanceCountersOptions _options;
    private readonly ConcurrentDictionary<string, PerformanceCounter> _counters;
    private readonly ConcurrentDictionary<string, CounterStatistics> _statistics;
    private readonly Timer? _samplingTimer;
    private readonly object _lockObject = new();
    private volatile bool _disposed;

    public MetalPerformanceCounters(
        ILogger<MetalPerformanceCounters> logger,
        MetalPerformanceCountersOptions options)
    {
        _logger = logger;
        _options = options;
        _counters = new ConcurrentDictionary<string, PerformanceCounter>();
        _statistics = new ConcurrentDictionary<string, CounterStatistics>();

        InitializeCounters();

        if (_options.EnableContinuousSampling && _options.SamplingInterval > TimeSpan.Zero)
        {
            _samplingTimer = new Timer(SampleCounters, null, _options.SamplingInterval, _options.SamplingInterval);
        }

        _logger.LogInformation("Metal performance counters initialized with {CounterCount} counters, sampling: {Sampling}",
            _counters.Count, _options.EnableContinuousSampling);
    }

    /// <summary>
    /// Records memory allocation performance
    /// </summary>
    public void RecordMemoryAllocation(long sizeBytes, TimeSpan duration, bool success)
    {
        if (_disposed)
        {
            return;
        }


        UpdateCounter("memory_allocations_total", 1);
        UpdateCounter("memory_allocated_bytes_total", sizeBytes);
        UpdateCounter("memory_allocation_duration_ms", duration.TotalMilliseconds);

        if (!success)
        {
            UpdateCounter("memory_allocation_failures_total", 1);
        }

        // Track allocation size distribution
        var sizeCategory = GetAllocationSizeCategory(sizeBytes);
        UpdateCounter($"memory_allocations_by_size_{sizeCategory}", 1);

        // Track allocation performance
        if (duration.TotalMilliseconds > _options.SlowAllocationThresholdMs)
        {
            UpdateCounter("memory_allocations_slow_total", 1);
            _logger.LogWarning("Slow memory allocation detected: {Size} bytes in {Duration}ms", sizeBytes, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Records kernel execution performance
    /// </summary>
    public void RecordKernelExecution(string kernelName, TimeSpan duration, long dataSize, bool success)
    {
        if (_disposed)
        {
            return;
        }


        UpdateCounter("kernel_executions_total", 1);
        UpdateCounter("kernel_execution_duration_ms", duration.TotalMilliseconds);
        UpdateCounter("kernel_data_processed_bytes_total", dataSize);

        // Kernel-specific metrics
        UpdateCounter($"kernel_{SafeName(kernelName)}_executions_total", 1);
        UpdateCounter($"kernel_{SafeName(kernelName)}_duration_ms", duration.TotalMilliseconds);

        if (!success)
        {
            UpdateCounter("kernel_execution_failures_total", 1);
            UpdateCounter($"kernel_{SafeName(kernelName)}_failures_total", 1);
        }

        // Performance analysis
        var throughputMBps = dataSize > 0 && duration.TotalSeconds > 0

            ? (dataSize / (1024.0 * 1024.0)) / duration.TotalSeconds

            : 0;


        if (throughputMBps > 0)
        {
            UpdateCounter($"kernel_{SafeName(kernelName)}_throughput_mbps", throughputMBps);
        }

        // Track slow kernels
        if (duration.TotalMilliseconds > _options.SlowKernelThresholdMs)
        {
            UpdateCounter("kernel_executions_slow_total", 1);
            _logger.LogWarning("Slow kernel execution detected: {Kernel} took {Duration}ms", kernelName, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Records device utilization metrics
    /// </summary>
    public void RecordDeviceUtilization(double gpuUtilization, double memoryUtilization)
    {
        if (_disposed)
        {
            return;
        }


        UpdateCounter("gpu_utilization_percent", gpuUtilization);
        UpdateCounter("memory_utilization_percent", memoryUtilization);

        // Track utilization bands
        var gpuBand = GetUtilizationBand(gpuUtilization);
        var memoryBand = GetUtilizationBand(memoryUtilization);

        UpdateCounter($"gpu_utilization_band_{gpuBand}", 1);
        UpdateCounter($"memory_utilization_band_{memoryBand}", 1);

        // Monitor thermal and power (if available on platform)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                RecordThermalMetrics();
                RecordPowerMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Could not retrieve thermal/power metrics");
            }
        }
    }

    /// <summary>
    /// Records error events
    /// </summary>
    public void RecordError(MetalError error, string context)
    {
        if (_disposed)
        {
            return;
        }


        UpdateCounter("errors_total", 1);
        UpdateCounter($"error_{error}_total", 1);
        UpdateCounter($"error_context_{SafeName(context)}_total", 1);

        // Track error patterns
        var errorCategory = GetErrorCategory(error);
        UpdateCounter($"errors_by_category_{errorCategory}", 1);
    }

    /// <summary>
    /// Records memory pressure events
    /// </summary>
    public void RecordMemoryPressure(MemoryPressureLevel level, double percentage)
    {
        if (_disposed)
        {
            return;
        }


        UpdateCounter("memory_pressure_events_total", 1);
        UpdateCounter($"memory_pressure_{level.ToString().ToUpperInvariant()}_total", 1);
        UpdateCounter("memory_pressure_percentage", percentage);

        if (level >= MemoryPressureLevel.High)
        {
            _logger.LogWarning("High memory pressure detected: {Level} at {Percentage:F1}%", level, percentage);
        }
    }

    /// <summary>
    /// Records resource usage metrics
    /// </summary>
    public void RecordResourceUsage(ResourceType type, long currentUsage, long peakUsage, long limit)
    {
        if (_disposed)
        {
            return;
        }


        var typeStr = type.ToString().ToUpperInvariant();


        UpdateCounter($"resource_{typeStr}_current_usage", currentUsage);
        UpdateCounter($"resource_{typeStr}_peak_usage", peakUsage);
        UpdateCounter($"resource_{typeStr}_limit", limit);

        if (limit > 0)
        {
            var utilizationPercentage = (double)currentUsage / limit * 100.0;
            UpdateCounter($"resource_{typeStr}_utilization_percent", utilizationPercentage);
        }

        // Track resource exhaustion
        if (limit > 0 && currentUsage >= limit * 0.95) // 95% utilization
        {
            UpdateCounter($"resource_{typeStr}_near_exhaustion_total", 1);
            _logger.LogWarning("Resource near exhaustion: {Type} at {Usage}/{Limit} ({Percent:F1}%)",
                type, currentUsage, limit, (double)currentUsage / limit * 100.0);
        }
    }

    /// <summary>
    /// Gets current counter values
    /// </summary>
    public Dictionary<string, object> GetCurrentCounters()
    {
        if (_disposed)
        {
            return [];
        }


        lock (_lockObject)
        {
            var result = new Dictionary<string, object>();

            foreach (var kvp in _statistics)
            {
                var stats = kvp.Value;
                result[kvp.Key] = new
                {
                    Current = stats.CurrentValue,
                    Total = stats.TotalValue,
                    Count = stats.SampleCount,
                    Average = stats.Average,
                    Min = stats.MinValue,
                    Max = stats.MaxValue,
                    LastUpdated = stats.LastUpdated
                };
            }

            return result;
        }
    }

    /// <summary>
    /// Performs performance analysis
    /// </summary>
    public MetalPerformanceAnalysis AnalyzePerformance()
    {
        if (_disposed)
        {
            return new MetalPerformanceAnalysis();
        }


        var counters = GetCurrentCounters();
        var analysis = new MetalPerformanceAnalysis
        {
            Timestamp = DateTimeOffset.UtcNow,
            AnalysisVersion = "1.0"
        };

        try
        {
            // Analyze throughput
            analysis.ThroughputAnalysis = AnalyzeThroughput(counters);

            // Analyze error rates

            analysis.ErrorRateAnalysis = AnalyzeErrorRates(counters);

            // Analyze resource utilization
            analysis.ResourceUtilizationAnalysis = AnalyzeResourceUtilization(counters);

            // Analyze performance trends
            var performanceTrend = AnalyzePerformanceTrends(counters);
            analysis.PerformanceTrends.Add(performanceTrend);

            // Generate performance score
            analysis.OverallPerformanceScore = CalculatePerformanceScore(analysis);


            _logger.LogDebug("Performance analysis completed with score: {Score:F2}/100", analysis.OverallPerformanceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during performance analysis");
            analysis.Errors.Add($"Analysis error: {ex.Message}");
        }

        return analysis;
    }

    /// <summary>
    /// Performs cleanup of old counter data
    /// </summary>
    public void PerformCleanup(DateTimeOffset cutoffTime)
    {
        if (_disposed)
        {
            return;
        }


        lock (_lockObject)
        {
            var expiredCounters = _statistics
                .Where(kvp => kvp.Value.LastUpdated < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredCounters)
            {
                _ = _statistics.TryRemove(key, out _);
                _ = _counters.TryRemove(key, out var counter);
                counter?.Dispose();
            }

            if (expiredCounters.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired performance counters", expiredCounters.Count);
            }
        }
    }

    private void InitializeCounters()
    {
        var counterNames = new[]
        {
            // Memory counters
            "memory_allocations_total",
            "memory_allocated_bytes_total",

            "memory_allocation_failures_total",
            "memory_allocations_slow_total",

            // Kernel execution counters
            "kernel_executions_total",
            "kernel_execution_failures_total",

            "kernel_executions_slow_total",

            // Device utilization counters
            "gpu_utilization_percent",
            "memory_utilization_percent",

            // Error counters
            "errors_total",

            // Resource counters
            "memory_pressure_events_total"
        };

        foreach (var counterName in counterNames)
        {
            _statistics[counterName] = new CounterStatistics(counterName);
        }
    }

    private void UpdateCounter(string counterName, double value)
    {
        if (_disposed)
        {
            return;
        }


        _ = _statistics.AddOrUpdate(counterName,
            new CounterStatistics(counterName, value),
            (_, existing) =>
            {
                existing.UpdateValue(value);
                return existing;
            });
    }

    private void SampleCounters(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Sample system-level performance counters
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SampleMacOSCounters();
            }

            // Sample Metal-specific counters
            SampleMetalCounters();


            _logger.LogTrace("Performance counter sampling completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during performance counter sampling");
        }
    }

    private void SampleMacOSCounters()
    {
        try
        {
            // CPU usage
            var cpuUsage = GetCpuUsage();
            if (cpuUsage >= 0)
            {
                UpdateCounter("system_cpu_usage_percent", cpuUsage);
            }

            // System memory
            var memoryInfo = GetSystemMemoryInfo();
            if (memoryInfo.TotalMemory > 0)
            {
                UpdateCounter("system_memory_total_bytes", memoryInfo.TotalMemory);
                UpdateCounter("system_memory_used_bytes", memoryInfo.UsedMemory);
                UpdateCounter("system_memory_usage_percent",

                    (double)memoryInfo.UsedMemory / memoryInfo.TotalMemory * 100.0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not sample macOS system counters");
        }
    }

    private void SampleMetalCounters()
    {
        try
        {
            // Query Metal device utilization (if native APIs support it)
            var deviceCount = MetalNative.GetDeviceCount();
            UpdateCounter("metal_devices_total", deviceCount);

            for (var i = 0; i < deviceCount; i++)
            {
                try
                {
                    var device = MetalNative.CreateDeviceAtIndex(i);
                    if (device != IntPtr.Zero)
                    {
                        var deviceInfo = MetalNative.GetDeviceInfo(device);

                        // Sample device-specific metrics if available

                        UpdateCounter($"metal_device_{i}_max_buffer_length", (double)deviceInfo.MaxBufferLength);
                        UpdateCounter($"metal_device_{i}_max_threadgroup_size", (double)deviceInfo.MaxThreadgroupSize);


                        MetalNative.ReleaseDevice(device);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Could not sample Metal device {DeviceIndex}", i);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not sample Metal counters");
        }
    }

    private void RecordThermalMetrics()
        // Placeholder for thermal monitoring
        // Would integrate with IOKit on macOS for real thermal data

        => _logger.LogTrace("Thermal metrics recording not implemented");

    private void RecordPowerMetrics()
        // Placeholder for power monitoring
        // Would integrate with IOKit on macOS for real power data

        => _logger.LogTrace("Power metrics recording not implemented");

    private static string GetAllocationSizeCategory(long sizeBytes)
    {
        return sizeBytes switch
        {
            < 4096 => "small", // < 4KB
            < 1024 * 1024 => "medium", // < 1MB
            < 100 * 1024 * 1024 => "large", // < 100MB
            _ => "xlarge"
        };
    }

    private static string GetUtilizationBand(double utilization)
    {
        return utilization switch
        {
            < 25 => "low",
            < 50 => "medium",
            < 75 => "high",
            _ => "critical"
        };
    }

    private static string GetErrorCategory(MetalError error)
    {
        return error switch
        {
            MetalError.OutOfMemory or MetalError.ResourceLimitExceeded => "resource",
            MetalError.InvalidOperation or MetalError.InvalidArgument => "validation",

            MetalError.DeviceUnavailable or MetalError.DeviceLost => "device",
            MetalError.InternalError => "internal",
            _ => "unknown"
        };
    }

    private static string SafeName(string name)
    {
        // Convert to safe counter name
        return name.ToUpperInvariant()
            .Replace(" ", "_", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal);
    }

    private static double GetCpuUsage()
    {
        // Simplified CPU usage - would use more sophisticated monitoring in production
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100.0;
        }
        catch
        {
            return -1;
        }
    }

    private static (long TotalMemory, long UsedMemory) GetSystemMemoryInfo()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = Environment.WorkingSet;
            return (workingSet, totalMemory);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static ThroughputAnalysis AnalyzeThroughput(Dictionary<string, object> counters)
    {
        var analysis = new ThroughputAnalysis();

        // Analyze kernel throughput

        if (TryGetCounterValue(counters, "kernel_executions_total", out var totalKernels) &&
            TryGetCounterValue(counters, "kernel_data_processed_bytes_total", out var totalData))
        {
            analysis.KernelThroughput = totalKernels > 0 ? totalData / totalKernels : 0;
        }

        // Analyze memory throughput

        if (TryGetCounterValue(counters, "memory_allocated_bytes_total", out var totalAllocated) &&
            TryGetCounterValue(counters, "memory_allocations_total", out var totalAllocations))
        {
            analysis.MemoryThroughput = totalAllocations > 0 ? totalAllocated / totalAllocations : 0;
        }


        return analysis;
    }

    private static ErrorRateAnalysis AnalyzeErrorRates(Dictionary<string, object> counters)
    {
        var analysis = new ErrorRateAnalysis();


        if (TryGetCounterValue(counters, "errors_total", out var totalErrors) &&
            TryGetCounterValue(counters, "kernel_executions_total", out var totalOperations))
        {
            analysis.OverallErrorRate = totalOperations > 0 ? totalErrors / totalOperations : 0;
        }


        if (TryGetCounterValue(counters, "memory_allocation_failures_total", out var memoryErrors) &&
            TryGetCounterValue(counters, "memory_allocations_total", out var memoryAllocations))
        {
            analysis.MemoryErrorRate = memoryAllocations > 0 ? memoryErrors / memoryAllocations : 0;
        }


        return analysis;
    }

    private static ResourceUtilizationAnalysis AnalyzeResourceUtilization(Dictionary<string, object> counters)
    {
        var analysis = new ResourceUtilizationAnalysis();


        if (TryGetCounterValue(counters, "gpu_utilization_percent", out var gpuUtil))
        {
            analysis.GpuUtilization = gpuUtil;
        }


        if (TryGetCounterValue(counters, "memory_utilization_percent", out var memUtil))
        {
            analysis.MemoryUtilization = memUtil;
        }


        return analysis;
    }

    private static PerformanceTrend AnalyzePerformanceTrends(Dictionary<string, object> counters)
    {
        // Simplified trend analysis - would use time series data in production
        return new PerformanceTrend
        {
            TrendDirection = TrendDirection.Stable,
            PerformanceChange = 0.0,
            Confidence = 0.5
        };
    }

    private static double CalculatePerformanceScore(MetalPerformanceAnalysis analysis)
    {
        var score = 100.0;

        // Penalize high error rates

        score -= analysis.ErrorRateAnalysis.OverallErrorRate * 1000; // -10 points per 1% error rate

        // Penalize low utilization (indicates inefficiency)

        if (analysis.ResourceUtilizationAnalysis.GpuUtilization < 20)
        {
            score -= (20 - analysis.ResourceUtilizationAnalysis.GpuUtilization) * 2;
        }

        // Penalize very high utilization (indicates resource pressure)

        if (analysis.ResourceUtilizationAnalysis.GpuUtilization > 90)
        {
            score -= (analysis.ResourceUtilizationAnalysis.GpuUtilization - 90) * 3;
        }


        return Math.Max(0, Math.Min(100, score));
    }

    private static bool TryGetCounterValue(Dictionary<string, object> counters, string name, out double value)
    {
        value = 0;


        if (counters.TryGetValue(name, out var obj) && obj is not null)
        {
            // Handle anonymous type from GetCurrentCounters
            var type = obj.GetType();
            var totalProp = type.GetProperty("Total");
            if (totalProp?.GetValue(obj) is double totalValue)
            {
                value = totalValue;
                return true;
            }
        }


        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            _samplingTimer?.Dispose();

            foreach (var counter in _counters.Values)
            {
                counter?.Dispose();
            }

            _counters.Clear();
            _statistics.Clear();

            _logger.LogDebug("Metal performance counters disposed");
        }
    }
}