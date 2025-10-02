// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Debugging.Types;
using DotCompute.Abstractions.Performance;
using DotCompute.Core.Optimization.Performance;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Provides comprehensive performance profiling and analysis for kernel execution.
/// Tracks execution traces, memory usage, and performance metrics across backends.
/// </summary>
public sealed class KernelProfiler : IDisposable
{
    private readonly ILogger<KernelProfiler> _logger;
    private readonly DebugServiceOptions _options;
    private readonly ConcurrentQueue<KernelExecutionResult> _executionHistory;
    private readonly ConcurrentDictionary<string, PerformanceProfile> _performanceProfiles;
    private bool _disposed;

    public KernelProfiler(
        ILogger<KernelProfiler> logger,
        DebugServiceOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _executionHistory = new ConcurrentQueue<KernelExecutionResult>();
        _performanceProfiles = new ConcurrentDictionary<string, PerformanceProfile>();
    }

    /// <summary>
    /// Traces kernel execution with detailed performance analysis.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to trace.</param>
    /// <param name="inputs">Input parameters for the kernel.</param>
    /// <param name="tracePoints">Specific points to trace during execution.</param>
    /// <param name="accelerator">Accelerator to use for execution.</param>
    /// <param name="backendType">Type of backend being used.</param>
    /// <returns>Detailed execution trace.</returns>
    public async Task<KernelExecutionTrace> TraceKernelExecutionAsync(
        string kernelName,
        object[] inputs,
        string[] tracePoints,
        IAccelerator accelerator,
        string backendType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(tracePoints);
        ArgumentNullException.ThrowIfNull(accelerator);

        var stopwatch = Stopwatch.StartNew();
        var tracePointsList = new List<TracePoint>();

        try
        {
            _logger.LogDebug("Starting kernel execution trace for {KernelName} on {BackendType}", kernelName, backendType);

            // Record initial state
            var initialMemory = GetCurrentMemoryUsage();
            var initialTime = stopwatch.Elapsed;

            tracePointsList.Add(new TracePoint
            {
                Name = "ExecutionStart",
                Timestamp = DateTime.UtcNow.Subtract(initialTime),
                MemoryUsage = initialMemory,
                Data = new Dictionary<string, object>
                {
                    ["InputCount"] = inputs.Length,
                    ["BackendType"] = backendType
                }
            });

            // Execute kernel with tracing
            var result = await ExecuteKernelWithTracingAsync(kernelName, inputs, accelerator, tracePointsList, tracePoints);

            // Record final state
            var finalMemory = GetCurrentMemoryUsage();
            var finalTime = stopwatch.Elapsed;

            tracePointsList.Add(new TracePoint
            {
                Name = "ExecutionEnd",
                Timestamp = DateTime.UtcNow.Subtract(finalTime),
                MemoryUsage = finalMemory,
                Data = new Dictionary<string, object>
                {
                    ["Success"] = result != null,
                    ["TotalTime"] = finalTime.TotalMilliseconds
                }
            });

            stopwatch.Stop();

            var trace = new KernelExecutionTrace
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = result != null,
                Result = result,
                TotalExecutionTime = stopwatch.Elapsed,
                TracePoints = tracePointsList,
                MemoryProfile = ConvertToAbstractionsMemoryProfile(CreateMemoryProfile(tracePointsList)),
                PerformanceMetrics = CreatePerformanceMetrics(tracePointsList, stopwatch.Elapsed)
            };

            // Store execution for historical analysis
            StoreExecutionResult(new KernelExecutionResult
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = result != null,
                Result = result,
                ExecutionTime = stopwatch.Elapsed,
                MemoryUsed = finalMemory - initialMemory
            });

            _logger.LogDebug("Completed kernel execution trace for {KernelName}: {Success}", kernelName, result != null);
            return trace;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during kernel execution trace for {KernelName}", kernelName);

