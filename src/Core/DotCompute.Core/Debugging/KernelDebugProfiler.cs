// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Handles performance profiling and execution tracing for kernel debugging.
/// Provides detailed performance metrics and execution traces.
/// </summary>
public sealed class KernelDebugProfiler : IDisposable
{
    private readonly ILogger<KernelDebugProfiler> _logger;
    private readonly ConcurrentQueue<KernelExecutionResult> _executionHistory;
    private DebugServiceOptions _options;
    private bool _disposed;

    public KernelDebugProfiler(
        ILogger<KernelDebugProfiler> logger,
        ConcurrentQueue<KernelExecutionResult> executionHistory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executionHistory = executionHistory ?? throw new ArgumentNullException(nameof(executionHistory));
        _options = new DebugServiceOptions();
    }

    public void Configure(DebugServiceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<KernelExecutionResult> ExecuteWithProfilingAsync(
        string kernelName,
        string backendType,
        object[] inputs,
        IAccelerator accelerator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentException.ThrowIfNullOrEmpty(backendType);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(accelerator);

        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);

        try
        {
            _logger.LogDebug("Executing kernel {kernelName} on {backendType} with profiling", kernelName, backendType);

            // TODO: Replace with actual kernel execution
            // This is a placeholder - the actual implementation would use the accelerator
            // to compile and execute the kernel
            await Task.Delay(10); // Simulate execution time

            var result = new object(); // Placeholder result
            var success = true;

            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);

            var executionResult = new KernelExecutionResult
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = success,
                Result = result,
                ExecutionTime = stopwatch.Elapsed,
                MemoryUsage = Math.Max(0, memoryAfter - memoryBefore),
                PerformanceMetrics = new Dictionary<string, object>
                {
                    ["TotalTime"] = stopwatch.Elapsed.TotalMilliseconds,
                    ["MemoryDelta"] = memoryAfter - memoryBefore,
                    ["InputSize"] = CalculateInputSize(inputs),
                    ["Timestamp"] = DateTimeOffset.UtcNow
                }
            };

            // Store in execution history
            _executionHistory.Enqueue(executionResult);

            // Maintain history size limit
            while (_executionHistory.Count > _options.MaxExecutionHistorySize)
            {
                _executionHistory.TryDequeue(out _);
            }

            return executionResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorMessage(ex, "Error during profiled execution of {kernelName} on {backendType}", kernelName, backendType);

