// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Execution;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Threading;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Provides optimization strategies and performance tuning for CPU kernel execution.
/// Analyzes workloads and applies various optimization techniques for maximum performance.
/// </summary>
internal sealed class CpuKernelOptimizer : IDisposable
{
    private readonly ILogger _logger;
    private readonly CpuThreadPool _threadPool;
    private readonly Dictionary<string, OptimizationProfile> _profileCache;
    private readonly PerformanceCounter _performanceCounter;
    private bool _disposed;

    // Optimization thresholds and constants
    private const int MinWorkItemsForVectorization = 64;
    private const int MinWorkItemsForParallelization = 16;
    private const double VectorizationSpeedupThreshold = 1.2;
    private const double ParallelizationSpeedupThreshold = 1.5;

    public CpuKernelOptimizer(ILogger logger, CpuThreadPool threadPool)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _threadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        _profileCache = new Dictionary<string, OptimizationProfile>();
        _performanceCounter = new PerformanceCounter();

        _logger.LogDebug("CpuKernelOptimizer initialized");
    }

    /// <summary>
    /// Creates an optimized execution plan for the given kernel definition.
    /// </summary>
    public async Task<KernelExecutionPlan> CreateOptimizedExecutionPlanAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        OptimizationLevel optimizationLevel = OptimizationLevel.Balanced)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(definition);

        _logger.LogDebug("Creating optimized execution plan for kernel {kernelName} with {optimization} optimization",
            definition.Name, optimizationLevel);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check cache for existing optimization profile
            var cacheKey = GenerateCacheKey(definition, workDimensions, optimizationLevel);
            if (_profileCache.TryGetValue(cacheKey, out var cachedProfile))
            {
                _logger.LogDebug("Using cached optimization profile for kernel {kernelName}", definition.Name);
                return cachedProfile.ExecutionPlan;
            }

            // Analyze kernel characteristics
            var analysis = await AnalyzeKernelAsync(definition, workDimensions);

            // Determine optimal execution strategy
            var executionPlan = await CreateExecutionPlanAsync(definition, workDimensions, analysis, optimizationLevel);

            // Cache the optimization profile
            var profile = new OptimizationProfile
            {
                KernelName = definition.Name,
                WorkDimensions = workDimensions,
                OptimizationLevel = optimizationLevel,
                Analysis = analysis,
                ExecutionPlan = executionPlan,
                CreationTime = DateTimeOffset.UtcNow
            };

            _profileCache[cacheKey] = profile;

            stopwatch.Stop();
            _logger.LogDebug("Optimization plan created for kernel {kernelName} in {time:F2}ms",
                definition.Name, stopwatch.Elapsed.TotalMilliseconds);

            return executionPlan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create optimization plan for kernel {kernelName}", definition.Name);
            throw;
        }
    }

    /// <summary>
    /// Analyzes kernel performance and suggests optimizations.
    /// </summary>
    public async Task<OptimizationRecommendations> AnalyzePerformanceAsync(
        KernelDefinition definition,
        ExecutionStatistics statistics,
        WorkDimensions workDimensions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(statistics);

        _logger.LogDebug("Analyzing performance for kernel {kernelName}", definition.Name);

        var recommendations = new OptimizationRecommendations
        {
            KernelName = definition.Name,
            AnalysisTime = DateTimeOffset.UtcNow,
            CurrentPerformance = statistics
        };

        try
        {
            // Analyze vectorization opportunities
            await AnalyzeVectorizationOpportunitiesAsync(definition, statistics, recommendations);

            // Analyze parallelization efficiency
            await AnalyzeParallelizationEfficiencyAsync(statistics, workDimensions, recommendations);

            // Analyze memory access patterns
            await AnalyzeMemoryAccessPatternsAsync(definition, workDimensions, recommendations);

            // Analyze cache utilization
            await AnalyzeCacheUtilizationAsync(definition, statistics, recommendations);

            // Generate specific optimization suggestions
            GenerateOptimizationSuggestions(recommendations);

            _logger.LogDebug("Performance analysis completed for kernel {kernelName} with {count} recommendations",
                definition.Name, recommendations.Suggestions.Count);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performance analysis failed for kernel {kernelName}", definition.Name);
            recommendations.ErrorMessage = ex.Message;
            return recommendations;
        }
    }

    /// <summary>
    /// Benchmarks different execution strategies to find the optimal configuration.
    /// </summary>
    public async Task<BenchmarkResults> BenchmarkExecutionStrategiesAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        int iterations = 10)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(definition);

        _logger.LogInformation("Benchmarking execution strategies for kernel {kernelName}", definition.Name);

        var results = new BenchmarkResults
        {
            KernelName = definition.Name,
            WorkDimensions = workDimensions,
            Iterations = iterations,
            BenchmarkTime = DateTimeOffset.UtcNow
        };

        try
        {
            // Benchmark scalar execution
            var scalarPlan = CreateBasicExecutionPlan(definition, workDimensions, false);
            results.ScalarPerformance = await BenchmarkExecutionPlanAsync(scalarPlan, iterations);

            // Benchmark vectorized execution if supported
            if (Vector.IsHardwareAccelerated && CanVectorize(definition))
            {
                var vectorizedPlan = CreateBasicExecutionPlan(definition, workDimensions, true);
                results.VectorizedPerformance = await BenchmarkExecutionPlanAsync(vectorizedPlan, iterations);
            }

            // Benchmark different thread counts
            results.ParallelizationResults = await BenchmarkParallelizationAsync(definition, workDimensions, iterations);

            // Determine optimal configuration
            results.OptimalConfiguration = DetermineOptimalConfiguration(results);

            _logger.LogInformation("Benchmark completed for kernel {kernelName}: Optimal={optimal}",
                definition.Name, results.OptimalConfiguration.Description);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Benchmarking failed for kernel {kernelName}", definition.Name);
            results.ErrorMessage = ex.Message;
            return results;
        }
    }

    /// <summary>
    /// Applies dynamic optimizations based on runtime performance feedback.
    /// </summary>
    public async Task<bool> ApplyDynamicOptimizationsAsync(
        string kernelName,
        ExecutionStatistics currentStats,
        KernelExecutionPlan currentPlan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ArgumentNullException.ThrowIfNull(currentStats);
        ArgumentNullException.ThrowIfNull(currentPlan);

        try
        {
            _logger.LogDebug("Applying dynamic optimizations for kernel {kernelName}", kernelName);

            var optimizationsApplied = false;

            // Check if performance has degraded
            if (await DetectPerformanceDegradationAsync(kernelName, currentStats))
            {
                // Attempt to optimize thread pool configuration
                if (await OptimizeThreadPoolConfigurationAsync(currentPlan))
                {
                    optimizationsApplied = true;
                    _logger.LogDebug("Thread pool configuration optimized for kernel {kernelName}", kernelName);
                }

                // Attempt to adjust vectorization settings
                if (await OptimizeVectorizationSettingsAsync(currentPlan, currentStats))
                {
                    optimizationsApplied = true;
                    _logger.LogDebug("Vectorization settings optimized for kernel {kernelName}", kernelName);
                }
            }

            return optimizationsApplied;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic optimization failed for kernel {kernelName}", kernelName);
            return false;
        }
    }

    // Private analysis methods

    private async Task<KernelAnalysis> AnalyzeKernelAsync(KernelDefinition definition, WorkDimensions workDimensions)
    {
        var analysis = new KernelAnalysis
        {
            Definition = definition,
            WorkDimensions = workDimensions,
            TotalWorkItems = GetTotalWorkItems(workDimensions),
            CanVectorize = CanVectorize(definition),
            OptimalVectorWidth = DetermineOptimalVectorWidth(definition),
            MemoryAccessPattern = AnalyzeMemoryAccessPattern(definition),
            ComputeIntensity = EstimateComputeIntensity(definition),
            ThreadingOverhead = EstimateThreadingOverhead(workDimensions)
        };

        // Add metadata to the analysis
        analysis.Definition.Metadata ??= new Dictionary<string, object>();
        analysis.Definition.Metadata["SimdCapabilities"] = CreateSimdSummary();

        return analysis;
    }

    private async Task<KernelExecutionPlan> CreateExecutionPlanAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        KernelAnalysis analysis,
        OptimizationLevel optimizationLevel)
    {
        var plan = new KernelExecutionPlan
        {
            Analysis = analysis,
            UseVectorization = ShouldUseVectorization(analysis, optimizationLevel),
            UseParallelization = ShouldUseParallelization(analysis, optimizationLevel),
            OptimalThreadCount = DetermineOptimalThreadCount(analysis, optimizationLevel),
            VectorizationFactor = DetermineVectorizationFactor(analysis),
            VectorWidth = analysis.OptimalVectorWidth,
            MemoryOptimizations = DetermineMemoryOptimizations(analysis, optimizationLevel),
            CacheOptimizations = DetermineCacheOptimizations(analysis, optimizationLevel)
        };

        return plan;
    }

    private static async Task AnalyzeVectorizationOpportunitiesAsync(
        KernelDefinition definition,
        ExecutionStatistics statistics,
        OptimizationRecommendations recommendations)
    {
        if (!statistics.UseVectorization && CanVectorize(definition))
        {
            recommendations.Suggestions.Add(new OptimizationSuggestion
            {
                Type = OptimizationType.Vectorization,
                Description = "Enable SIMD vectorization for this kernel",
                ExpectedSpeedup = EstimateVectorizationSpeedup(definition),
                Implementation = "Set UseVectorization = true in execution plan"
            });
        }
        else if (statistics.UseVectorization && statistics.AverageExecutionTimeMs > 0)
        {
            // Analyze current vectorization efficiency
            var efficiency = CalculateVectorizationEfficiency(statistics);
            if (efficiency < 0.7) // Less than 70% efficient
            {
                recommendations.Suggestions.Add(new OptimizationSuggestion
                {
                    Type = OptimizationType.Vectorization,
                    Description = "Vectorization efficiency is low - consider optimizing data layout",
                    ExpectedSpeedup = 1.3,
                    Implementation = "Restructure data for better SIMD utilization"
                });
            }
        }
    }

    private async Task AnalyzeParallelizationEfficiencyAsync(
        ExecutionStatistics statistics,
        WorkDimensions workDimensions,
        OptimizationRecommendations recommendations)
    {
        var totalWorkItems = GetTotalWorkItems(workDimensions);
        var currentThreads = _threadPool.MaxConcurrency;

        if (totalWorkItems > MinWorkItemsForParallelization)
        {
            var optimalThreads = DetermineOptimalThreadCount(totalWorkItems);
            if (Math.Abs(currentThreads - optimalThreads) > 1)
            {
                recommendations.Suggestions.Add(new OptimizationSuggestion
                {
                    Type = OptimizationType.Parallelization,
                    Description = $"Adjust thread count from {currentThreads} to {optimalThreads}",
                    ExpectedSpeedup = EstimateParallelizationSpeedup(optimalThreads, currentThreads),
                    Implementation = $"Configure thread pool with {optimalThreads} threads"
                });
            }
        }
    }

    private static async Task AnalyzeMemoryAccessPatternsAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        OptimizationRecommendations recommendations)
    {
        var accessPattern = AnalyzeMemoryAccessPattern(definition);

        if (accessPattern == MemoryAccessPattern.Random)
        {
            recommendations.Suggestions.Add(new OptimizationSuggestion
            {
                Type = OptimizationType.Memory,
                Description = "Random memory access pattern detected - consider data restructuring",
                ExpectedSpeedup = 1.5,
                Implementation = "Reorganize data structures for better spatial locality"
            });
        }
        else if (accessPattern == MemoryAccessPattern.Strided)
        {
            recommendations.Suggestions.Add(new OptimizationSuggestion
            {
                Type = OptimizationType.Memory,
                Description = "Strided memory access pattern - consider memory prefetching",
                ExpectedSpeedup = 1.2,
                Implementation = "Add memory prefetch hints or restructure access pattern"
            });
        }
    }

    private static async Task AnalyzeCacheUtilizationAsync(
        KernelDefinition definition,
        ExecutionStatistics statistics,
        OptimizationRecommendations recommendations)
    {
        // Estimate cache utilization based on data access patterns
        var estimatedCacheUtilization = EstimateCacheUtilization(definition, statistics);

        if (estimatedCacheUtilization < 0.6) // Less than 60% cache utilization
        {
            recommendations.Suggestions.Add(new OptimizationSuggestion
            {
                Type = OptimizationType.Cache,
                Description = "Low cache utilization detected",
                ExpectedSpeedup = 1.4,
                Implementation = "Optimize data structures for better cache locality"
            });
        }
    }

    // Helper methods for optimization logic

    private static bool CanVectorize(KernelDefinition definition)
    {
        // Simplified heuristic - in practice, this would analyze the kernel code
        return Vector.IsHardwareAccelerated &&
               definition.Name.Contains("Vector", StringComparison.OrdinalIgnoreCase);
    }

    private static int DetermineOptimalVectorWidth(KernelDefinition definition)
    {
        return Vector<float>.Count; // Use the platform's preferred vector width
    }

    private static MemoryAccessPattern AnalyzeMemoryAccessPattern(KernelDefinition definition)
    {
        // Simplified pattern analysis - in practice, this would examine kernel code
        if (definition.Name.Contains("Random", StringComparison.OrdinalIgnoreCase))
        {

            return MemoryAccessPattern.Random;
        }


        if (definition.Name.Contains("Stride", StringComparison.OrdinalIgnoreCase))
        {

            return MemoryAccessPattern.Strided;
        }


        return MemoryAccessPattern.Sequential;
    }

    private static double EstimateComputeIntensity(KernelDefinition definition)
    {
        // Simplified estimation - in practice, this would analyze operations per memory access
        return 1.0; // Default moderate compute intensity
    }

    private static double EstimateThreadingOverhead(WorkDimensions workDimensions)
    {
        var totalWorkItems = GetTotalWorkItems(workDimensions);

        // Simple heuristic: smaller workloads have higher threading overhead
        if (totalWorkItems < 100)
        {
            return 0.5;
        }


        if (totalWorkItems < 1000)
        {
            return 0.2;
        }


        return 0.1;
    }

    private static long GetTotalWorkItems(WorkDimensions dimensions)
    {
        return dimensions.X * dimensions.Y * dimensions.Z;
    }

    private static SimdSummary CreateSimdSummary()
    {
        return new SimdSummary
        {
            IsHardwareAccelerated = Vector.IsHardwareAccelerated,
            PreferredVectorWidth = Vector<float>.Count,
            SupportedInstructionSets = new HashSet<string> { "SSE", "AVX", "AVX2" } // Simplified
        };
    }

    private KernelExecutionPlan CreateBasicExecutionPlan(KernelDefinition definition, WorkDimensions workDimensions, bool useVectorization)
    {
        return new KernelExecutionPlan
        {
            Analysis = new KernelAnalysis { Definition = definition, WorkDimensions = workDimensions },
            UseVectorization = useVectorization,
            UseParallelization = true,
            OptimalThreadCount = _threadPool.MaxConcurrency,
            VectorizationFactor = useVectorization ? Vector<float>.Count : 1,
            VectorWidth = Vector<float>.Count
        };
    }

    // Performance benchmarking and measurement

    private static async Task<PerformanceMetrics> BenchmarkExecutionPlanAsync(KernelExecutionPlan plan, int iterations)
    {
        var metrics = new PerformanceMetrics();
        var executionTimes = new List<double>();

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simulate kernel execution (in practice, this would execute the actual kernel)
            await Task.Delay(1); // Placeholder

            stopwatch.Stop();
            executionTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        metrics.AverageTimeMs = executionTimes.Average();
        metrics.MinTimeMs = executionTimes.Min();
        metrics.MaxTimeMs = executionTimes.Max();
        metrics.StandardDeviation = CalculateStandardDeviation(executionTimes);

        return metrics;
    }

    private async Task<Dictionary<int, PerformanceMetrics>> BenchmarkParallelizationAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        int iterations)
    {
        var results = new Dictionary<int, PerformanceMetrics>();
        var threadCounts = new[] { 1, 2, 4, 8, Environment.ProcessorCount };

        foreach (var threadCount in threadCounts)
        {
            var plan = CreateBasicExecutionPlan(definition, workDimensions, false);
            plan.OptimalThreadCount = threadCount;

            results[threadCount] = await BenchmarkExecutionPlanAsync(plan, iterations);
        }

        return results;
    }

    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        var mean = valuesList.Average();
        var squaredDifferences = valuesList.Select(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(squaredDifferences.Average());
    }

    private OptimalConfiguration DetermineOptimalConfiguration(BenchmarkResults results)
    {
        var bestPerformance = results.ScalarPerformance;
        var bestDescription = "Scalar execution";

        if (results.VectorizedPerformance != null &&
            results.VectorizedPerformance.AverageTimeMs < bestPerformance.AverageTimeMs)
        {
            bestPerformance = results.VectorizedPerformance;
            bestDescription = "Vectorized execution";
        }

        return new OptimalConfiguration
        {
            Description = bestDescription,
            Performance = bestPerformance,
            UseVectorization = bestDescription.Contains("Vectorized"),
            OptimalThreadCount = DetermineBestThreadCount(results.ParallelizationResults)
        };
    }

    private static int DetermineBestThreadCount(Dictionary<int, PerformanceMetrics> parallelizationResults)
    {
        return parallelizationResults
            .OrderBy(kvp => kvp.Value.AverageTimeMs)
            .First()
            .Key;
    }

    // Dynamic optimization methods

    private static async Task<bool> DetectPerformanceDegradationAsync(string kernelName, ExecutionStatistics currentStats)
    {
        // Simplified degradation detection - compare against historical performance
        return currentStats.AverageExecutionTimeMs > 100; // Placeholder threshold
    }

    private static async Task<bool> OptimizeThreadPoolConfigurationAsync(KernelExecutionPlan plan)
    {
        // Adjust thread pool settings based on current workload
        return false; // Placeholder
    }

    private static async Task<bool> OptimizeVectorizationSettingsAsync(KernelExecutionPlan plan, ExecutionStatistics stats)
    {
        // Adjust vectorization parameters based on performance feedback
        return false; // Placeholder
    }

    // Utility methods for analysis

    private static bool ShouldUseVectorization(KernelAnalysis analysis, OptimizationLevel level)
    {
        return analysis.CanVectorize &&
               analysis.TotalWorkItems >= MinWorkItemsForVectorization &&
               level >= OptimizationLevel.Balanced;
    }

    private static bool ShouldUseParallelization(KernelAnalysis analysis, OptimizationLevel level)
    {
        return analysis.TotalWorkItems >= MinWorkItemsForParallelization &&
               analysis.ThreadingOverhead < 0.3;
    }

    private int DetermineOptimalThreadCount(KernelAnalysis analysis, OptimizationLevel level)
    {
        var totalWorkItems = analysis.TotalWorkItems;
        var maxThreads = _threadPool.MaxConcurrency;

        return level switch
        {
            OptimizationLevel.None => 1,
            OptimizationLevel.Basic => Math.Min(2, maxThreads),
            OptimizationLevel.Balanced => Math.Min((int)Math.Ceiling(totalWorkItems / 100.0), maxThreads),
            OptimizationLevel.Aggressive => maxThreads,
            _ => Math.Min(4, maxThreads)
        };
    }

    private int DetermineOptimalThreadCount(long totalWorkItems)
    {
        var maxThreads = _threadPool.MaxConcurrency;
        var optimalThreads = Math.Min((int)Math.Ceiling(totalWorkItems / 100.0), maxThreads);
        return Math.Max(1, optimalThreads);
    }

    private static int DetermineVectorizationFactor(KernelAnalysis analysis)
    {
        return analysis.CanVectorize ? analysis.OptimalVectorWidth : 1;
    }

    private static List<string> DetermineMemoryOptimizations(KernelAnalysis analysis, OptimizationLevel level)
    {
        var optimizations = new List<string>();

        if (level >= OptimizationLevel.Balanced)
        {
            if (analysis.MemoryAccessPattern == MemoryAccessPattern.Random)
            {
                optimizations.Add("EnableMemoryPrefetching");
            }

            if (analysis.ComputeIntensity < 0.5)
            {
                optimizations.Add("OptimizeDataLayout");
            }
        }

        return optimizations;
    }

    private static List<string> DetermineCacheOptimizations(KernelAnalysis analysis, OptimizationLevel level)
    {
        var optimizations = new List<string>();

        if (level >= OptimizationLevel.Aggressive)
        {
            optimizations.Add("EnableCachePrefetching");
            optimizations.Add("OptimizeDataLocality");
        }

        return optimizations;
    }

    private static double EstimateVectorizationSpeedup(KernelDefinition definition)
    {
        // Simplified estimation based on kernel characteristics
        return Vector<float>.Count * 0.8; // Assume 80% of theoretical speedup
    }

    private static double EstimateParallelizationSpeedup(int optimalThreads, int currentThreads)
    {
        return Math.Min(optimalThreads / (double)currentThreads, Environment.ProcessorCount);
    }

    private static double CalculateVectorizationEfficiency(ExecutionStatistics statistics)
    {
        // Simplified efficiency calculation
        return statistics.UseVectorization ? 0.8 : 0.0;
    }

    private static double EstimateCacheUtilization(KernelDefinition definition, ExecutionStatistics statistics)
    {
        // Simplified cache utilization estimation
        return 0.7; // Default moderate cache utilization
    }

    private static void GenerateOptimizationSuggestions(OptimizationRecommendations recommendations)
    {
        // Sort suggestions by expected speedup
        recommendations.Suggestions.Sort((a, b) => b.ExpectedSpeedup.CompareTo(a.ExpectedSpeedup));

        // Limit to top 5 suggestions
        if (recommendations.Suggestions.Count > 5)
        {
            recommendations.Suggestions = recommendations.Suggestions.Take(5).ToList();
        }
    }

    private static string GenerateCacheKey(KernelDefinition definition, WorkDimensions workDimensions, OptimizationLevel level)
    {
        return $"{definition.Name}_{workDimensions.X}x{workDimensions.Y}x{workDimensions.Z}_{level}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _performanceCounter?.Dispose();
            _disposed = true;
        }
    }
}

