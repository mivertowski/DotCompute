// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Backends.CPU.Threading;

namespace DotCompute.Backends.CPU.Performance;


/// <summary>
/// Performance metrics and profiling for CPU kernels.
/// </summary>
public sealed class KernelPerformanceMetrics
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets the execution count.
    /// </summary>
    public required long ExecutionCount { get; init; }

    /// <summary>
    /// Gets the total execution time in milliseconds.
    /// </summary>
    public required double TotalExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the average execution time in milliseconds.
    /// </summary>
    public required double AverageExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets whether vectorization is enabled.
    /// </summary>
    public required bool VectorizationEnabled { get; init; }

    /// <summary>
    /// Gets the vector width in bits.
    /// </summary>
    public required int VectorWidth { get; init; }

    /// <summary>
    /// Gets the supported instruction sets.
    /// </summary>
    public required IReadOnlySet<string> InstructionSets { get; init; }

    /// <summary>
    /// Gets the minimum execution time in milliseconds.
    /// </summary>
    public double MinExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the maximum execution time in milliseconds.
    /// </summary>
    public double MaxExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the standard deviation of execution times.
    /// </summary>
    public double ExecutionTimeStdDev { get; init; }

    /// <summary>
    /// Gets the throughput in operations per second.
    /// </summary>
    public double ThroughputOpsPerSec => AverageExecutionTimeMs > 0 ? 1000.0 / AverageExecutionTimeMs : 0;

    /// <summary>
    /// Gets the efficiency relative to peak theoretical performance.
    /// </summary>
    public double Efficiency { get; init; }

    /// <summary>
    /// Gets cache hit ratio (if available).
    /// </summary>
    public double CacheHitRatio { get; init; }

    /// <summary>
    /// Gets memory bandwidth utilization percentage.
    /// </summary>
    public double MemoryBandwidthUtilization { get; init; }

    /// <summary>
    /// Gets the NUMA node statistics.
    /// </summary>
    public IReadOnlyDictionary<int, NumaNodeMetrics>? NumaMetrics { get; init; }
}

/// <summary>
/// Performance metrics for a specific NUMA node.
/// </summary>
public sealed class NumaNodeMetrics
{
    /// <summary>
    /// Gets the node ID.
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// Gets the execution count on this node.
    /// </summary>
    public required long ExecutionCount { get; init; }

    /// <summary>
    /// Gets the total execution time on this node.
    /// </summary>
    public required double TotalExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the memory allocations on this node.
    /// </summary>
    public required long MemoryAllocations { get; init; }

    /// <summary>
    /// Gets the memory usage in bytes.
    /// </summary>
    public required long MemoryUsageBytes { get; init; }
}