            return new KernelExecutionTrace
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = false,
                ErrorMessage = ex.Message,
                TotalExecutionTime = stopwatch.Elapsed,
                TracePoints = tracePointsList
            };
        }
    }

    /// <summary>
    /// Generates a comprehensive performance report for a kernel.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <param name="timeWindow">Time window for historical analysis.</param>
    /// <returns>Performance report with trends and recommendations.</returns>
    public async Task<KernelPerformanceReport> GeneratePerformanceReportAsync(
        string kernelName,
        TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        await Task.CompletedTask; // Make async for consistency

        var cutoffTime = DateTime.UtcNow - (timeWindow ?? TimeSpan.FromHours(24));
        var relevantExecutions = _executionHistory
            .Where(e => e.KernelName == kernelName && e.ExecutionTime > TimeSpan.Zero)
            .ToList();

        if (relevantExecutions.Count == 0)
        {
            return new KernelPerformanceReport
            {
                KernelName = kernelName,
                ExecutionCount = 0,
                Summary = "No execution data available for analysis"
            };
        }

        // Group by backend for analysis
        var backendGroups = relevantExecutions.GroupBy(e => e.BackendType).ToList();
        var backendMetrics = new Dictionary<string, PerformanceMetrics>();
        var recommendations = new List<string>();

        foreach (var group in backendGroups)
        {
            var executions = group.ToList();
            var metrics = AnalyzeExecutionGroup(executions);
            backendMetrics[group.Key] = metrics;

            // Generate backend-specific recommendations
            var backendRecommendations = GenerateBackendRecommendations(group.Key, metrics, executions);
            recommendations.AddRange(backendRecommendations);
        }

        // Cross-backend analysis
        var crossBackendRecommendations = GenerateCrossBackendRecommendations(backendMetrics);
        recommendations.AddRange(crossBackendRecommendations);

        return new KernelPerformanceReport
        {
            KernelName = kernelName,
            ExecutionCount = relevantExecutions.Count,
            Backends = backendMetrics.ToDictionary(kvp => kvp.Key, kvp => new BackendPerformanceStats
            {
                SampleCount = 0, // This would need to be populated from actual data
                AverageExecutionTimeMs = kvp.Value.ExecutionTimeMs,
                MinExecutionTimeMs = 0,
                MaxExecutionTimeMs = 0,
                ExecutionTimeStdDev = 0,
                AverageMemoryUsage = kvp.Value.MemoryUsageBytes,
                ReliabilityScore = 1.0f,
                LastUpdated = DateTime.UtcNow
            }),
            TimeWindow = timeWindow ?? TimeSpan.FromHours(24),
            Summary = string.Join("; ", recommendations)
        };
    }

    /// <summary>
    /// Analyzes memory usage patterns for a kernel.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <returns>Memory usage analysis.</returns>
    public async Task<MemoryUsageAnalysis> AnalyzeMemoryUsageAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        await Task.CompletedTask; // Make async for consistency

        var relevantExecutions = _executionHistory
            .Where(e => e.KernelName == kernelName && e.MemoryUsed > 0)
            .ToList();

        if (relevantExecutions.Count == 0)
        {
            return new MemoryUsageAnalysis
            {
                KernelName = kernelName,
                AverageMemoryUsage = 0,
                PeakMemoryUsage = 0,
                MinMemoryUsage = 0,
                TotalMemoryAllocated = 0,
                AnalysisTime = DateTime.UtcNow
            };
        }

        var memoryUsages = relevantExecutions.Select(e => e.MemoryUsed).ToArray();
        var average = memoryUsages.Average();
        var peak = memoryUsages.Max();
        var minimum = memoryUsages.Min();

        // Calculate efficiency score (lower variance is better)
        var variance = memoryUsages.Select(m => Math.Pow(m - average, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);
        var coefficientOfVariation = average > 0 ? standardDeviation / average : 0;
        var efficiencyScore = Math.Max(0, 1.0 - coefficientOfVariation);

        return new MemoryUsageAnalysis
        {
            KernelName = kernelName,
            AverageMemoryUsage = average,
            PeakMemoryUsage = peak,
            MinMemoryUsage = minimum,
            TotalMemoryAllocated = memoryUsages.Sum(),
            AnalysisTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Detects performance bottlenecks in kernel execution.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <returns>Bottleneck analysis results.</returns>
    public async Task<BottleneckAnalysis> DetectBottlenecksAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        await Task.CompletedTask; // Make async for consistency

        var bottlenecks = new List<Bottleneck>();
        var relevantExecutions = _executionHistory
            .Where(e => e.KernelName == kernelName)
            .ToList();

        if (relevantExecutions.Count < 5) // Need sufficient data
        {
            return new BottleneckAnalysis
            {
                KernelName = kernelName,
                Bottlenecks = bottlenecks,
                AnalysisTime = DateTime.UtcNow
            };
        }

        // Analyze execution time patterns
        var executionTimes = relevantExecutions.Select(e => e.ExecutionTime.TotalMilliseconds).ToArray();
        var avgExecutionTime = executionTimes.Average();
        var maxExecutionTime = executionTimes.Max();

        if (maxExecutionTime > avgExecutionTime * 2)
        {
            bottlenecks.Add(new Bottleneck
            {
                Type = AbstractionsMemory.Types.BottleneckType.Compute,
                Severity = BottleneckSeverity.High,
                Description = $"Execution time varies significantly (max: {maxExecutionTime:F2}ms, avg: {avgExecutionTime:F2}ms)",
                Impact = $"Affects kernel execution performance",
                Recommendation = "Profile kernel for hot paths, consider algorithmic optimizations"
            });
        }

        // Analyze memory usage patterns
        var memoryUsages = relevantExecutions.Where(e => e.MemoryUsed > 0).Select(e => e.MemoryUsed).ToArray();
        if (memoryUsages.Length > 0)
        {
            var avgMemory = memoryUsages.Average();
            var maxMemory = memoryUsages.Max();

            if (maxMemory > avgMemory * 3)
            {
                bottlenecks.Add(new Bottleneck
                {
                    Type = AbstractionsMemory.Types.BottleneckType.Memory,
                    Severity = BottleneckSeverity.Medium,
                    Description = $"Memory usage varies significantly (max: {maxMemory:N0} bytes, avg: {avgMemory:N0} bytes)",
                    Impact = $"Affects memory allocation efficiency",
                    Recommendation = "Implement memory pooling, review allocation patterns"
                });
            }
        }

        // Calculate overall performance score
        var performanceScore = CalculatePerformanceScore(relevantExecutions, bottlenecks);

        return new BottleneckAnalysis
        {
            KernelName = kernelName,
            Bottlenecks = bottlenecks,
            AnalysisTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets historical execution statistics for a kernel.
    /// </summary>
    /// <param name="kernelName">Name of the kernel.</param>
    /// <param name="timeWindow">Time window for analysis.</param>
    /// <returns>Execution statistics.</returns>
    public DotCompute.Abstractions.Debugging.ExecutionStatistics GetExecutionStatistics(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        var cutoffTime = DateTime.UtcNow - (timeWindow ?? TimeSpan.FromHours(24));
        var relevantExecutions = _executionHistory
            .Where(e => e.KernelName == kernelName)
            .ToList();

        if (relevantExecutions.Count == 0)
        {
            return new DotCompute.Abstractions.Debugging.ExecutionStatistics
            {
                KernelName = kernelName,
                TotalExecutions = 0,
                SuccessfulExecutions = 0,
                FailedExecutions = 0,
                SuccessRate = 0
            };
        }

        var successful = relevantExecutions.Count(e => e.Success);
        var failed = relevantExecutions.Count - successful;
        var successRate = (double)successful / relevantExecutions.Count;

        var executionTimes = relevantExecutions
            .Where(e => e.Success && e.ExecutionTime > TimeSpan.Zero)
            .Select(e => e.ExecutionTime.TotalMilliseconds)
            .ToArray();

        return new ExecutionStatistics
        {
            KernelName = kernelName,
            TotalExecutions = relevantExecutions.Count,
            SuccessfulExecutions = successful,
            FailedExecutions = failed,
            SuccessRate = successRate,
            AverageExecutionTimeMs = executionTimes.Length > 0 ? executionTimes.Average() : 0,
            AverageExecutionTime = executionTimes.Length > 0 ? TimeSpan.FromMilliseconds(executionTimes.Average()) : TimeSpan.Zero
        };
    }

    /// <summary>
    /// Clears execution history older than the specified age.
    /// </summary>
    /// <param name="maxAge">Maximum age of execution records to keep.</param>
    public void ClearOldExecutionHistory(TimeSpan maxAge)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Note: ConcurrentQueue doesn't support removal of specific items
        // In a real implementation, we might use a different data structure
        // or implement a background cleanup service
        _logger.LogDebug("Cleanup of execution history requested (age > {MaxAge})", maxAge);
    }

    /// <summary>
    /// Executes kernel with detailed tracing.
    /// </summary>
    private async Task<object?> ExecuteKernelWithTracingAsync(
        string kernelName,
        object[] inputs,
        IAccelerator accelerator,
        List<TracePoint> tracePoints,
        string[] requestedTracePoints)
    {
        var startTime = Stopwatch.GetTimestamp();

        try
        {
            // Add trace points during execution
            foreach (var tracePointName in requestedTracePoints)
            {
                var elapsed = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - startTime);
                tracePoints.Add(new TracePoint
                {
                    Name = tracePointName,
                    Timestamp = DateTime.UtcNow.Subtract(elapsed),
                    MemoryUsage = GetCurrentMemoryUsage(),
                    Data = new Dictionary<string, object>
                    {
                        ["Stage"] = "Execution",
                        ["InputsProcessed"] = inputs.Length
                    }
                });
            }

            // TODO: Implement actual kernel execution
            await Task.Delay(10); // Simulate execution
            return new { KernelName = kernelName, Success = true };
        }
        catch (Exception ex)
        {
            var elapsed = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - startTime);
            tracePoints.Add(new TracePoint
            {
                Name = "Error",
                Timestamp = DateTime.UtcNow.Subtract(elapsed),
                MemoryUsage = GetCurrentMemoryUsage(),
                Data = new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["Stage"] = "Execution"
                }
            });
            throw;
        }
    }

    /// <summary>
    /// Gets current memory usage.
    /// </summary>
    private static long GetCurrentMemoryUsage()
    {
        try
        {
            return GC.GetTotalMemory(false);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Creates memory profile from trace points.
    /// </summary>
    private static MemoryProfile CreateMemoryProfile(List<TracePoint> tracePoints)
    {
        var memoryReadings = tracePoints.Select(tp => tp.MemoryUsage).ToArray();

        return new MemoryProfile
        {
            InitialMemory = memoryReadings.FirstOrDefault(),
            PeakMemory = memoryReadings.Max(),
            FinalMemory = memoryReadings.LastOrDefault(),
            AverageMemory = (long)memoryReadings.Average(),
            MemoryGrowth = memoryReadings.LastOrDefault() - memoryReadings.FirstOrDefault()
        };
    }

    /// <summary>
    /// Creates performance metrics from trace points.
    /// </summary>
    private static PerformanceMetrics CreatePerformanceMetrics(List<TracePoint> tracePoints, TimeSpan totalTime)
    {
        return new PerformanceMetrics
        {
            ExecutionTimeMs = (long)totalTime.TotalMilliseconds,
            OperationsPerSecond = totalTime.TotalSeconds > 0 ? (long)(1.0 / totalTime.TotalSeconds) : 0,
            MemoryUsageBytes = tracePoints.LastOrDefault()?.MemoryUsage ?? 0
        };
    }

    /// <summary>
    /// Converts Core MemoryProfile to Abstractions MemoryProfile.
    /// </summary>
    private static DotCompute.Abstractions.Debugging.MemoryProfile ConvertToAbstractionsMemoryProfile(MemoryProfile coreProfile)
    {
        return new DotCompute.Abstractions.Debugging.MemoryProfile
        {
            PeakMemory = coreProfile.PeakMemory,
            Allocations = 0, // Not tracked in core profile
            Efficiency = coreProfile.MemoryGrowth > 0 ? Math.Min(1.0f, (float)coreProfile.InitialMemory / coreProfile.PeakMemory) : 1.0f
        };
    }

    /// <summary>
    /// Stores execution result for historical analysis.
    /// </summary>
    private void StoreExecutionResult(KernelExecutionResult result)
    {
        _executionHistory.Enqueue(result);

        // Keep history size manageable
        if (_executionHistory.Count > 10000)
        {
            for (var i = 0; i < 1000; i++)
            {
                _ = _executionHistory.TryDequeue(out _);
            }
        }
    }

    /// <summary>
    /// Analyzes a group of executions for a specific backend.
    /// </summary>
    private static PerformanceMetrics AnalyzeExecutionGroup(List<KernelExecutionResult> executions)
    {
        var executionTimes = executions.Select(e => e.ExecutionTime.TotalMilliseconds).ToArray();
        var memoryUsages = executions.Where(e => e.MemoryUsed > 0).Select(e => e.MemoryUsed).ToArray();

        return new PerformanceMetrics
        {
            ExecutionTimeMs = (long)executionTimes.Average(),
            OperationsPerSecond = (long)(executionTimes.Average() > 0 ? 1000.0 / executionTimes.Average() : 0),
            MemoryUsageBytes = memoryUsages.Length > 0 ? (long)memoryUsages.Average() : 0
        };
    }

    /// <summary>
    /// Generates backend-specific recommendations.
    /// </summary>
    private static List<string> GenerateBackendRecommendations(string backend, PerformanceMetrics metrics, List<KernelExecutionResult> executions)
    {
        var recommendations = new List<string>();

        var avgTime = metrics.ExecutionTimeMs;
        if (avgTime > 100) // > 100ms
        {
            recommendations.Add($"{backend}: Consider optimizing for faster execution (current avg: {avgTime:F2}ms)");
        }

        var successRate = (double)executions.Count(e => e.Success) / executions.Count;
        if (successRate < 0.95)
        {
            recommendations.Add($"{backend}: Improve reliability (success rate: {successRate:P1})");
        }

        return recommendations;
    }

    /// <summary>
    /// Generates cross-backend recommendations.
    /// </summary>
    private static List<string> GenerateCrossBackendRecommendations(Dictionary<string, PerformanceMetrics> backendMetrics)
    {
        var recommendations = new List<string>();

        if (backendMetrics.Count > 1)
        {
            var fastest = backendMetrics.OrderBy(kvp => kvp.Value.ExecutionTimeMs).First();
            recommendations.Add($"Consider using {fastest.Key} backend for best performance");
        }

        return recommendations;
    }

    /// <summary>
    /// Calculates recommended memory allocation.
    /// </summary>
    private static long CalculateRecommendedMemoryAllocation(long[] memoryUsages)
    {
        if (memoryUsages.Length == 0)
        {

            return 0;
        }

        // Recommend 95th percentile with some buffer

        Array.Sort(memoryUsages);
        var percentile95Index = (int)(memoryUsages.Length * 0.95);
        var percentile95 = memoryUsages[Math.Min(percentile95Index, memoryUsages.Length - 1)];

        return (long)(percentile95 * 1.2); // 20% buffer
    }

    /// <summary>
    /// Calculates overall performance score.
    /// </summary>
    private static double CalculatePerformanceScore(List<KernelExecutionResult> executions, List<Bottleneck> bottlenecks)
    {
        var baseScore = 1.0;

        // Reduce score based on success rate
        var successRate = (double)executions.Count(e => e.Success) / executions.Count;
        baseScore *= successRate;

        // Reduce score based on bottleneck severity
        foreach (var bottleneck in bottlenecks)
        {
            var reduction = bottleneck.Severity switch
            {
                BottleneckSeverity.Low => 0.05,
                BottleneckSeverity.Medium => 0.15,
                BottleneckSeverity.High => 0.30,
                BottleneckSeverity.Critical => 0.50,
                _ => 0
            };
            baseScore *= (1.0 - reduction);
        }

        return Math.Max(0, Math.Min(1.0, baseScore));
    }

    /// <summary>
    /// Generates optimization recommendations based on bottlenecks.
    /// </summary>
    private static List<string> GenerateOptimizationRecommendations(List<Bottleneck> bottlenecks)
    {
        var recommendations = new List<string>();

        foreach (var bottleneck in bottlenecks)
        {
            recommendations.Add(bottleneck.Recommendation);
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("No specific optimizations recommended - performance appears acceptable");
        }

        return recommendations.Distinct().ToList();
    }

    /// <summary>
    /// Calculates standard deviation of execution times.
    /// </summary>
    private static double CalculateStandardDeviation(double[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }


        var average = values.Average();
        var variance = values.Select(v => Math.Pow(v - average, 2)).Average();
        return Math.Sqrt(variance);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Clear collections
            while (_executionHistory.TryDequeue(out _)) { }
            _performanceProfiles.Clear();
        }
    }
}

// Supporting data structures for profiling
public record MemoryProfile
{
    public long InitialMemory { get; init; }
    public long PeakMemory { get; init; }
    public long FinalMemory { get; init; }
    public long AverageMemory { get; init; }
    public long MemoryGrowth { get; init; }
}

public record PerformanceProfile
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public PerformanceMetrics Metrics { get; init; } = new();
    public DateTime LastUpdated { get; init; }
    public int SampleCount { get; init; }
}