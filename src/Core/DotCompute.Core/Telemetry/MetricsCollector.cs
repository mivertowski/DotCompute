using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Debugging;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// High-performance metrics collector for kernel execution, memory usage, and device utilization.
/// Provides real-time metrics collection with minimal performance impact (less than 1%).
/// </summary>
public sealed class MetricsCollector : IDisposable
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly ConcurrentDictionary<string, DeviceMetrics> _deviceMetrics;
    private readonly ConcurrentDictionary<string, KernelMetrics> _kernelMetrics;
    private readonly ConcurrentQueue<MemoryOperation> _memoryOperations;
    private readonly ConcurrentQueue<PerformanceSnapshot> _performanceSnapshots;
    private readonly Timer _collectionTimer;
    private volatile bool _disposed;

    // Performance counters

    private long _totalKernelExecutions;
    private long _totalMemoryAllocations;
    private long _totalErrors;
    private double _averageKernelDuration;
    private double _peakMemoryUsage;
    /// <summary>
    /// Initializes a new instance of the MetricsCollector class.
    /// </summary>
    /// <param name="logger">The logger.</param>

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _deviceMetrics = new ConcurrentDictionary<string, DeviceMetrics>();
        _kernelMetrics = new ConcurrentDictionary<string, KernelMetrics>();
        _memoryOperations = new ConcurrentQueue<MemoryOperation>();
        _performanceSnapshots = new ConcurrentQueue<PerformanceSnapshot>();

        // Start collection timer (every 5 seconds for high-resolution metrics)

        _collectionTimer = new Timer(CollectSystemMetrics, null,

            TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Records kernel execution metrics with detailed performance characteristics.
    /// </summary>
    public void RecordKernelExecution(string kernelName, string deviceId,

        TimeSpan executionTime, long memoryUsed, bool success,
        KernelExecutionDetails details)
    {
        ThrowIfDisposed();


        _ = Interlocked.Increment(ref _totalKernelExecutions);


        if (!success)
        {
            _ = Interlocked.Increment(ref _totalErrors);
        }

        // Update average kernel duration using exponential moving average

        var currentAvg = _averageKernelDuration;
        var newAvg = currentAvg + 0.1 * (executionTime.TotalMilliseconds - currentAvg);
        _ = Interlocked.Exchange(ref _averageKernelDuration, newAvg);

        // Update kernel-specific metrics

        _ = _kernelMetrics.AddOrUpdate(kernelName,

            new KernelMetrics
            {
                KernelName = kernelName,
                ExecutionCount = 1,
                TotalExecutionTime = executionTime,
                MinExecutionTime = executionTime,
                MaxExecutionTime = executionTime,
                TotalMemoryUsed = memoryUsed,
                SuccessCount = success ? 1 : 0,
                LastExecutionTime = DateTimeOffset.UtcNow,
                Throughput = CalculateThroughput(details),
                Occupancy = details.Occupancy,
                CacheHitRate = details.CacheHitRate
            },
            (key, existing) => UpdateKernelMetrics(existing, executionTime, memoryUsed, success, details));

        // Update device-specific metrics

        UpdateDeviceMetrics(deviceId, executionTime, memoryUsed, success);
    }

    /// <summary>
    /// Records memory allocation and transfer operations with bandwidth analysis.
    /// </summary>
    public void RecordMemoryOperation(string operationType, string deviceId,
        long bytes, TimeSpan duration, bool success, MemoryOperationDetails details)
    {
        ThrowIfDisposed();


        _ = Interlocked.Increment(ref _totalMemoryAllocations);


        if (!success)
        {
            _ = Interlocked.Increment(ref _totalErrors);
        }

        // Track peak memory usage

        var currentPeak = _peakMemoryUsage;
        if (details.CurrentMemoryUsage > currentPeak)
        {
            _ = Interlocked.Exchange(ref _peakMemoryUsage, details.CurrentMemoryUsage);
        }


        var operation = new MemoryOperation
        {
            OperationType = operationType,
            DeviceId = deviceId,
            Bytes = bytes,
            Duration = duration,
            Success = success,
            Bandwidth = bytes / duration.TotalSeconds,
            Timestamp = DateTimeOffset.UtcNow,
            AccessPattern = details.AccessPattern,
            CoalescingEfficiency = details.CoalescingEfficiency
        };


        _memoryOperations.Enqueue(operation);

        // Trim queue if it gets too large (keep last 10000 operations)

        if (_memoryOperations.Count > 10000)
        {
            _ = _memoryOperations.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Gets current memory usage across all devices.
    /// </summary>
    public long GetCurrentMemoryUsage()
    {
        ThrowIfDisposed();


        return _deviceMetrics.Values.Sum(d => d.CurrentMemoryUsage);
    }

    /// <summary>
    /// Gets current device utilization as a weighted average across all devices.
    /// </summary>
    public double GetDeviceUtilization()
    {
        ThrowIfDisposed();


        var devices = _deviceMetrics.Values.ToArray();
        if (devices.Length == 0)
        {
            return 0.0;
        }


        return devices.Average(d => d.UtilizationPercentage);
    }

    /// <summary>
    /// Gets comprehensive performance metrics for a specific kernel.
    /// </summary>
    public KernelPerformanceMetrics? GetKernelPerformanceMetrics(string kernelName)
    {
        ThrowIfDisposed();


        if (!_kernelMetrics.TryGetValue(kernelName, out var metrics))
        {

            return null;
        }


        return new KernelPerformanceMetrics
        {
            KernelName = kernelName,
            ExecutionCount = metrics.ExecutionCount,
            AverageExecutionTime = metrics.TotalExecutionTime.TotalMilliseconds / metrics.ExecutionCount,
            MinExecutionTime = metrics.MinExecutionTime.TotalMilliseconds,
            MaxExecutionTime = metrics.MaxExecutionTime.TotalMilliseconds,
            SuccessRate = (double)metrics.SuccessCount / metrics.ExecutionCount,
            AverageThroughput = metrics.Throughput,
            AverageOccupancy = metrics.Occupancy,
            CacheHitRate = metrics.CacheHitRate,
            MemoryEfficiency = CalculateMemoryEfficiency(metrics),
            LastExecutionTime = metrics.LastExecutionTime
        };
    }

    /// <summary>
    /// Gets device-specific performance metrics including temperature and utilization trends.
    /// </summary>
    public DevicePerformanceMetrics? GetDevicePerformanceMetrics(string deviceId)
    {
        ThrowIfDisposed();


        if (!_deviceMetrics.TryGetValue(deviceId, out var metrics))
        {

            return null;
        }


        return new DevicePerformanceMetrics
        {
            DeviceId = deviceId,
            UtilizationPercentage = metrics.UtilizationPercentage,
            TemperatureCelsius = metrics.TemperatureCelsius,
            CurrentMemoryUsage = metrics.CurrentMemoryUsage,
            MaxMemoryCapacity = metrics.MaxMemoryCapacity,
            TotalOperations = metrics.TotalOperations,
            ErrorCount = metrics.ErrorCount,
            AverageResponseTime = metrics.AverageResponseTime,
            ThroughputOpsPerSecond = CalculateDeviceThroughput(metrics),
            PowerConsumptionWatts = metrics.PowerConsumptionWatts,
            LastUpdateTime = metrics.LastUpdateTime
        };
    }

    /// <summary>
    /// Collects all available metrics for export to monitoring systems.
    /// </summary>
    public async Task<CollectedMetrics> CollectAllMetricsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        await Task.Yield(); // Allow other operations to continue


        var metrics = new CollectedMetrics();

        // Collect counter metrics

        metrics.Counters["total_kernel_executions"] = Interlocked.Read(ref _totalKernelExecutions);
        metrics.Counters["total_memory_allocations"] = Interlocked.Read(ref _totalMemoryAllocations);
        metrics.Counters["total_errors"] = Interlocked.Read(ref _totalErrors);

        // Collect gauge metrics

        metrics.Gauges["average_kernel_duration_ms"] = _averageKernelDuration;
        metrics.Gauges["peak_memory_usage_bytes"] = _peakMemoryUsage;
        metrics.Gauges["current_memory_usage_bytes"] = GetCurrentMemoryUsage();
        metrics.Gauges["device_utilization_percentage"] = GetDeviceUtilization();

        // Collect histogram data for response times

        var recentOperations = GetRecentMemoryOperations(TimeSpan.FromMinutes(5));
        if (recentOperations.Any())
        {
            var durations = recentOperations.Select(op => op.Duration.TotalMilliseconds).ToArray();
            metrics.Histograms["memory_operation_duration_ms"] = durations;
        }


        return metrics;
    }

    /// <summary>
    /// Gets memory access patterns for optimization analysis.
    /// </summary>
    public MemoryAccessAnalysis GetMemoryAccessAnalysis(TimeSpan timeWindow)
    {
        ThrowIfDisposed();


        var recentOperations = GetRecentMemoryOperations(timeWindow);


        return new MemoryAccessAnalysis
        {
            TotalOperations = recentOperations.Count(),
            AverageBandwidth = recentOperations.Any() ? recentOperations.Average(op => op.Bandwidth) : 0,
            PeakBandwidth = recentOperations.Any() ? recentOperations.Max(op => op.Bandwidth) : 0,
            AccessPatterns = recentOperations
                .GroupBy(op => op.AccessPattern)
                .ToDictionary(g => g.Key, g => g.Count()),
            AverageCoalescingEfficiency = recentOperations.Any() ?

                recentOperations.Average(op => op.CoalescingEfficiency) : 0,
            TimeWindow = timeWindow,
            AnalysisTimestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Detects performance bottlenecks and returns optimization recommendations.
    /// </summary>
    public List<PerformanceBottleneck> DetectBottlenecks()
    {
        ThrowIfDisposed();


        var bottlenecks = new List<PerformanceBottleneck>();

        // Check memory utilization

        foreach (var device in _deviceMetrics.Values)
        {
            var memoryUtilization = (double)device.CurrentMemoryUsage / device.MaxMemoryCapacity;
            if (memoryUtilization > 0.9)
            {
                bottlenecks.Add(new PerformanceBottleneck
                {
                    Type = BottleneckType.MemoryUtilization,
                    DeviceId = device.DeviceId,
                    Severity = BottleneckSeverity.High,
                    Description = $"Memory utilization at {memoryUtilization:P1}",
                    Recommendation = "Consider memory optimization or load balancing",
                    MetricValue = memoryUtilization
                });
            }
        }

        // Check kernel performance

        foreach (var kernel in _kernelMetrics.Values)
        {
            var successRate = (double)kernel.SuccessCount / kernel.ExecutionCount;
            if (successRate < 0.95)
            {
                bottlenecks.Add(new PerformanceBottleneck
                {
                    Type = BottleneckType.KernelFailures,
                    KernelName = kernel.KernelName,
                    Severity = BottleneckSeverity.Medium,
                    Description = $"Kernel success rate at {successRate:P1}",
                    Recommendation = "Review kernel implementation and error handling",
                    MetricValue = successRate
                });
            }
        }


        return bottlenecks;
    }

    private void CollectSystemMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Collect system-level performance metrics
            var snapshot = new PerformanceSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                TotalMemoryUsage = GetCurrentMemoryUsage(),
                AverageDeviceUtilization = GetDeviceUtilization(),
                ActiveKernels = _kernelMetrics.Count,
                ProcessorTime = GetProcessorTime(),
                GcMemoryUsage = GC.GetTotalMemory(false)
            };


            _performanceSnapshots.Enqueue(snapshot);

            // Trim snapshots (keep last hour at 5-second intervals = 720 snapshots)

            if (_performanceSnapshots.Count > 720)
            {
                _ = _performanceSnapshots.TryDequeue(out _);
            }

            // Update device metrics if available

            UpdateDeviceUtilizationFromSystem();
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to collect system metrics");
        }
    }

    private static KernelMetrics UpdateKernelMetrics(KernelMetrics existing, TimeSpan executionTime,
        long memoryUsed, bool success, KernelExecutionDetails details)
    {
        existing.ExecutionCount++;
        existing.TotalExecutionTime += executionTime;
        existing.TotalMemoryUsed += memoryUsed;
        existing.LastExecutionTime = DateTimeOffset.UtcNow;


        if (success)
        {
            existing.SuccessCount++;
        }


        if (executionTime < existing.MinExecutionTime)
        {
            existing.MinExecutionTime = executionTime;
        }


        if (executionTime > existing.MaxExecutionTime)
        {
            existing.MaxExecutionTime = executionTime;
        }

        // Update moving averages

        existing.Throughput = (existing.Throughput + CalculateThroughput(details)) / 2;
        existing.Occupancy = (existing.Occupancy + details.Occupancy) / 2;
        existing.CacheHitRate = (existing.CacheHitRate + details.CacheHitRate) / 2;


        return existing;
    }

    private void UpdateDeviceMetrics(string deviceId, TimeSpan executionTime,

        long memoryUsed, bool success)
    {
        _ = _deviceMetrics.AddOrUpdate(deviceId,
            new DeviceMetrics
            {
                DeviceId = deviceId,
                TotalOperations = 1,
                ErrorCount = success ? 0 : 1,
                CurrentMemoryUsage = memoryUsed,
                AverageResponseTime = executionTime.TotalMilliseconds,
                LastUpdateTime = DateTimeOffset.UtcNow
            },
            (key, existing) =>
            {
                existing.TotalOperations++;
                if (!success)
                {
                    existing.ErrorCount++;
                }


                existing.AverageResponseTime =

                    (existing.AverageResponseTime + executionTime.TotalMilliseconds) / 2;
                existing.LastUpdateTime = DateTimeOffset.UtcNow;
                return existing;
            });
    }

    private void UpdateDeviceUtilizationFromSystem()
    {
        // This would integrate with actual hardware monitoring APIs
        // For now, simulate with placeholder values
        foreach (var device in _deviceMetrics.Keys.ToArray())
        {
            if (_deviceMetrics.TryGetValue(device, out var metrics))
            {
                // Update utilization based on recent activity
                var recentActivity = _performanceSnapshots
                    .Where(s => s.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-1))
                    .Count();


                metrics.UtilizationPercentage = Math.Min(100, recentActivity * 2);
            }
        }
    }

    private static double CalculateThroughput(KernelExecutionDetails details)
    {
        if (details.ExecutionTime.TotalSeconds == 0)
        {
            return 0;
        }


        return details.OperationsPerformed / details.ExecutionTime.TotalSeconds;
    }

    private static double CalculateMemoryEfficiency(KernelMetrics metrics)
    {
        // Simplified efficiency calculation based on execution time vs memory usage
        var avgExecutionTime = metrics.TotalExecutionTime.TotalMilliseconds / metrics.ExecutionCount;
        var avgMemoryUsage = (double)metrics.TotalMemoryUsed / metrics.ExecutionCount;

        // Higher efficiency means better performance per memory unit

        return avgMemoryUsage > 0 ? 1000.0 / (avgExecutionTime * Math.Log10(avgMemoryUsage + 1)) : 0;
    }

    private static double CalculateDeviceThroughput(DeviceMetrics metrics)
    {
        var timeSinceFirstOperation = DateTimeOffset.UtcNow - metrics.LastUpdateTime;
        return timeSinceFirstOperation.TotalSeconds > 0 ?

            metrics.TotalOperations / timeSinceFirstOperation.TotalSeconds : 0;
    }

    private IEnumerable<MemoryOperation> GetRecentMemoryOperations(TimeSpan timeWindow)
    {
        var cutoff = DateTimeOffset.UtcNow - timeWindow;
        return _memoryOperations.Where(op => op.Timestamp > cutoff);
    }

    private static double GetProcessorTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime.TotalMilliseconds;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MetricsCollector));
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        _collectionTimer?.Dispose();
    }
}
/// <summary>
/// A class that represents device metrics.
/// </summary>