/// <summary>
/// Real-time performance profiler for CPU kernels.
/// </summary>
internal sealed class CpuKernelProfiler : IDisposable
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, ProfileData> _kernelProfiles = new();
    private readonly Timer _samplingTimer;
    internal readonly PerformanceCounterManager _perfCounters;
    private readonly int _samplingIntervalMs;
    private int _disposed;

    public CpuKernelProfiler(int samplingIntervalMs = 100)
    {
        _samplingIntervalMs = samplingIntervalMs;
        _perfCounters = new PerformanceCounterManager();
        _samplingTimer = new Timer(SamplePerformance, null, TimeSpan.FromMilliseconds(samplingIntervalMs), TimeSpan.FromMilliseconds(samplingIntervalMs));
    }

    /// <summary>
    /// Starts profiling a kernel execution.
    /// </summary>
    public ProfilingSession StartProfiling(string kernelName, bool useVectorization, int vectorWidth)
    {
        ThrowIfDisposed();

        var sessionId = Guid.NewGuid();
        var startTime = Stopwatch.GetTimestamp();
        var startCounters = _perfCounters.Sample();

        return new ProfilingSession(sessionId, kernelName, startTime, startCounters, useVectorization, vectorWidth, this);
    }

    /// <summary>
    /// Completes a profiling session and updates metrics.
    /// </summary>
    internal void CompleteProfiling(ProfilingSession session, long endTime, PerformanceCounterSample endCounters)
    {
        var elapsedTicks = endTime - session.StartTime;
        var elapsedMs = (double)elapsedTicks / Stopwatch.Frequency * 1000.0;

        var profile = _kernelProfiles.GetOrAdd(session.KernelName, _ => new ProfileData(session.KernelName));

        lock (profile)
        {
            profile.RecordExecution(elapsedMs, session.UseVectorization, session.VectorWidth, session.StartCounters, endCounters);
        }
    }

    /// <summary>
    /// Gets performance metrics for a specific kernel.
    /// </summary>
    public KernelPerformanceMetrics GetMetrics(string kernelName)
    {
        ThrowIfDisposed();

        if (!_kernelProfiles.TryGetValue(kernelName, out var profile))
        {
            return new KernelPerformanceMetrics
            {
                KernelName = kernelName,
                ExecutionCount = 0,
                TotalExecutionTimeMs = 0,
                AverageExecutionTimeMs = 0,
                VectorizationEnabled = false,
                VectorWidth = 0,
                InstructionSets = new HashSet<string>()
            };
        }

        lock (profile)
        {
            return profile.GetMetrics();
        }
    }

    /// <summary>
    /// Gets metrics for all profiled kernels.
    /// </summary>
    public IReadOnlyDictionary<string, KernelPerformanceMetrics> GetAllMetrics()
    {
        ThrowIfDisposed();

        var metrics = new Dictionary<string, KernelPerformanceMetrics>();

        foreach (var kvp in _kernelProfiles)
        {
            lock (kvp.Value)
            {
                metrics[kvp.Key] = kvp.Value.GetMetrics();
            }
        }

        return metrics;
    }

    /// <summary>
    /// Resets all profiling data.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();

        _kernelProfiles.Clear();
    }

    private void SamplePerformance(object? state)
    {
        if (_disposed != 0)
        {
            return;
        }

        try
        {
            var sample = _perfCounters.Sample();

            // Update all active profiles with current performance counters
            foreach (var profile in _kernelProfiles.Values)
            {
                lock (profile)
                {
                    profile.UpdatePerformanceCounters(sample);
                }
            }
        }
        catch
        {
            // Ignore sampling errors
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _samplingTimer?.Dispose();
        _perfCounters?.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);
}

/// <summary>
/// Represents an active profiling session.
/// </summary>
public sealed class ProfilingSession : IDisposable
{
    private readonly CpuKernelProfiler _profiler;
    private int _disposed;

    internal ProfilingSession(
        Guid sessionId,
        string kernelName,
        long startTime,
        PerformanceCounterSample startCounters,
        bool useVectorization,
        int vectorWidth,
        CpuKernelProfiler profiler)
    {
        SessionId = sessionId;
        KernelName = kernelName;
        StartTime = startTime;
        StartCounters = startCounters;
        UseVectorization = useVectorization;
        VectorWidth = vectorWidth;
        _profiler = profiler;
    }

    /// <summary>
    /// Gets the session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string KernelName { get; }

    /// <summary>
    /// Gets the start time in ticks.
    /// </summary>
    public long StartTime { get; }

    /// <summary>
    /// Gets the start performance counters.
    /// </summary>
    public PerformanceCounterSample StartCounters { get; }

    /// <summary>
    /// Gets whether vectorization is used.
    /// </summary>
    public bool UseVectorization { get; }

    /// <summary>
    /// Gets the vector width.
    /// </summary>
    public int VectorWidth { get; }

    /// <summary>
    /// Completes the profiling session.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var endTime = Stopwatch.GetTimestamp();
        var endCounters = _profiler._perfCounters.Sample();

        _profiler.CompleteProfiling(this, endTime, endCounters);
    }
}