// Supporting enums and classes for optimization

// Use the canonical OptimizationLevel from DotCompute.Abstractions.Types
// This local enum has been replaced with the unified type

public enum OptimizationType
{
    Vectorization,
    Parallelization,
    Memory,
    Cache,
    Threading
}

public class OptimizationProfile
{
    public required string KernelName { get; set; }
    public WorkDimensions WorkDimensions { get; set; }
    public OptimizationLevel OptimizationLevel { get; set; }
    public required KernelAnalysis Analysis { get; set; }
    public required KernelExecutionPlan ExecutionPlan { get; set; }
    public DateTimeOffset CreationTime { get; set; }
}

public class OptimizationRecommendations
{
    public required string KernelName { get; set; }
    public DateTimeOffset AnalysisTime { get; set; }
    public required ExecutionStatistics CurrentPerformance { get; set; }
    public List<OptimizationSuggestion> Suggestions { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class OptimizationSuggestion
{
    public OptimizationType Type { get; set; }
    public required string Description { get; set; }
    public double ExpectedSpeedup { get; set; }
    public required string Implementation { get; set; }
}

public class BenchmarkResults
{
    public required string KernelName { get; set; }
    public WorkDimensions WorkDimensions { get; set; }
    public int Iterations { get; set; }
    public DateTimeOffset BenchmarkTime { get; set; }
    public required PerformanceMetrics ScalarPerformance { get; set; }
    public PerformanceMetrics? VectorizedPerformance { get; set; }
    public Dictionary<int, PerformanceMetrics> ParallelizationResults { get; set; } = new();
    public OptimalConfiguration? OptimalConfiguration { get; set; }
    public string? ErrorMessage { get; set; }
}

// Use the canonical PerformanceMetrics from DotCompute.Abstractions.Performance
// This local class has been replaced with the unified type

public class OptimalConfiguration
{
    public required string Description { get; set; }
    public required PerformanceMetrics Performance { get; set; }
    public bool UseVectorization { get; set; }
    public int OptimalThreadCount { get; set; }
}

public class PerformanceCounter : IDisposable
{
    public void Dispose() { }
}