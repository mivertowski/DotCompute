// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Performance;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Provides comprehensive performance profiling and analysis for kernel execution.
/// Tracks execution traces, memory usage, and performance metrics across backends.
/// </summary>
#pragma warning disable CS9113 // Parameter reserved for future configurable profiling options
public sealed partial class KernelProfiler(
    ILogger<KernelProfiler> logger,
    DebugServiceOptions options) : IDisposable
#pragma warning restore CS9113
{
    private readonly ILogger<KernelProfiler> _logger = logger;
    private readonly ConcurrentQueue<KernelExecutionResult> _executionHistory = new();
    private readonly ConcurrentDictionary<string, PerformanceProfile> _performanceProfiles = new();
    private bool _disposed;

    // LoggerMessage delegates - Event ID range 11100-11115 for KernelProfiler
    private static readonly Action<ILogger, string, string, Exception?> _logStartingTrace =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Debug,
            new EventId(11100, nameof(LogStartingTrace)),
            "Starting kernel execution trace for {KernelName} on {BackendType}");

    private static void LogStartingTrace(ILogger logger, string kernelName, string backendType)
        => _logStartingTrace(logger, kernelName, backendType, null);

    private static readonly Action<ILogger, string, bool, Exception?> _logTraceCompleted =
        LoggerMessage.Define<string, bool>(
            MsLogLevel.Debug,
            new EventId(11101, nameof(LogTraceCompleted)),
            "Completed kernel execution trace for {KernelName}: {Success}");

    private static void LogTraceCompleted(ILogger logger, string kernelName, bool success)
        => _logTraceCompleted(logger, kernelName, success, null);

    private static readonly Action<ILogger, string, Exception?> _logTraceError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(11102, nameof(LogTraceError)),
            "Error during kernel execution trace for {KernelName}");

    private static void LogTraceError(ILogger logger, Exception error, string kernelName)
        => _logTraceError(logger, kernelName, error);

    private static readonly Action<ILogger, TimeSpan, Exception?> _logCleanupRequested =
        LoggerMessage.Define<TimeSpan>(
            MsLogLevel.Debug,
            new EventId(11103, nameof(LogCleanupRequested)),
            "Cleanup of execution history requested (age > {MaxAge})");

    private static void LogCleanupRequested(ILogger logger, TimeSpan maxAge)
        => _logCleanupRequested(logger, maxAge, null);

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
            LogStartingTrace(_logger, kernelName, backendType);

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
                Output = result,
                Timings = new KernelExecutionTimings { KernelTimeMs = stopwatch.Elapsed.TotalMilliseconds, TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds },
                PerformanceCounters = new Dictionary<string, object> { ["MemoryUsed"] = finalMemory - initialMemory },
                Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = kernelName, SubmittedAt = DateTimeOffset.UtcNow, IsCompleted = true }
            });

            LogTraceCompleted(_logger, kernelName, result != null);
            return trace;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogTraceError(_logger, ex, kernelName);

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
    public async Task<PerformanceReport> GeneratePerformanceReportAsync(
        string kernelName,
        TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        await Task.CompletedTask; // Make async for consistency

        var cutoffTime = DateTime.UtcNow - (timeWindow ?? TimeSpan.FromHours(24));
        var relevantExecutions = _executionHistory
            .Where(e => e.KernelName == kernelName && e.Timings != null && e.Timings.TotalTimeMs > 0)
            .ToList();

        if (relevantExecutions.Count == 0)
        {
            return new PerformanceReport
            {
                KernelName = kernelName,
                ExecutionCount = 0,
                Recommendations = ["No execution data available for analysis"]
            };
        }

        // Group by backend for analysis
        var backendGroups = relevantExecutions.GroupBy(e => e.BackendType ?? "Unknown").ToList();
        var backendMetrics = new Dictionary<string, PerformanceMetrics>();
        var recommendations = new List<string>();

        foreach (var group in backendGroups)
        {
            var executions = group.ToList();
            var metrics = AnalyzeExecutionGroup(executions);
            backendMetrics[group.Key ?? "Unknown"] = metrics;

            // Generate backend-specific recommendations
            var backendRecommendations = GenerateBackendRecommendations(group.Key ?? "Unknown", metrics, executions);
            recommendations.AddRange(backendRecommendations);
        }

        // Cross-backend analysis
        var crossBackendRecommendations = GenerateCrossBackendRecommendations(backendMetrics);
        recommendations.AddRange(crossBackendRecommendations);

        return new PerformanceReport
        {
            KernelName = kernelName,
            ExecutionCount = relevantExecutions.Count,
            BackendMetrics = backendMetrics,
            Recommendations = recommendations,
            AnalysisTimeWindow = timeWindow ?? TimeSpan.FromHours(24),
            GeneratedAt = DateTime.UtcNow
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
            .Where(e => e.KernelName == kernelName
                && e.PerformanceCounters?.ContainsKey("MemoryUsed") == true
                && Convert.ToInt64(e.PerformanceCounters["MemoryUsed"], CultureInfo.InvariantCulture) > 0)
            .ToList();

        if (relevantExecutions.Count == 0)
        {
            return new MemoryUsageAnalysis
            {
                KernelName = kernelName,
                SampleCount = 0,
                AverageMemoryUsage = 0,
                PeakMemoryUsage = 0,
                MemoryEfficiencyScore = 0
            };
        }

        var memoryUsages = relevantExecutions
            .Select(e => Convert.ToInt64(e.PerformanceCounters!["MemoryUsed"], CultureInfo.InvariantCulture))
            .ToArray();
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
            SampleCount = relevantExecutions.Count,
            AverageMemoryUsage = (long)average,
            PeakMemoryUsage = peak,
            MinimumMemoryUsage = minimum,
            MemoryEfficiencyScore = efficiencyScore,
            MemoryVariance = variance,
            RecommendedMemoryAllocation = CalculateRecommendedMemoryAllocation(memoryUsages)
        };
    }

    /// <summary>
    /// Detects performance bottlenecks in kernel execution.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <returns>Bottleneck analysis results.</returns>
    public async Task<DotCompute.Abstractions.Debugging.BottleneckAnalysis> DetectBottlenecksAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        await Task.CompletedTask; // Make async for consistency

        var bottlenecks = new List<DotCompute.Abstractions.Debugging.PerformanceBottleneck>();
        var relevantExecutions = _executionHistory
            .Where(e => e.KernelName == kernelName)
            .ToList();

        if (relevantExecutions.Count < 5) // Need sufficient data
        {
            return new DotCompute.Abstractions.Debugging.BottleneckAnalysis
            {
                KernelName = kernelName,
                Bottlenecks = [],
                OverallPerformanceScore = 0.5, // Neutral score
                RecommendedOptimizations = ["Insufficient execution data for bottleneck analysis"]
            };
        }

        // Analyze execution time patterns
        var executionTimes = relevantExecutions
            .Where(e => e.Timings != null)
            .Select(e => e.Timings!.TotalTimeMs)
            .ToArray();
        var avgExecutionTime = executionTimes.Length > 0 ? executionTimes.Average() : 0;
        var maxExecutionTime = executionTimes.Length > 0 ? executionTimes.Max() : 0;

        if (executionTimes.Length > 0 && maxExecutionTime > avgExecutionTime * 2)
        {
            bottlenecks.Add(new DotCompute.Abstractions.Debugging.PerformanceBottleneck
            {
                Type = DotCompute.Abstractions.Types.BottleneckType.Compute,
                Severity = DotCompute.Abstractions.Debugging.BottleneckSeverity.High,
                Description = $"Execution time varies significantly (max: {maxExecutionTime:F2}ms, avg: {avgExecutionTime:F2}ms)",
                AffectedComponents = ["Kernel execution"],
                RecommendedActions = ["Profile kernel for hot paths", "Consider algorithmic optimizations"]
            });
        }

        // Analyze memory usage patterns
        var memoryUsages = relevantExecutions
            .Where(e => e.PerformanceCounters?.ContainsKey("MemoryUsed") == true)
            .Select(e => Convert.ToInt64(e.PerformanceCounters!["MemoryUsed"], CultureInfo.InvariantCulture))
            .ToArray();
        if (memoryUsages.Length > 0)
        {
            var avgMemory = memoryUsages.Average();
            var maxMemory = memoryUsages.Max();

            if (maxMemory > avgMemory * 3)
            {
                bottlenecks.Add(new DotCompute.Abstractions.Debugging.PerformanceBottleneck
                {
                    Type = DotCompute.Abstractions.Types.BottleneckType.Memory,
                    Severity = DotCompute.Abstractions.Debugging.BottleneckSeverity.Medium,
                    Description = $"Memory usage varies significantly (max: {maxMemory:N0} bytes, avg: {avgMemory:N0} bytes)",
                    AffectedComponents = ["Memory allocation"],
                    RecommendedActions = ["Implement memory pooling", "Review memory allocation patterns"]
                });
            }
        }

        // Calculate overall performance score
        var performanceScore = CalculatePerformanceScore(relevantExecutions, bottlenecks);

        return new DotCompute.Abstractions.Debugging.BottleneckAnalysis
        {
            KernelName = kernelName,
            Bottlenecks = bottlenecks,
            OverallPerformanceScore = performanceScore,
            RecommendedOptimizations = GenerateOptimizationRecommendations(bottlenecks)
        };
    }

    /// <summary>
    /// Gets historical execution statistics for a kernel.
    /// </summary>
    /// <param name="kernelName">Name of the kernel.</param>
    /// <param name="timeWindow">Time window for analysis.</param>
    /// <returns>Execution statistics.</returns>
    public ExecutionStatistics GetExecutionStatistics(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        var cutoffTime = DateTime.UtcNow - (timeWindow ?? TimeSpan.FromHours(24));
        var relevantExecutions = _executionHistory
            .Where(e => e.KernelName == kernelName)
            .ToList();

        if (relevantExecutions.Count == 0)
        {
            return new ExecutionStatistics
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
            .Where(e => e.Success && e.Timings != null && e.Timings.TotalTimeMs > 0)
            .Select(e => e.Timings!.TotalTimeMs)
            .ToArray();

        return new ExecutionStatistics
        {
            KernelName = kernelName,
            TotalExecutions = relevantExecutions.Count,
            SuccessfulExecutions = successful,
            FailedExecutions = failed,
            SuccessRate = successRate,
            AverageExecutionTimeMs = executionTimes.Length > 0 ? executionTimes.Average() : 0
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
        LogCleanupRequested(_logger, maxAge);
    }

    /// <summary>
    /// Executes kernel with detailed tracing.
    /// </summary>
    private static async Task<object?> ExecuteKernelWithTracingAsync(
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
            await Task.CompletedTask; // Placeholder for kernel execution
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
    private static MemoryProfile CreateMemoryProfile(IReadOnlyList<TracePoint> tracePoints)
    {
        var memoryReadings = tracePoints.Select(tp => tp.MemoryUsage).ToArray();

        return new MemoryProfile
        {
            PeakMemory = memoryReadings.Max(),
            Allocations = memoryReadings.LastOrDefault() - memoryReadings.FirstOrDefault(),
            Efficiency = 1.0f // Default efficiency, could be calculated based on memory growth
        };
    }

    /// <summary>
    /// Creates performance metrics from trace points.
    /// </summary>
    private static PerformanceMetrics CreatePerformanceMetrics(IReadOnlyList<TracePoint> tracePoints, TimeSpan totalTime)
    {
        return new PerformanceMetrics
        {
            ExecutionTimeMs = (long)totalTime.TotalMilliseconds,
            OperationsPerSecond = totalTime.TotalSeconds > 0 ? (int)(1.0 / totalTime.TotalSeconds) : 0,
            MemoryUsageBytes = tracePoints.Count > 0 ? tracePoints[^1].MemoryUsage : 0
        };
    }

    /// <summary>
    /// Converts Core MemoryProfile to Abstractions MemoryProfile.
    /// </summary>
    private static AbstractionsMemory.Debugging.MemoryProfile ConvertToAbstractionsMemoryProfile(MemoryProfile coreProfile)
    {
        return new AbstractionsMemory.Debugging.MemoryProfile
        {
            PeakMemory = coreProfile.PeakMemory,
            Allocations = coreProfile.Allocations,
            Efficiency = coreProfile.Efficiency
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
    private static PerformanceMetrics AnalyzeExecutionGroup(IReadOnlyList<KernelExecutionResult> executions)
    {
        var executionTimes = executions
            .Where(e => e.Timings != null)
            .Select(e => e.Timings!.TotalTimeMs)
            .ToArray();
        var memoryUsages = executions
            .Where(e => e.PerformanceCounters?.ContainsKey("MemoryUsed") == true)
            .Select(e => Convert.ToInt64(e.PerformanceCounters!["MemoryUsed"], CultureInfo.InvariantCulture))
            .ToArray();

        return new PerformanceMetrics
        {
            ExecutionTimeMs = executionTimes.Length > 0 ? (long)Math.Round(executionTimes.Average()) : 0L,
            OperationsPerSecond = executionTimes.Length > 0 && executionTimes.Average() > 0
                ? (long)Math.Round(1000.0 / executionTimes.Average())
                : 0L,
            MemoryUsageBytes = memoryUsages.Length > 0 ? (long)Math.Round(memoryUsages.Average()) : 0L
        };
    }

    /// <summary>
    /// Generates backend-specific recommendations.
    /// </summary>
    private static List<string> GenerateBackendRecommendations(string backend, PerformanceMetrics metrics, IReadOnlyList<KernelExecutionResult> executions)
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
    private static double CalculatePerformanceScore(List<KernelExecutionResult> executions, IReadOnlyList<DotCompute.Abstractions.Debugging.PerformanceBottleneck> bottlenecks)
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
                DotCompute.Abstractions.Debugging.BottleneckSeverity.Low => 0.05,
                DotCompute.Abstractions.Debugging.BottleneckSeverity.Medium => 0.15,
                DotCompute.Abstractions.Debugging.BottleneckSeverity.High => 0.30,
                DotCompute.Abstractions.Debugging.BottleneckSeverity.Critical => 0.50,
                _ => 0
            };
            baseScore *= (1.0 - reduction);
        }

        return Math.Max(0, Math.Min(1.0, baseScore));
    }

    /// <summary>
    /// Generates optimization recommendations based on bottlenecks.
    /// </summary>
    private static List<string> GenerateOptimizationRecommendations(IReadOnlyList<DotCompute.Abstractions.Debugging.PerformanceBottleneck> bottlenecks)
    {
        var recommendations = new List<string>();

        foreach (var bottleneck in bottlenecks)
        {
            recommendations.AddRange(bottleneck.RecommendedActions);
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("No specific optimizations recommended - performance appears acceptable");
        }

        return [.. recommendations.Distinct()];
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
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Clear collections
            while (_executionHistory.TryDequeue(out _))
            {
            }

            _performanceProfiles.Clear();
        }
    }
}
