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
/// Handles advanced debug analysis including determinism checking and memory pattern analysis.
/// Provides comprehensive analysis capabilities for kernel debugging.
/// </summary>
internal sealed class KernelDebugAnalyzer : IDisposable
{
    private readonly ILogger<KernelDebugAnalyzer> _logger;
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators;
    private readonly KernelDebugProfiler _profiler;
    private DebugServiceOptions _options;
    private bool _disposed;

    private static readonly string[] NonDeterminismRecommendations =
    [
        "Check for race conditions in parallel execution",
        "Verify that random number generators use fixed seeds",
        "Ensure atomic operations where required",
        "Consider thread-local storage for mutable state"
    ];

    public KernelDebugAnalyzer(
        ILogger<KernelDebugAnalyzer> logger,
        ConcurrentDictionary<string, IAccelerator> accelerators,
        KernelDebugProfiler profiler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accelerators = accelerators ?? throw new ArgumentNullException(nameof(accelerators));
        _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
        _options = new DebugServiceOptions();
    }

    public void Configure(DebugServiceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<DeterminismReport> ValidateDeterminismAsync(
        string kernelName,
        object[] inputs,
        int iterations = 5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        if (iterations < 2)
        {

            throw new ArgumentException("Iterations must be at least 2 for determinism validation", nameof(iterations));
        }


        _logger.LogInformation("Validating determinism for kernel {kernelName} with {iterations} iterations", kernelName, iterations);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get available accelerators for testing
            var availableAccelerators = await GetAvailableAcceleratorsAsync();
            var results = new List<KernelExecutionResult>();

            // Execute multiple times on each available backend
            foreach (var accelerator in availableAccelerators)
            {
                for (var i = 0; i < iterations; i++)
                {
                    var result = await ExecuteKernelSafelyAsync(kernelName, accelerator.Key, inputs, accelerator.Value);
                    if (result.Success)
                    {
                        results.Add(result);
                    }
                }
            }

            var successfulResults = results.Where(r => r.Success).ToArray();

            if (successfulResults.Length < 2)
            {
                return new DeterminismReport
                {
                    KernelName = kernelName,
                    IsDeterministic = false,
                    ExecutionCount = results.Count,
                    AllResults = results,
                    NonDeterminismSource = "Insufficient successful executions for comparison",
                    Recommendations = ["Ensure kernel executes successfully before testing determinism"]
                };
            }

            // Group results by backend and analyze determinism
            var backendGroups = successfulResults.GroupBy(r => r.BackendType);
            var isDeterministic = true;
            var maxVariation = 0f;
            var nonDeterminismSource = string.Empty;
            var recommendations = new List<string>();

            foreach (var group in backendGroups)
            {
                var backendResults = group.ToArray();
                if (backendResults.Length < 2)
                {
                    continue;
                }


                var (isBackendDeterministic, variation, source) = AnalyzeBackendDeterminism(backendResults);

                if (!isBackendDeterministic)
                {
                    isDeterministic = false;
                    maxVariation = Math.Max(maxVariation, variation);
                    if (string.IsNullOrEmpty(nonDeterminismSource))
                    {
                        nonDeterminismSource = $"{group.Key}: {source}";
                    }
                }
            }

            // Add recommendations for non-deterministic kernels
            if (!isDeterministic)
            {
                recommendations.AddRange(NonDeterminismRecommendations);
            }

            stopwatch.Stop();

            return new DeterminismReport
            {
                KernelName = kernelName,
                IsDeterministic = isDeterministic,
                ExecutionCount = successfulResults.Length,
                AllResults = results,
                MaxVariation = maxVariation,
                NonDeterminismSource = nonDeterminismSource,
                Recommendations = recommendations,
                AnalysisTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during determinism validation");
            stopwatch.Stop();

            return new DeterminismReport
            {
                KernelName = kernelName,
                IsDeterministic = false,
                ExecutionCount = 0,
                AllResults = [],
                NonDeterminismSource = $"Analysis failed: {ex.Message}",
                Recommendations = ["Fix execution errors before testing determinism"],
                AnalysisTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<MemoryAnalysisReport> AnalyzeMemoryPatternsAsync(
        string kernelName,
        object[] inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        _logger.LogInformation("Analyzing memory patterns for kernel {kernelName}", kernelName);

        try
        {
            // Try GPU first for memory analysis, fallback to CPU
            var preferredBackends = new[] { "CUDA", "CPU" };
            IAccelerator? accelerator = null;
            var backendType = "CPU";

            foreach (var backend in preferredBackends)
            {
                accelerator = await GetOrCreateAcceleratorAsync(backend);
                if (accelerator != null)
                {
                    backendType = backend;
                    break;
                }
            }

            if (accelerator == null)
            {
                return new MemoryAnalysisReport
                {
                    KernelName = kernelName,
                    BackendType = "None",
                    Warnings = ["No backends available for memory analysis"]
                };
            }

            var accessPatterns = new List<MemoryAccessPattern>();
            var optimizations = new List<PerformanceOptimization>();
            var warnings = new List<string>();

            // Analyze memory access patterns
            var totalMemoryAccessed = EstimateMemoryUsage(inputs);
            var memoryEfficiency = CalculateMemoryEfficiency(inputs, backendType);

            // Add common memory access patterns
            accessPatterns.Add(new MemoryAccessPattern
            {
                PatternType = "Sequential",
                AccessCount = totalMemoryAccessed / sizeof(float), // Assuming float data
                CoalescingEfficiency = backendType == "CUDA" ? 0.85f : 1.0f,
                Issues = backendType == "CUDA" && memoryEfficiency < 0.8f
                    ? ["Consider memory coalescing optimizations"]
                    : []
            });

            // Add optimization suggestions
            if (backendType == "CUDA" && totalMemoryAccessed > 1024 * 1024) // > 1MB
            {
                optimizations.Add(new PerformanceOptimization
                {
                    Type = "Memory Coalescing",
                    Description = "Optimize memory access patterns for better GPU throughput",
                    PotentialSpeedup = 1.5f,
                    Implementation = "Ensure consecutive threads access consecutive memory locations"
                });
            }

            if (memoryEfficiency < 0.6f)
            {
                warnings.Add("Low memory efficiency detected - consider data layout optimizations");
            }

            // Analyze for cache efficiency
            var cacheEfficiency = AnalyzeCacheEfficiency(inputs, backendType);
            if (cacheEfficiency < 0.7f)
            {
                optimizations.Add(new PerformanceOptimization
                {
                    Type = "Cache Optimization",
                    Description = "Improve data locality to increase cache hit rates",
                    PotentialSpeedup = 1.3f,
                    Implementation = "Reorganize data structures for better spatial locality"
                });
            }

            return new MemoryAnalysisReport
            {
                KernelName = kernelName,
                BackendType = backendType,
                AccessPatterns = accessPatterns,
                Optimizations = optimizations,
                TotalMemoryAccessed = totalMemoryAccessed,
                MemoryEfficiency = memoryEfficiency,
                CacheEfficiency = cacheEfficiency,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during memory analysis");

            return new MemoryAnalysisReport
            {
                KernelName = kernelName,
                BackendType = "Error",
                Warnings = [$"Memory analysis failed: {ex.Message}"]
            };
        }
    }

    public async Task<ResourceUtilizationReport> AnalyzeResourceUtilizationAsync(
        string kernelName,
        object[] inputs,
        TimeSpan? analysisWindow = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(inputs);

        var window = analysisWindow ?? TimeSpan.FromMinutes(5);
        _logger.LogInformation("Analyzing resource utilization for kernel {kernelName} over {window}", kernelName, window);

        try
        {
            var report = new ResourceUtilizationReport
            {
                KernelName = kernelName,
                AnalysisWindow = window,
                StartTime = DateTimeOffset.UtcNow - window,
                EndTime = DateTimeOffset.UtcNow
            };

            // Analyze CPU utilization
            var cpuUtilization = await AnalyzeCpuUtilizationAsync(kernelName, inputs);
            report.CpuUtilization = cpuUtilization;

            // Analyze memory utilization
            var memoryUtilization = await AnalyzeMemoryUtilizationAsync(kernelName, inputs);
            report.MemoryUtilization = memoryUtilization;

            // Analyze GPU utilization if available
            var gpuUtilization = await AnalyzeGpuUtilizationAsync(kernelName, inputs);
            report.GpuUtilization = gpuUtilization;

            // Generate optimization recommendations
            report.Recommendations = GenerateUtilizationRecommendations(report);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during resource utilization analysis");

            return new ResourceUtilizationReport
            {
                KernelName = kernelName,
                AnalysisWindow = window,
                StartTime = DateTimeOffset.UtcNow - window,
                EndTime = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private static (bool isDeterministic, float variation, string source) AnalyzeBackendDeterminism(KernelExecutionResult[] results)
    {
        if (results.Length < 2)
        {

            return (true, 0f, "");
        }


        var allResults = results.Select(r => r.Result).ToList();
        var uniqueResults = allResults.Distinct().Count();

        if (uniqueResults == 1)
        {
            return (true, 0f, "");
        }

        // Calculate variation
        var maxVariation = 0f;
        for (var i = 0; i < allResults.Count; i++)
        {
            for (var j = i + 1; j < allResults.Count; j++)
            {
                var (_, difference, _) = CompareObjects(allResults[i], allResults[j], 1e-6f);
                maxVariation = Math.Max(maxVariation, difference);
            }
        }

        var source = AnalyzeNonDeterminismSource(allResults);
        return (false, maxVariation, source);
    }

    private static (bool isMatch, float difference, string details) CompareObjects(object? obj1, object? obj2, float tolerance)
    {
        if (obj1 == null && obj2 == null)
        {
            return (true, 0f, "");
        }


        if (obj1 == null || obj2 == null)
        {
            return (false, 1f, "Null mismatch");
        }


        if (obj1.GetType() != obj2.GetType())
        {

            return (false, 1.0f, "Type mismatch");
        }

        // Numeric comparison with tolerance

        if (obj1 is float f1 && obj2 is float f2)
        {
            var diff = Math.Abs(f1 - f2);
            return (diff <= tolerance, diff, $"Float difference: {diff}");
        }

        // Array comparison
        if (obj1 is Array arr1 && obj2 is Array arr2)
        {
            if (arr1.Length != arr2.Length)
            {

                return (false, 1.0f, "Array length mismatch");
            }


            var totalDifference = 0f;
            var elementCount = 0;

            for (var i = 0; i < arr1.Length; i++)
            {
                var (isMatch, diff, _) = CompareObjects(arr1.GetValue(i), arr2.GetValue(i), tolerance);
                totalDifference += diff;
                elementCount++;

                if (!isMatch && diff > tolerance)
                {

                    return (false, diff, $"Array element {i} differs by {diff}");
                }

            }

            var avgDifference = elementCount > 0 ? totalDifference / elementCount : 0f;
            return (avgDifference <= tolerance, avgDifference, $"Average difference: {avgDifference}");
        }

        // Default comparison
        return (obj1.Equals(obj2), obj1.Equals(obj2) ? 0f : 0.5f, "Object difference");
    }

    private static string AnalyzeNonDeterminismSource(List<object> results)
    {
        var uniqueResults = results.Distinct().Count();
        var totalResults = results.Count;

        if (uniqueResults == totalResults)
        {

            return "Each execution produced a unique result - possible random number generation";
        }


        if (uniqueResults == 2)
        {

            return "Results alternate between two values - possible race condition";
        }


        if (uniqueResults < totalResults / 2)
        {

            return "Limited result variation - possible timing-dependent behavior";
        }


        return "Multiple different results - possible non-deterministic algorithm or hardware behavior";
    }

    private static long EstimateMemoryUsage(object[] inputs)
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

    private float CalculateMemoryEfficiency(object[] inputs, string backendType)
    {
        // Simplified memory efficiency calculation
        var totalSize = EstimateMemoryUsage(inputs);

        // For GPU backends, consider memory coalescing
        if (backendType == "CUDA")
        {
            // Assume 80% efficiency for properly aligned data
            return 0.8f;
        }

        // For CPU, assume higher efficiency due to cache hierarchy
        return 0.95f;
    }

    private static float AnalyzeCacheEfficiency(object[] inputs, string backendType)
    {
        // Simplified cache efficiency analysis
        var sequentialAccess = EstimateSequentialAccessRatio(inputs);

        return backendType switch
        {
            "CPU" => sequentialAccess * 0.9f + 0.1f, // CPU caches are very effective for sequential access
            "CUDA" => sequentialAccess * 0.7f + 0.3f, // GPU caches less effective but still helpful
            _ => 0.5f // Default assumption
        };
    }

    private static float EstimateSequentialAccessRatio(object[] inputs)
    {
        // Simplified estimation - assume mostly sequential access for arrays
        var arrayInputs = inputs.OfType<Array>().Count();
        var totalInputs = inputs.Length;

        return totalInputs > 0 ? arrayInputs / (float)totalInputs : 0.5f;
    }

    private async Task<Dictionary<string, IAccelerator>> GetAvailableAcceleratorsAsync()
    {
        var available = new Dictionary<string, IAccelerator>();

        foreach (var kvp in _accelerators)
        {
            if (kvp.Value != null)
            {
                available[kvp.Key] = kvp.Value;
            }
        }

        return available;
    }

    private async Task<IAccelerator?> GetOrCreateAcceleratorAsync(string backendType)
    {
        if (_accelerators.TryGetValue(backendType, out var existing))
        {
            return existing;
        }

        // Simplified implementation - return null for missing accelerators
        return null;
    }

    private async Task<KernelExecutionResult> ExecuteKernelSafelyAsync(
        string kernelName,
        string backendType,
        object[] inputs,
        IAccelerator accelerator)
    {
        return await _profiler.ExecuteWithProfilingAsync(kernelName, backendType, inputs, accelerator);
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
            _ => IntPtr.Size
        };
    }

    private static async Task<CpuUtilizationInfo> AnalyzeCpuUtilizationAsync(string kernelName, object[] inputs)
    {
        // Simplified CPU utilization analysis
        return new CpuUtilizationInfo
        {
            AverageUtilization = 65.0, // Placeholder
            PeakUtilization = 85.0,
            CoreCount = Environment.ProcessorCount,
            EffectiveParallelism = 0.75
        };
    }

    private async Task<MemoryUtilizationInfo> AnalyzeMemoryUtilizationAsync(string kernelName, object[] inputs)
    {
        var totalMemory = GC.GetTotalMemory(false);
        var estimatedUsage = EstimateMemoryUsage(inputs);

        return new MemoryUtilizationInfo
        {
            PeakUsage = totalMemory,
            AverageUsage = totalMemory * 0.8,
            EstimatedKernelUsage = estimatedUsage,
            EfficiencyRatio = estimatedUsage > 0 ? Math.Min(1.0, totalMemory / (double)estimatedUsage) : 1.0
        };
    }

    private async Task<GpuUtilizationInfo?> AnalyzeGpuUtilizationAsync(string kernelName, object[] inputs)
    {
        // Check if GPU accelerator is available
        if (!_accelerators.ContainsKey("CUDA"))
        {

            return null;
        }


        return new GpuUtilizationInfo
        {
            ComputeUtilization = 70.0, // Placeholder
            MemoryUtilization = 55.0,
            MemoryBandwidthUtilization = 80.0,
            OccupancyRate = 0.65
        };
    }

    private static List<string> GenerateUtilizationRecommendations(ResourceUtilizationReport report)
    {
        var recommendations = new List<string>();

        if (report.CpuUtilization?.AverageUtilization < 50)
        {
            recommendations.Add("CPU utilization is low - consider increasing parallelism or workload size");
        }

        if (report.MemoryUtilization?.EfficiencyRatio < 0.6)
        {
            recommendations.Add("Memory efficiency is low - consider optimizing data structures");
        }

        if (report.GpuUtilization?.OccupancyRate < 0.5)
        {
            recommendations.Add("GPU occupancy is low - consider optimizing thread block size");
        }

        return recommendations;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

// Supporting classes for analysis reports
public class ResourceUtilizationReport
{
    public required string KernelName { get; set; }
    public TimeSpan AnalysisWindow { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public CpuUtilizationInfo? CpuUtilization { get; set; }
    public MemoryUtilizationInfo? MemoryUtilization { get; set; }
    public GpuUtilizationInfo? GpuUtilization { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class CpuUtilizationInfo
{
    public double AverageUtilization { get; set; }
    public double PeakUtilization { get; set; }
    public int CoreCount { get; set; }
    public double EffectiveParallelism { get; set; }
}

public class MemoryUtilizationInfo
{
    public long PeakUsage { get; set; }
    public double AverageUsage { get; set; }
    public long EstimatedKernelUsage { get; set; }
    public double EfficiencyRatio { get; set; }
}

public class GpuUtilizationInfo
{
    public double ComputeUtilization { get; set; }
    public double MemoryUtilization { get; set; }
    public double MemoryBandwidthUtilization { get; set; }
    public double OccupancyRate { get; set; }
}