// Supporting data structures and enums follow...
public sealed class DeviceMetrics
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
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
    /// Gets or sets the current memory usage.
    /// </summary>
    /// <value>The current memory usage.</value>
    public long CurrentMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the max memory capacity.
    /// </summary>
    /// <value>The max memory capacity.</value>
    public long MaxMemoryCapacity { get; set; } = long.MaxValue;
    /// <summary>
    /// Gets or sets the total operations.
    /// </summary>
    /// <value>The total operations.</value>
    public long TotalOperations { get; set; }
    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    /// <value>The error count.</value>
    public long ErrorCount { get; set; }
    /// <summary>
    /// Gets or sets the average response time.
    /// </summary>
    /// <value>The average response time.</value>
    public double AverageResponseTime { get; set; }
    /// <summary>
    /// Gets or sets the power consumption watts.
    /// </summary>
    /// <value>The power consumption watts.</value>
    public double PowerConsumptionWatts { get; set; }
    /// <summary>
    /// Gets or sets the last update time.
    /// </summary>
    /// <value>The last update time.</value>
    public DateTimeOffset LastUpdateTime { get; set; }
}
/// <summary>
/// A class that represents kernel metrics.
/// </summary>

public sealed class KernelMetrics
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    /// <value>The execution count.</value>
    public long ExecutionCount { get; set; }
    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    /// <value>The total execution time.</value>
    public TimeSpan TotalExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the min execution time.
    /// </summary>
    /// <value>The min execution time.</value>
    public TimeSpan MinExecutionTime { get; set; } = TimeSpan.MaxValue;
    /// <summary>
    /// Gets or sets the max execution time.
    /// </summary>
    /// <value>The max execution time.</value>
    public TimeSpan MaxExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the total memory used.
    /// </summary>
    /// <value>The total memory used.</value>
    public long TotalMemoryUsed { get; set; }
    /// <summary>
    /// Gets or sets the success count.
    /// </summary>
    /// <value>The success count.</value>
    public long SuccessCount { get; set; }
    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    /// <value>The last execution time.</value>
    public DateTimeOffset LastExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the throughput.
    /// </summary>
    /// <value>The throughput.</value>
    public double Throughput { get; set; }
    /// <summary>
    /// Gets or sets the occupancy.
    /// </summary>
    /// <value>The occupancy.</value>
    public double Occupancy { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
}
/// <summary>
/// A class that represents memory operation.
/// </summary>