/// <summary>
/// Internal profile data for a kernel.
/// </summary>
internal sealed class ProfileData
{
    private readonly string _kernelName;
    private readonly List<double> _executionTimes = [];
    private readonly List<PerformanceCounterSample> _performanceSamples = [];
    private long _executionCount;
    private double _totalExecutionTime;
    private double _minExecutionTime = double.MaxValue;
    private double _maxExecutionTime;
    private bool _lastUseVectorization;
    private int _lastVectorWidth;
    private readonly HashSet<string> _instructionSets = [];

    public ProfileData(string kernelName)
    {
        _kernelName = kernelName;
    }

    public void RecordExecution(double executionTimeMs, bool useVectorization, int vectorWidth, PerformanceCounterSample startCounters, PerformanceCounterSample endCounters)
    {
        _executionCount++;
        _totalExecutionTime += executionTimeMs;
        _minExecutionTime = Math.Min(_minExecutionTime, executionTimeMs);
        _maxExecutionTime = Math.Max(_maxExecutionTime, executionTimeMs);
        _lastUseVectorization = useVectorization;
        _lastVectorWidth = vectorWidth;

        _executionTimes.Add(executionTimeMs);
        _performanceSamples.Add(endCounters);

        // Record instruction sets based on vector width
        if (useVectorization)
        {
            switch (vectorWidth)
            {
                case >= 512:
                    _ = _instructionSets.Add("AVX512F");
                    break;
                case >= 256:
                    _ = _instructionSets.Add("AVX2");
                    break;
                case >= 128:
                    _ = _instructionSets.Add("SSE");
                    break;
            }
        }

        // Keep only recent samples to prevent unbounded growth
        if (_executionTimes.Count > 1000)
        {
            _executionTimes.RemoveAt(0);
            _performanceSamples.RemoveAt(0);
        }
    }

    public void UpdatePerformanceCounters(PerformanceCounterSample sample)
    {
        // Store latest performance sample for real-time monitoring
        if (_performanceSamples.Count > 0)
        {
            _performanceSamples[^1] = sample;
        }
    }

    public KernelPerformanceMetrics GetMetrics()
    {
        var averageTime = _executionCount > 0 ? _totalExecutionTime / _executionCount : 0;
        var stdDev = CalculateStandardDeviation(_executionTimes, averageTime);
        var efficiency = CalculateEfficiency();
        var cacheHitRatio = CalculateCacheHitRatio();
        var memoryBandwidth = CalculateMemoryBandwidthUtilization();

        return new KernelPerformanceMetrics
        {
            KernelName = _kernelName,
            ExecutionCount = _executionCount,
            TotalExecutionTimeMs = _totalExecutionTime,
            AverageExecutionTimeMs = averageTime,
            MinExecutionTimeMs = _minExecutionTime == double.MaxValue ? 0 : _minExecutionTime,
            MaxExecutionTimeMs = _maxExecutionTime,
            ExecutionTimeStdDev = stdDev,
            VectorizationEnabled = _lastUseVectorization,
            VectorWidth = _lastVectorWidth,
            InstructionSets = _instructionSets.ToHashSet(),
            Efficiency = efficiency,
            CacheHitRatio = cacheHitRatio,
            MemoryBandwidthUtilization = memoryBandwidth
        };
    }

