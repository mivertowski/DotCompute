// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Core.Debugging.Types;
using DotCompute.Abstractions.Performance;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Handles performance profiling and execution tracing for kernel debugging.
/// Provides detailed performance metrics and execution traces.
/// </summary>
public sealed class KernelDebugProfiler(
    ILogger<KernelDebugProfiler> logger,
    ConcurrentQueue<KernelExecutionResult> executionHistory) : IDisposable
{
    private readonly ILogger<KernelDebugProfiler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentQueue<KernelExecutionResult> _executionHistory = executionHistory ?? throw new ArgumentNullException(nameof(executionHistory));
    private DebugServiceOptions _options = new();
    private bool _disposed;

    public void Configure(DebugServiceOptions options) => _options = options ?? throw new ArgumentNullException(nameof(options));

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
        _ = GC.GetTotalMemory(false);

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
                ErrorMessage = null,
                ExecutedAt = DateTime.UtcNow
            };

            // Store in execution history
            _executionHistory.Enqueue(executionResult);

            // Maintain history size limit
            const int maxHistorySize = 1000; // Default max history size
            while (_executionHistory.Count > maxHistorySize)
            {
                _ = _executionHistory.TryDequeue(out _);
            }

            return executionResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during profiled execution of {kernelName} on {backendType}", kernelName, backendType);

            var failedResult = new KernelExecutionResult
            {
                KernelName = kernelName,
                BackendType = backendType,
                Success = false,
                Result = null,
                ExecutionTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                ExecutedAt = DateTime.UtcNow
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
            TracePoints = new List<TracePoint>(),
            TotalExecutionTime = TimeSpan.Zero,
            Success = false
        };

        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            // Phase 1: Preparation
            var prepStopwatch = Stopwatch.StartNew();
            trace.TracePoints.Add(new TracePoint
            {
                Name = "Preparation",
                ExecutionOrder = 1,
                TimestampFromStart = overallStopwatch.Elapsed,
                Values = new Dictionary<string, object> { ["Description"] = "Preparing kernel execution environment" }
            });

            // Simulate preparation work
            await Task.Delay(5);

            prepStopwatch.Stop();
            // trace.TracePoints.Last().EndTime = DateTimeOffset.UtcNow;
            // trace.TracePoints.Last().Duration = prepStopwatch.Elapsed;

            // Phase 2: Compilation
            var compileStopwatch = Stopwatch.StartNew();
            trace.TracePoints.Add(new TracePoint
            {
                Name = "Compilation",
                ExecutionOrder = 2,
                TimestampFromStart = overallStopwatch.Elapsed,
                Values = new Dictionary<string, object> { ["Description"] = "Compiling kernel for target backend" }
            });

            // Simulate compilation
            await Task.Delay(20);

            compileStopwatch.Stop();
            // trace.TracePoints.Last().EndTime = DateTimeOffset.UtcNow;
            // trace.TracePoints.Last().Duration = compileStopwatch.Elapsed;

            // Phase 3: Execution
            var execStopwatch = Stopwatch.StartNew();
            trace.TracePoints.Add(new TracePoint
            {
                Name = "Execution",
                ExecutionOrder = 3,
                TimestampFromStart = overallStopwatch.Elapsed,
                Values = new Dictionary<string, object> { ["Description"] = "Executing kernel on target backend" }
            });

            // Simulate actual execution
            await Task.Delay(50);

            execStopwatch.Stop();
            // trace.TracePoints.Last().EndTime = DateTimeOffset.UtcNow;
            // trace.TracePoints.Last().Duration = execStopwatch.Elapsed;

            // Phase 4: Cleanup
            var cleanupStopwatch = Stopwatch.StartNew();
            trace.TracePoints.Add(new TracePoint
            {
                Name = "Cleanup",
                ExecutionOrder = 4,
                TimestampFromStart = overallStopwatch.Elapsed,
                Values = new Dictionary<string, object> { ["Description"] = "Cleaning up execution resources" }
            });

            // Simulate cleanup
            await Task.Delay(2);

            cleanupStopwatch.Stop();
            // trace.TracePoints.Last().EndTime = DateTimeOffset.UtcNow;
            // trace.TracePoints.Last().Duration = cleanupStopwatch.Elapsed;

            overallStopwatch.Stop();

            // Populate performance counters
            // trace.PerformanceCounters["TotalExecutionTime"] = overallStopwatch.ElapsedMilliseconds;
            // trace.PerformanceCounters["PreparationTime"] = (long)prepStopwatch.Elapsed.TotalMilliseconds;
            // trace.PerformanceCounters["CompilationTime"] = (long)compileStopwatch.Elapsed.TotalMilliseconds;
            // trace.PerformanceCounters["KernelExecutionTime"] = (long)execStopwatch.Elapsed.TotalMilliseconds;
            // trace.PerformanceCounters["CleanupTime"] = (long)cleanupStopwatch.Elapsed.TotalMilliseconds;

            // Populate resource usage
            // trace.ResourceUsage["PeakMemoryUsage"] = GC.GetTotalMemory(false);
            // trace.ResourceUsage["InputDataSize"] = CalculateInputSize(inputs);
            // trace.ResourceUsage["ThreadCount"] = Environment.ProcessorCount; // Placeholder

            // Create final trace with all properties
            return new KernelExecutionTrace
            {
                KernelName = kernelName,
                BackendType = backendType,
                TracePoints = trace.TracePoints,
                TotalExecutionTime = overallStopwatch.Elapsed,
                Success = true,
                Result = new { Status = "Completed successfully" },
                PerformanceMetrics = new PerformanceMetrics
                {
                    ExecutionTimeMs = overallStopwatch.ElapsedMilliseconds,
                    ThroughputGBps = 0, // Would need to calculate based on actual data
                    ComputeUtilization = 80.0, // Example utilization
                    MemoryUtilization = 90.0 // Example efficiency
                }
            };
        }
        catch (Exception ex)
        {
            overallStopwatch.Stop();
            _logger.LogError(ex, "Error during execution tracing");

            // Create error trace with all properties
            return new KernelExecutionTrace
            {
                KernelName = kernelName,
                BackendType = backendType,
                TracePoints = trace.TracePoints,
                TotalExecutionTime = overallStopwatch.Elapsed,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<PerformanceReport> GeneratePerformanceReportAsync(string kernelName, TimeSpan? timeWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);

        var window = timeWindow ?? TimeSpan.FromHours(1);
        var cutoffTime = DateTime.UtcNow - window;

        var relevantResults = _executionHistory
            .Where(r => r.KernelName == kernelName &&
                       r.ExecutedAt >= cutoffTime)
            .ToList();

        if (!relevantResults.Any())
        {
            return Task.FromResult(new PerformanceReport
            {
                KernelName = kernelName,
                TimeWindow = window,
                ExecutionCount = 0,
                Backends = [],
                Summary = "No execution data available for the specified time window"
            });
        }

        var backendGroups = relevantResults.GroupBy(r => r.BackendType).ToList();
        var backendStats = new Dictionary<string, BackendPerformanceStats>();

        foreach (var group in backendGroups)
        {
            var execTimes = group.Select(r => r.ExecutionTime.TotalMilliseconds).ToArray();
            var memoryUsages = group.Select(GetMemoryUsage).Where(m => m > 0).ToArray();

            backendStats[group.Key] = new BackendPerformanceStats
            {
                SampleCount = group.Count(),
                AverageExecutionTimeMs = execTimes.Average(),
                MinExecutionTimeMs = execTimes.Min(),
                MaxExecutionTimeMs = execTimes.Max(),
                ExecutionTimeStdDev = CalculateStandardDeviation(execTimes),
                AverageMemoryUsage = memoryUsages.Any() ? memoryUsages.Average() : 0,
                ReliabilityScore = (float)(group.Count(r => r.Success) / (double)group.Count()),
                LastUpdated = DateTime.UtcNow
            };
        }

        var overallStats = new OverallPerformanceStats
        {
            TotalExecutions = relevantResults.Count(),
            SuccessfulExecutions = relevantResults.Count(r => r.Success),
            FailedExecutions = relevantResults.Count(r => !r.Success),
            AverageExecutionTime = relevantResults.Where(r => r.Success).Select(r => r.ExecutionTime.TotalMilliseconds).DefaultIfEmpty(0).Average(),
            TotalMemoryUsed = relevantResults.Sum(GetMemoryUsage)
        };

        return Task.FromResult(new PerformanceReport
        {
            KernelName = kernelName,
            TimeWindow = window,
            ExecutionCount = relevantResults.Count(),
            Backends = backendStats,
            OverallStats = overallStats,
            Summary = GeneratePerformanceSummary(backendStats, overallStats)
        });
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
            var fastest = backendStats.OrderBy(kvp => kvp.Value.AverageExecutionTimeMs).First();
            summary += $", fastest backend: {fastest.Key} ({fastest.Value.AverageExecutionTimeMs:F2}ms avg)";
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
    public Task<MemoryUsageAnalysis> AnalyzeMemoryUsageAsync(string kernelName, TimeSpan timeWindow)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cutoffTime = DateTime.UtcNow - timeWindow;
        var relevantResults = _executionHistory
            .Where(r => r.KernelName == kernelName && r.ExecutedAt >= cutoffTime)
            .ToList();

        if (!relevantResults.Any())
        {
            return Task.FromResult(new MemoryUsageAnalysis
            {
                KernelName = kernelName,
                AverageMemoryUsage = 0,
                PeakMemoryUsage = 0,
                MinMemoryUsage = 0,
                TotalMemoryAllocated = 0,
                AnalysisTime = DateTime.UtcNow
            });
        }

        var memoryUsages = relevantResults.Select(r => r.MemoryUsed).ToList();

        return Task.FromResult(new MemoryUsageAnalysis
        {
            KernelName = kernelName,
            AverageMemoryUsage = memoryUsages.Average(),
            PeakMemoryUsage = memoryUsages.Max(),
            MinMemoryUsage = memoryUsages.Min(),
            TotalMemoryAllocated = memoryUsages.Sum(),
            AnalysisTime = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Detects bottlenecks in kernel execution.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to analyze.</param>
    /// <returns>Bottleneck analysis result.</returns>
    public async Task<Core.BottleneckAnalysis> DetectBottlenecksAsync(string kernelName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Task.CompletedTask; // Make async for consistency

        var relevantResults = _executionHistory
            .Where(r => r.KernelName == kernelName)
            .ToList();

        var bottlenecks = new List<PerformanceBottleneck>();

        if (relevantResults.Any())
        {
            var avgExecutionTime = relevantResults.Average(r => r.ExecutionTime.TotalMilliseconds);
            var slowExecutions = relevantResults.Where(r => r.ExecutionTime.TotalMilliseconds > avgExecutionTime * 1.5);

            if (slowExecutions.Any())
            {
                bottlenecks.Add(new PerformanceBottleneck
                {
                    Description = $"Slow execution detected in {slowExecutions.Count()} runs - {(slowExecutions.Count() / (double)relevantResults.Count * 100):F1}% of executions",
                    Severity = BottleneckSeverity.Medium,
                    Component = "Execution Performance"
                });
            }
        }

        return new Core.BottleneckAnalysis
        {
            KernelName = kernelName,
            Bottlenecks = bottlenecks.Select(b => new Core.Bottleneck
            {
                Type = AbstractionsMemory.Types.BottleneckType.Unknown,
                Severity = BottleneckSeverity.Medium,
                Description = b.Description,
                Impact = "Performance impact",
                Recommendation = "Optimize for better performance"
            }).ToList(),
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
            TotalExecutions = relevantResults.Count(),
            SuccessfulExecutions = successfulExecutions,
            FailedExecutions = relevantResults.Count - successfulExecutions,
            AverageExecutionTime = avgTime,
            LastExecutionTime = relevantResults.Max(r => r.ExecutedAt)
        };
    }

    private static long GetMemoryUsage(KernelExecutionResult result)
        // Since KernelExecutionResult doesn't have memory usage property,
        // we'll return 0 for now. In a real implementation, this could be tracked separately.
        => 0;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