            var failedResult = new KernelExecutionResult
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTime = stopwatch.Elapsed,
                MemoryUsage = GC.GetTotalMemory(false) - memoryBefore
            };

            _executionHistory.Enqueue(failedResult);
            return failedResult;
        }
    }

    public async Task<KernelExecutionTrace> TraceKernelExecutionAsync(
        string kernelName,
        string backendType,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentException.ThrowIfNullOrEmpty(backendType);
        ArgumentNullException.ThrowIfNull(inputs);

        _logger.LogInformation("Tracing execution for kernel {kernelName} on {backendType}", kernelName, backendType);

        var trace = new KernelExecutionTrace
        {
            KernelName = kernelName,
            BackendType = backendType,
            StartTime = DateTimeOffset.UtcNow,
            Steps = new List<ExecutionStep>(),
            PerformanceCounters = new Dictionary<string, long>(),
            ResourceUsage = new Dictionary<string, object>()
        };

        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            // Phase 1: Preparation
            var prepStopwatch = Stopwatch.StartNew();
            trace.Steps.Add(new ExecutionStep
            {
                StepName = "Preparation",
                StartTime = DateTimeOffset.UtcNow,
                Description = "Preparing kernel execution environment"
            });

            // Simulate preparation work
            await Task.Delay(5);

            prepStopwatch.Stop();
            trace.Steps.Last().EndTime = DateTimeOffset.UtcNow;
            trace.Steps.Last().Duration = prepStopwatch.Elapsed;

            // Phase 2: Compilation
            var compileStopwatch = Stopwatch.StartNew();
            trace.Steps.Add(new ExecutionStep
            {
                StepName = "Compilation",
                StartTime = DateTimeOffset.UtcNow,
                Description = "Compiling kernel for target backend"
            });

            // Simulate compilation
            await Task.Delay(20);

            compileStopwatch.Stop();
            trace.Steps.Last().EndTime = DateTimeOffset.UtcNow;
            trace.Steps.Last().Duration = compileStopwatch.Elapsed;

            // Phase 3: Execution
            var execStopwatch = Stopwatch.StartNew();
            trace.Steps.Add(new ExecutionStep
            {
                StepName = "Execution",
                StartTime = DateTimeOffset.UtcNow,
                Description = "Executing kernel on target backend"
            });

            // Simulate actual execution
            await Task.Delay(50);

            execStopwatch.Stop();
            trace.Steps.Last().EndTime = DateTimeOffset.UtcNow;
            trace.Steps.Last().Duration = execStopwatch.Elapsed;

            // Phase 4: Cleanup
            var cleanupStopwatch = Stopwatch.StartNew();
            trace.Steps.Add(new ExecutionStep
            {
                StepName = "Cleanup",
                StartTime = DateTimeOffset.UtcNow,
                Description = "Cleaning up execution resources"
            });

            // Simulate cleanup
            await Task.Delay(2);

            cleanupStopwatch.Stop();
            trace.Steps.Last().EndTime = DateTimeOffset.UtcNow;
            trace.Steps.Last().Duration = cleanupStopwatch.Elapsed;

            overallStopwatch.Stop();

            // Populate performance counters
            trace.PerformanceCounters["TotalExecutionTime"] = overallStopwatch.ElapsedMilliseconds;
            trace.PerformanceCounters["PreparationTime"] = (long)prepStopwatch.Elapsed.TotalMilliseconds;
            trace.PerformanceCounters["CompilationTime"] = (long)compileStopwatch.Elapsed.TotalMilliseconds;
            trace.PerformanceCounters["KernelExecutionTime"] = (long)execStopwatch.Elapsed.TotalMilliseconds;
            trace.PerformanceCounters["CleanupTime"] = (long)cleanupStopwatch.Elapsed.TotalMilliseconds;

            // Populate resource usage
            trace.ResourceUsage["PeakMemoryUsage"] = GC.GetTotalMemory(false);
            trace.ResourceUsage["InputDataSize"] = CalculateInputSize(inputs);
            trace.ResourceUsage["ThreadCount"] = Environment.ProcessorCount; // Placeholder

            trace.EndTime = DateTimeOffset.UtcNow;
            trace.TotalDuration = overallStopwatch.Elapsed;
            trace.Success = true;

            return trace;
        }
        catch (Exception ex)
        {
            overallStopwatch.Stop();
            _logger.LogErrorMessage(ex, "Error during execution tracing");

            trace.Success = false;
            trace.ErrorMessage = ex.Message;
            trace.EndTime = DateTimeOffset.UtcNow;
            trace.TotalDuration = overallStopwatch.Elapsed;

            return trace;
        }
    }

    public async Task<PerformanceReport> GeneratePerformanceReportAsync(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        var window = timeWindow ?? TimeSpan.FromHours(1);
        var cutoffTime = DateTimeOffset.UtcNow - window;

        var relevantResults = _executionHistory
            .Where(r => r.KernelName == kernelName &&
                       r.PerformanceMetrics != null &&
                       r.PerformanceMetrics.TryGetValue("Timestamp", out var timestamp) &&
                       timestamp is DateTimeOffset dt && dt >= cutoffTime)
            .ToList();

        if (!relevantResults.Any())
        {
            return new PerformanceReport
            {
                KernelName = kernelName,
                TimeWindow = window,
                ExecutionCount = 0,
                Backends = [],
                Summary = "No execution data available for the specified time window"
            };
        }

        var backendGroups = relevantResults.GroupBy(r => r.BackendType).ToList();
        var backendStats = new Dictionary<string, BackendPerformanceStats>();

        foreach (var group in backendGroups)
        {
            var execTimes = group.Select(r => r.ExecutionTime.TotalMilliseconds).ToArray();
            var memoryUsages = group.Select(r => r.MemoryUsage).Where(m => m > 0).ToArray();

            backendStats[group.Key] = new BackendPerformanceStats
            {
                ExecutionCount = group.Count(),
                AverageExecutionTime = execTimes.Average(),
                MinExecutionTime = execTimes.Min(),
                MaxExecutionTime = execTimes.Max(),
                StandardDeviation = CalculateStandardDeviation(execTimes),
                AverageMemoryUsage = memoryUsages.Any() ? memoryUsages.Average() : 0,
                SuccessRate = group.Count(r => r.Success) / (double)group.Count()
            };
        }

        var overallStats = new OverallPerformanceStats
        {
            TotalExecutions = relevantResults.Count,
            SuccessfulExecutions = relevantResults.Count(r => r.Success),
            FailedExecutions = relevantResults.Count(r => !r.Success),
            AverageExecutionTime = relevantResults.Where(r => r.Success).Select(r => r.ExecutionTime.TotalMilliseconds).DefaultIfEmpty(0).Average(),
            TotalMemoryUsed = relevantResults.Sum(r => r.MemoryUsage)
        };

        return new PerformanceReport
        {
            KernelName = kernelName,
            TimeWindow = window,
            ExecutionCount = relevantResults.Count,
            Backends = backendStats,
            OverallStats = overallStats,
            Summary = GeneratePerformanceSummary(backendStats, overallStats)
        };
    }

    private static long CalculateInputSize(object[] inputs)
    {
        long totalSize = 0;

        foreach (var input in inputs)
        {
            if (input == null)
            {
                continue;
            }


            totalSize += input switch
            {
                Array array => array.Length * GetElementSize(array.GetType().GetElementType()),
                string str => str.Length * sizeof(char),
                _ => GetPrimitiveSize(input.GetType())
            };
        }

        return totalSize;
    }

    private static int GetElementSize(Type? elementType)
    {
        if (elementType == null)
        {
            return 0;
        }


        return elementType.Name switch
        {
            "Byte" => sizeof(byte),
            "Int16" => sizeof(short),
            "Int32" => sizeof(int),
            "Int64" => sizeof(long),
            "Single" => sizeof(float),
            "Double" => sizeof(double),
            "Boolean" => sizeof(bool),
            "Char" => sizeof(char),
            _ => 8 // Default assumption
        };
    }

    private static int GetPrimitiveSize(Type type)
    {
        return type.Name switch
        {
            "Byte" => sizeof(byte),
            "Int16" => sizeof(short),
            "Int32" => sizeof(int),
            "Int64" => sizeof(long),
            "Single" => sizeof(float),
            "Double" => sizeof(double),
            "Boolean" => sizeof(bool),
            "Char" => sizeof(char),
            _ => IntPtr.Size // Default to pointer size
        };
    }

    private static double CalculateStandardDeviation(double[] values)
    {
        if (values.Length <= 1)
        {
            return 0;
        }


        var mean = values.Average();
        var squaredDifferences = values.Select(v => Math.Pow(v - mean, 2));
        var variance = squaredDifferences.Average();
        return Math.Sqrt(variance);
    }

    private static string GeneratePerformanceSummary(
        Dictionary<string, BackendPerformanceStats> backendStats,
        OverallPerformanceStats overallStats)
    {
        var summary = $"Overall: {overallStats.SuccessfulExecutions}/{overallStats.TotalExecutions} successful executions";

        if (backendStats.Count > 1)
        {
            var fastest = backendStats.OrderBy(kvp => kvp.Value.AverageExecutionTime).First();
            summary += $", fastest backend: {fastest.Key} ({fastest.Value.AverageExecutionTime:F2}ms avg)";
        }

        if (overallStats.FailedExecutions > 0)
        {
            var failureRate = (overallStats.FailedExecutions / (double)overallStats.TotalExecutions) * 100;
            summary += $", {failureRate:F1}% failure rate";
        }

        return summary;
    }

    /// <summary>
    /// Analyzes memory usage patterns for kernels.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <param name="timeWindow">Time window for analysis.</param>
    /// <returns>Memory usage analysis result.</returns>
    public async Task<MemoryUsageAnalysis> AnalyzeMemoryUsageAsync(string kernelName, TimeSpan timeWindow)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Task.CompletedTask; // Make async for consistency

        var cutoffTime = DateTime.UtcNow - timeWindow;
        var relevantResults = _executionHistory
            .Where(r => r.KernelName == kernelName && r.ExecutedAt >= cutoffTime)
            .ToList();

        if (!relevantResults.Any())
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

        var memoryUsages = relevantResults.Select(r => r.MemoryUsed).ToList();

        return new MemoryUsageAnalysis
        {
            KernelName = kernelName,
            AverageMemoryUsage = memoryUsages.Average(),
            PeakMemoryUsage = memoryUsages.Max(),
            MinMemoryUsage = memoryUsages.Min(),
            TotalMemoryAllocated = memoryUsages.Sum(),
            AnalysisTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Detects bottlenecks in kernel execution.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <returns>Bottleneck analysis result.</returns>
    public async Task<DotCompute.Core.Debugging.Core.BottleneckAnalysis> DetectBottlenecksAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Task.CompletedTask; // Make async for consistency

        var relevantResults = _executionHistory
            .Where(r => r.KernelName == kernelName)
            .ToList();

        var bottlenecks = new List<DotCompute.Core.Debugging.Core.Bottleneck>();

        if (relevantResults.Any())
        {
            var avgExecutionTime = relevantResults.Average(r => r.ExecutionTime.TotalMilliseconds);
            var slowExecutions = relevantResults.Where(r => r.ExecutionTime.TotalMilliseconds > avgExecutionTime * 1.5);

            if (slowExecutions.Any())
            {
                bottlenecks.Add(new DotCompute.Core.Debugging.Core.Bottleneck
                {
                    Type = DotCompute.Abstractions.Types.BottleneckType.Execution,
                    Severity = DotCompute.Core.Debugging.Core.BottleneckSeverity.Medium,
                    Description = $"Slow execution detected in {slowExecutions.Count()} runs",
                    Impact = $"{(slowExecutions.Count() / (double)relevantResults.Count * 100):F1}% of executions",
                    Recommendation = "Consider optimizing algorithm or increasing resources"
                });
            }
        }

        return new DotCompute.Core.Debugging.Core.BottleneckAnalysis
        {
            KernelName = kernelName,
            Bottlenecks = bottlenecks,
            AnalysisTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets execution statistics for a kernel.
    /// </summary>
    /// <param name="kernelName">Name of the kernel.</param>
    /// <returns>Execution statistics.</returns>
    public ExecutionStatistics GetExecutionStatistics(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var relevantResults = _executionHistory
            .Where(r => r.KernelName == kernelName)
            .ToList();

        if (!relevantResults.Any())
        {
            return new ExecutionStatistics
            {
                KernelName = kernelName,
                TotalExecutions = 0,
                SuccessfulExecutions = 0,
                FailedExecutions = 0,
                AverageExecutionTime = TimeSpan.Zero,
                LastExecutionTime = null
            };
        }

        var successfulExecutions = relevantResults.Count(r => r.Success);
        var avgTime = TimeSpan.FromMilliseconds(
            relevantResults.Average(r => r.ExecutionTime.TotalMilliseconds));

        return new ExecutionStatistics
        {
            KernelName = kernelName,
            TotalExecutions = relevantResults.Count,
            SuccessfulExecutions = successfulExecutions,
            FailedExecutions = relevantResults.Count - successfulExecutions,
            AverageExecutionTime = avgTime,
            LastExecutionTime = relevantResults.Max(r => r.ExecutedAt)
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

// Supporting classes for performance reporting
public class BackendPerformanceStats
{
    public int ExecutionCount { get; set; }
    public double AverageExecutionTime { get; set; }
    public double MinExecutionTime { get; set; }
    public double MaxExecutionTime { get; set; }
    public double StandardDeviation { get; set; }
    public double AverageMemoryUsage { get; set; }
    public double SuccessRate { get; set; }
}

public class OverallPerformanceStats
{
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double AverageExecutionTime { get; set; }
    public long TotalMemoryUsed { get; set; }
}

public class PerformanceReport
{
    public required string KernelName { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public int ExecutionCount { get; set; }
    public Dictionary<string, BackendPerformanceStats> Backends { get; set; } = new();
    public OverallPerformanceStats? OverallStats { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Memory usage analysis result.
/// </summary>
public class MemoryUsageAnalysis
{
    public string KernelName { get; set; } = string.Empty;
    public double AverageMemoryUsage { get; set; }
    public long PeakMemoryUsage { get; set; }
    public long MinMemoryUsage { get; set; }
    public long TotalMemoryAllocated { get; set; }
    public DateTime AnalysisTime { get; set; }
}

/// <summary>
/// Execution statistics for a kernel.
/// </summary>
public class ExecutionStatistics
{
    public string KernelName { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public DateTime? LastExecutionTime { get; set; }
}