    private static double CalculateStandardDeviation(IReadOnlyList<double> values, double mean)
    {
        if (values.Count <= 1)
        {
            return 0;
        }

        var sumSquaredDifferences = values.Sum(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(sumSquaredDifferences / (values.Count - 1));
    }

    private double CalculateEfficiency()
    {
        // Calculate efficiency based on theoretical peak performance
        // This is a simplified calculation - in practice, it would consider
        // the specific operation type and hardware capabilities
        if (!_lastUseVectorization || _executionCount == 0)
        {
            return 0.5; // Assume 50% efficiency for scalar operations
        }

        var theoreticalOpsPerMs = _lastVectorWidth switch
        {
            512 => 16 * 1000, // 16 operations per cycle at 1GHz
            256 => 8 * 1000,  // 8 operations per cycle at 1GHz
            128 => 4 * 1000,  // 4 operations per cycle at 1GHz
            _ => 1 * 1000     // 1 operation per cycle at 1GHz
        };

        var averageTime = _totalExecutionTime / _executionCount;
        var actualOpsPerMs = averageTime > 0 ? 1000.0 / averageTime : 0;

        return Math.Min(1.0, actualOpsPerMs / theoreticalOpsPerMs);
    }

    private double CalculateCacheHitRatio()
    {
        // Calculate cache hit ratio from performance counters
        if (_performanceSamples.Count == 0)
        {
            return 0;
        }

        var latest = _performanceSamples[^1];
        return latest.CacheHitRatio;
    }

    private double CalculateMemoryBandwidthUtilization()
    {
        // Calculate memory bandwidth utilization
        if (_performanceSamples.Count == 0)
        {
            return 0;
        }

        var latest = _performanceSamples[^1];
        return latest.MemoryBandwidthUtilization;
    }
}

/// <summary>
/// Manages hardware performance counters.
/// </summary>
internal sealed class PerformanceCounterManager : IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private readonly PerformanceCounter? _cacheCounter;
    private bool _disposed;

    public PerformanceCounterManager()
    {
        try
        {
            // Initialize performance counters based on platform
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes", true);
                _cacheCounter = new PerformanceCounter("Memory", "Cache Bytes", true);
            }
            // Linux performance counters would be implemented using perf_event_open
            // macOS would use system_profiler or similar APIs
        }
        catch
        {
            // Performance counters may not be available in all environments
        }
    }

    /// <summary>
    /// Samples current performance counters.
    /// </summary>
    public PerformanceCounterSample Sample()
    {
        if (_disposed)
        {
            return new PerformanceCounterSample();
        }

        try
        {
            var cpuUsage = _cpuCounter?.NextValue() ?? 0;
            var availableMemory = _memoryCounter?.NextValue() ?? 0;

            return new PerformanceCounterSample
            {
                Timestamp = DateTimeOffset.UtcNow,
                CpuUsagePercent = cpuUsage,
                AvailableMemoryMB = availableMemory,
                CacheHitRatio = EstimateCacheHitRatio(),
                MemoryBandwidthUtilization = EstimateMemoryBandwidthUtilization()
            };
        }
        catch
        {
            return new PerformanceCounterSample();
        }
    }

    private static double EstimateCacheHitRatio()
        // In a full implementation, this would read actual cache performance counters
        // For now, return a reasonable estimate

        => 0.85; // Assume 85% cache hit ratio

    private static double EstimateMemoryBandwidthUtilization()
        // In a full implementation, this would measure actual memory bandwidth usage
        // For now, return a reasonable estimate

        => 0.25; // Assume 25% memory bandwidth utilization

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
        _cacheCounter?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Snapshot of performance counters at a specific time.