public sealed class MemoryOperation
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
    /// Gets or sets the bytes.
    /// </summary>
    /// <value>The bytes.</value>
    public long Bytes { get; set; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan Duration { get; set; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; set; }
    /// <summary>
    /// Gets or sets the bandwidth.
    /// </summary>
    /// <value>The bandwidth.</value>
    public double Bandwidth { get; set; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
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
}
/// <summary>
/// A class that represents performance snapshot.
/// </summary>

public sealed class PerformanceSnapshot
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the total memory usage.
    /// </summary>
    /// <value>The total memory usage.</value>
    public long TotalMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the average device utilization.
    /// </summary>
    /// <value>The average device utilization.</value>
    public double AverageDeviceUtilization { get; set; }
    /// <summary>
    /// Gets or sets the active kernels.
    /// </summary>
    /// <value>The active kernels.</value>
    public int ActiveKernels { get; set; }
    /// <summary>
    /// Gets or sets the processor time.
    /// </summary>
    /// <value>The processor time.</value>
    public double ProcessorTime { get; set; }
    /// <summary>
    /// Gets or sets the gc memory usage.
    /// </summary>
    /// <value>The gc memory usage.</value>
    public long GcMemoryUsage { get; set; }
}
/// <summary>
/// A class that represents kernel execution details.
/// </summary>

public sealed class KernelExecutionDetails
{
    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    /// <value>The execution time.</value>
    public TimeSpan ExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the operations performed.
    /// </summary>
    /// <value>The operations performed.</value>
    public long OperationsPerformed { get; set; }
    /// <summary>
    /// Gets or sets the occupancy.
    /// </summary>
    /// <value>The occupancy.</value>
    public double Occupancy { get; set; }
    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
}
/// <summary>
/// A class that represents memory operation details.
/// </summary>

public sealed class MemoryOperationDetails
{
    /// <summary>
    /// Gets or sets the current memory usage.
    /// </summary>
    /// <value>The current memory usage.</value>
    public long CurrentMemoryUsage { get; set; }
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
}
/// <summary>
/// A class that represents kernel performance metrics.
/// </summary>

public sealed class KernelPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    /// <value>The execution count.</value>
    public long ExecutionCount { get; set; }
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
    /// Gets or sets the success rate.
    /// </summary>
    /// <value>The success rate.</value>
    public double SuccessRate { get; set; }
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
    /// Gets or sets the cache hit rate.
    /// </summary>
    /// <value>The cache hit rate.</value>
    public double CacheHitRate { get; set; }
    /// <summary>
    /// Gets or sets the memory efficiency.
    /// </summary>
    /// <value>The memory efficiency.</value>
    public double MemoryEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    /// <value>The last execution time.</value>
    public DateTimeOffset LastExecutionTime { get; set; }
}
/// <summary>
/// A class that represents device performance metrics.
/// </summary>