/// </summary>
public readonly struct PerformanceCounterSample
{
    /// <summary>
    /// Gets the timestamp of the sample.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the CPU usage percentage.
    /// </summary>
    public float CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets the available memory in megabytes.
    /// </summary>
    public float AvailableMemoryMB { get; init; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    public double CacheHitRatio { get; init; }

    /// <summary>
    /// Gets the memory bandwidth utilization percentage.
    /// </summary>
    public double MemoryBandwidthUtilization { get; init; }
}

/// <summary>
/// Performance analysis and optimization recommendations.
/// </summary>
public static class PerformanceAnalyzer
{
    /// <summary>
    /// Analyzes kernel performance and provides optimization recommendations.
    /// </summary>
    public static PerformanceAnalysis AnalyzeKernel(KernelPerformanceMetrics metrics)
    {
        var recommendations = new List<string>();
        var issues = new List<string>();
        var score = CalculatePerformanceScore(metrics);

        // Analyze execution time variance
        if (metrics.ExecutionTimeStdDev > metrics.AverageExecutionTimeMs * 0.1)
        {
            issues.Add("High execution time variance detected");
            recommendations.Add("Consider reducing system load or improving data locality");
        }

        // Analyze vectorization efficiency
        if (metrics.VectorizationEnabled && metrics.Efficiency < 0.7)
        {
            issues.Add("Low vectorization efficiency");
            recommendations.Add("Optimize memory access patterns for better SIMD utilization");
        }

        // Analyze cache performance
        if (metrics.CacheHitRatio < 0.8)
        {
            issues.Add("Low cache hit ratio");
            recommendations.Add("Improve data locality by restructuring memory access patterns");
        }

        // Analyze memory bandwidth
        if (metrics.MemoryBandwidthUtilization > 0.8)
        {
            issues.Add("High memory bandwidth utilization");
            recommendations.Add("Consider data compression or reducing memory traffic");
        }

        // Analyze instruction set usage
        if (!metrics.VectorizationEnabled && Environment.ProcessorCount > 4)
        {
            issues.Add("Vectorization not enabled on multi-core system");
            recommendations.Add("Enable SIMD vectorization for better performance");
        }

        return new PerformanceAnalysis
        {
            PerformanceScore = score,
            Issues = issues,
            Recommendations = recommendations,
            Analysis = GenerateDetailedAnalysis(metrics)
        };
    }

    private static double CalculatePerformanceScore(KernelPerformanceMetrics metrics)
    {
        // Calculate a composite performance score (0-100)
        var scores = new[]
        {
        metrics.Efficiency * 100,
        metrics.CacheHitRatio * 100,
        Math.Max(0, 100 - metrics.MemoryBandwidthUtilization * 100),
        metrics.VectorizationEnabled ? 100 : 50
    };

        return scores.Average();
    }

    private static string GenerateDetailedAnalysis(KernelPerformanceMetrics metrics)
    {
        var analysis = new System.Text.StringBuilder();

        _ = analysis.AppendLine($"Kernel '{metrics.KernelName}' Performance Analysis:");
        _ = analysis.AppendLine($"- Execution Count: {metrics.ExecutionCount:N0}");
        _ = analysis.AppendLine($"- Average Execution Time: {metrics.AverageExecutionTimeMs:F2} ms");
        _ = analysis.AppendLine($"- Throughput: {metrics.ThroughputOpsPerSec:F2} ops/sec");
        _ = analysis.AppendLine($"- Vectorization: {(metrics.VectorizationEnabled ? $"Enabled ({metrics.VectorWidth}-bit)" : "Disabled")}");
        _ = analysis.AppendLine($"- Efficiency: {metrics.Efficiency:P1}");
        _ = analysis.AppendLine($"- Cache Hit Ratio: {metrics.CacheHitRatio:P1}");
        _ = analysis.AppendLine($"- Memory Bandwidth Utilization: {metrics.MemoryBandwidthUtilization:P1}");

        if (metrics.InstructionSets.Count > 0)
        {
            _ = analysis.AppendLine($"- Instruction Sets: {string.Join(", ", metrics.InstructionSets)}");
        }

        return analysis.ToString();
    }
}

/// <summary>
/// Results of performance analysis.
/// </summary>
public sealed class PerformanceAnalysis
{
    /// <summary>
    /// Gets the overall performance score (0-100).
    /// </summary>
    public required double PerformanceScore { get; init; }

    /// <summary>
    /// Gets the list of performance issues identified.
    /// </summary>
    public required IReadOnlyList<string> Issues { get; init; }

    /// <summary>
    /// Gets the list of optimization recommendations.
    /// </summary>
    public required IReadOnlyList<string> Recommendations { get; init; }

    /// <summary>
    /// Gets the detailed analysis text.
    /// </summary>
    public required string Analysis { get; init; }
}