public sealed class DevicePerformanceMetrics
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string DeviceId { get; set; } = string.Empty;
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
    /// Gets or sets the current memory usage.
    /// </summary>
    /// <value>The current memory usage.</value>
    public long CurrentMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the max memory capacity.
    /// </summary>
    /// <value>The max memory capacity.</value>
    public long MaxMemoryCapacity { get; set; }
    /// <summary>
    /// Gets or sets the total operations.
    /// </summary>
    /// <value>The total operations.</value>
    public long TotalOperations { get; set; }
    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    /// <value>The error count.</value>
    public long ErrorCount { get; set; }
    /// <summary>
    /// Gets or sets the average response time.
    /// </summary>
    /// <value>The average response time.</value>
    public double AverageResponseTime { get; set; }
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public double ThroughputOpsPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the power consumption watts.
    /// </summary>
    /// <value>The power consumption watts.</value>
    public double PowerConsumptionWatts { get; set; }
    /// <summary>
    /// Gets or sets the last update time.
    /// </summary>
    /// <value>The last update time.</value>
    public DateTimeOffset LastUpdateTime { get; set; }
}
/// <summary>
/// A class that represents memory access analysis.
/// </summary>

public sealed class MemoryAccessAnalysis
{
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
    /// Gets or sets the access patterns.
    /// </summary>
    /// <value>The access patterns.</value>
    public Dictionary<string, int> AccessPatterns { get; } = [];
    /// <summary>
    /// Gets or sets the average coalescing efficiency.
    /// </summary>
    /// <value>The average coalescing efficiency.</value>
    public double AverageCoalescingEfficiency { get; set; }
    /// <summary>
    /// Gets or sets the time window.
    /// </summary>
    /// <value>The time window.</value>
    public TimeSpan TimeWindow { get; set; }
    /// <summary>
    /// Gets or sets the analysis timestamp.
    /// </summary>
    /// <value>The analysis timestamp.</value>
    public DateTimeOffset AnalysisTimestamp { get; set; }
}
/// <summary>
/// A class that represents performance bottleneck.
/// </summary>

public sealed class PerformanceBottleneck
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public BottleneckType Type { get; set; }
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public string? DeviceId { get; set; }
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string? KernelName { get; set; }
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public BottleneckSeverity Severity { get; set; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the recommendation.
    /// </summary>
    /// <value>The recommendation.</value>
    public string Recommendation { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the metric value.
    /// </summary>
    /// <value>The metric value.</value>
    public double MetricValue { get; set; }
}


// BottleneckSeverity enum consolidated to DotCompute.Abstractions.Debugging.PerformanceAnalysisResult
