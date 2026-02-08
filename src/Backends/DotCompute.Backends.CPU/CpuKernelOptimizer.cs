// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// Import organized CPU optimization types
global using DotCompute.Backends.CPU.Types;

using System.Diagnostics;
using System.Numerics;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels.Enums;
using DotCompute.Backends.CPU.Kernels.Models;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;
using MemoryAccessPattern = DotCompute.Abstractions.Types.MemoryAccessPattern;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Provides optimization strategies and performance tuning for CPU kernel execution.
/// Analyzes workloads and applies various optimization techniques for maximum performance.
/// </summary>
internal sealed partial class CpuKernelOptimizer : IDisposable
{
    private readonly ILogger _logger;
    private readonly CpuThreadPool _threadPool;
    private readonly Dictionary<string, OptimizationProfile> _profileCache;
    private readonly Types.PerformanceCounter _performanceCounter;
    private bool _disposed;

    // Optimization thresholds and constants
    private const int MinWorkItemsForVectorization = 64;
    private const int MinWorkItemsForParallelization = 16;
    /// <summary>
    /// Initializes a new instance of the CpuKernelOptimizer class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="threadPool">The thread pool.</param>

    public CpuKernelOptimizer(ILogger logger, CpuThreadPool threadPool)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _threadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        _profileCache = [];
        _performanceCounter = new Types.PerformanceCounter();

        LogOptimizerInitialized(_logger);
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 7200,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "CpuKernelOptimizer initialized")]
    private static partial void LogOptimizerInitialized(ILogger logger);

    [LoggerMessage(
        EventId = 7201,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Creating optimized execution plan for kernel {KernelName} with {Optimization} optimization")]
    private static partial void LogCreatingExecutionPlan(ILogger logger, string kernelName, OptimizationLevel optimization);

    [LoggerMessage(
        EventId = 7202,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Using cached optimization profile for kernel {KernelName}")]
    private static partial void LogUsingCachedProfile(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7203,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Optimization plan created for kernel {KernelName} in {Time:F2}ms")]
    private static partial void LogOptimizationPlanCreated(ILogger logger, string kernelName, double time);

    [LoggerMessage(
        EventId = 7204,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Failed to create optimization plan for kernel {KernelName}")]
    private static partial void LogOptimizationPlanFailed(ILogger logger, Exception exception, string kernelName);

    [LoggerMessage(
        EventId = 7205,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Analyzing performance for kernel {KernelName}")]
    private static partial void LogAnalyzingPerformance(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7206,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Performance analysis completed for kernel {KernelName} with {Count} recommendations")]
    private static partial void LogPerformanceAnalysisCompleted(ILogger logger, string kernelName, int count);

    [LoggerMessage(
        EventId = 7207,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Performance analysis failed for kernel {KernelName}")]
    private static partial void LogPerformanceAnalysisFailed(ILogger logger, Exception exception, string kernelName);

    [LoggerMessage(
        EventId = 7208,
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Benchmarking execution strategies for kernel {KernelName}")]
    private static partial void LogBenchmarkingStrategies(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7209,
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Benchmark completed for kernel {KernelName}: Optimal={Optimal}")]
    private static partial void LogBenchmarkCompleted(ILogger logger, string kernelName, string optimal);

    [LoggerMessage(
        EventId = 7210,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Benchmarking failed for kernel {KernelName}")]
    private static partial void LogBenchmarkingFailed(ILogger logger, Exception exception, string kernelName);

    [LoggerMessage(
        EventId = 7211,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Applying dynamic optimizations for kernel {KernelName}")]
    private static partial void LogApplyingDynamicOptimizations(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7212,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Thread pool configuration optimized for kernel {KernelName}")]
    private static partial void LogThreadPoolOptimized(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7213,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Vectorization settings optimized for kernel {KernelName}")]
    private static partial void LogVectorizationOptimized(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7214,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Dynamic optimization failed for kernel {KernelName}")]
    private static partial void LogDynamicOptimizationFailed(ILogger logger, Exception exception, string kernelName);

    #endregion

    /// <summary>
    /// Creates an optimized execution plan for the given kernel definition.
    /// </summary>
    public async Task<KernelExecutionPlan> CreateOptimizedExecutionPlanAsync(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        OptimizationLevel optimizationLevel = OptimizationLevel.O2)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(definition);

        LogCreatingExecutionPlan(_logger, definition.Name, optimizationLevel);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check cache for existing optimization profile
            var cacheKey = GenerateCacheKey(definition, workDimensions, optimizationLevel);
            if (_profileCache.TryGetValue(cacheKey, out var cachedProfile))
            {
                LogUsingCachedProfile(_logger, definition.Name);
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
            LogOptimizationPlanCreated(_logger, definition.Name, stopwatch.Elapsed.TotalMilliseconds);

            return executionPlan;
        }
        catch (Exception ex)
        {
            LogOptimizationPlanFailed(_logger, ex, definition.Name);
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

        LogAnalyzingPerformance(_logger, definition.Name);

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

            LogPerformanceAnalysisCompleted(_logger, definition.Name, recommendations.Suggestions.Count);

            return recommendations;
        }
        catch (Exception ex)
        {
            LogPerformanceAnalysisFailed(_logger, ex, definition.Name);
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

        LogBenchmarkingStrategies(_logger, definition.Name);

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
            var parallelizationResults = await BenchmarkParallelizationAsync(definition, workDimensions, iterations);
            foreach (var kvp in parallelizationResults)
            {
                results.ParallelizationResults[kvp.Key] = kvp.Value;
            }

            // Determine optimal configuration
            results.OptimalConfiguration = DetermineOptimalConfiguration(results);

            LogBenchmarkCompleted(_logger, definition.Name, results.OptimalConfiguration.Description);

            return results;
        }
        catch (Exception ex)
        {
            LogBenchmarkingFailed(_logger, ex, definition.Name);
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
            LogApplyingDynamicOptimizations(_logger, kernelName);

            var optimizationsApplied = false;

            // Check if performance has degraded
            if (await DetectPerformanceDegradationAsync(kernelName, currentStats))
            {
                // Attempt to optimize thread pool configuration
                if (await OptimizeThreadPoolConfigurationAsync(currentPlan))
                {
                    optimizationsApplied = true;
                    LogThreadPoolOptimized(_logger, kernelName);
                }

                // Attempt to adjust vectorization settings
                if (await OptimizeVectorizationSettingsAsync(currentPlan, currentStats))
                {
                    optimizationsApplied = true;
                    LogVectorizationOptimized(_logger, kernelName);
                }
            }

            return optimizationsApplied;
        }
        catch (Exception ex)
        {
            LogDynamicOptimizationFailed(_logger, ex, kernelName);
            return false;
        }
    }

    // Private analysis methods

    private static Task<KernelAnalysis> AnalyzeKernelAsync(KernelDefinition definition, WorkDimensions workDimensions)
    {
        var canVectorize = CanVectorize(definition);
        var optimalVectorWidth = DetermineOptimalVectorWidth(definition);

        var analysis = new KernelAnalysis
        {
            Definition = definition,
            WorkDimensions = workDimensions,
            TotalWorkItems = GetTotalWorkItems(workDimensions),
            CanVectorize = canVectorize,
            VectorizationFactor = canVectorize ? (optimalVectorWidth / 4) : 1,
            OptimalVectorWidth = optimalVectorWidth,
            MemoryAccessPattern = AnalyzeMemoryAccessPattern(definition),
            ComputeIntensity = ConvertToComputeIntensity(EstimateComputeIntensity(definition)),
            PreferredWorkGroupSize = DetermineOptimalWorkGroupSize(workDimensions),
            ThreadingOverhead = EstimateThreadingOverhead(workDimensions)
        };

        return Task.FromResult(analysis);
    }

    private Task<KernelExecutionPlan> CreateExecutionPlanAsync(
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
            WorkGroupSize = analysis.PreferredWorkGroupSize,
            MemoryPrefetchDistance = DetermineMemoryPrefetchDistance(analysis),
            EnableLoopUnrolling = optimizationLevel >= OptimizationLevel.O2,
            InstructionSets = GetAvailableInstructionSets(),
            MemoryOptimizations = DetermineMemoryOptimizations(analysis, optimizationLevel),
            CacheOptimizations = DetermineCacheOptimizations(analysis, optimizationLevel)
        };

        return Task.FromResult(plan);
    }

    private static Task AnalyzeVectorizationOpportunitiesAsync(
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
        return Task.CompletedTask;
    }

    private Task AnalyzeParallelizationEfficiencyAsync(
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
        return Task.CompletedTask;
    }

    private static Task AnalyzeMemoryAccessPatternsAsync(
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

        return Task.CompletedTask;
    }

    private static Task AnalyzeCacheUtilizationAsync(
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

        return Task.CompletedTask;
    }

    // Helper methods for optimization logic

    private static bool CanVectorize(KernelDefinition definition)
    {
        if (!Vector.IsHardwareAccelerated)
        {
            return false;
        }

        var name = definition.Name;
        // Element-wise operations, reductions, and linear algebra are typically vectorizable
        return name.Contains("Vector", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Add", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Multiply", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Scale", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Transform", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Reduce", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Dot", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Norm", StringComparison.OrdinalIgnoreCase)
            || name.Contains("SAXPY", StringComparison.OrdinalIgnoreCase)
            || name.Contains("BLAS", StringComparison.OrdinalIgnoreCase);
    }

    private static int DetermineOptimalVectorWidth(KernelDefinition definition) => Vector<float>.Count; // Use the platform's preferred vector width

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
        var name = definition.Name;
        // Matrix operations and FFT are compute-intensive
        if (name.Contains("Matrix", StringComparison.OrdinalIgnoreCase)
            || name.Contains("FFT", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Convolution", StringComparison.OrdinalIgnoreCase))
        {
            return 0.9;
        }

        // Reductions and dot products have moderate intensity
        if (name.Contains("Reduce", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Dot", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Norm", StringComparison.OrdinalIgnoreCase))
        {
            return 0.6;
        }

        // Copy/fill are memory-bound (low compute intensity)
        if (name.Contains("Copy", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Fill", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Memset", StringComparison.OrdinalIgnoreCase))
        {
            return 0.1;
        }

        return 0.5; // Default moderate intensity
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

    private static long GetTotalWorkItems(WorkDimensions dimensions) => dimensions.X * dimensions.Y * dimensions.Z;

    private static SimdSummary CreateSimdSummary()
    {
        var instructionSets = new HashSet<string>();
        if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
        {
            instructionSets.Add("SSE");
        }

        if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
        {
            instructionSets.Add("SSE2");
        }

        if (System.Runtime.Intrinsics.X86.Sse41.IsSupported)
        {
            instructionSets.Add("SSE4.1");
        }

        if (System.Runtime.Intrinsics.X86.Sse42.IsSupported)
        {
            instructionSets.Add("SSE4.2");
        }

        if (System.Runtime.Intrinsics.X86.Avx.IsSupported)
        {
            instructionSets.Add("AVX");
        }

        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            instructionSets.Add("AVX2");
        }

        if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported)
        {
            instructionSets.Add("AVX-512F");
        }

        if (System.Runtime.Intrinsics.X86.Avx512BW.IsSupported)
        {
            instructionSets.Add("AVX-512BW");
        }

        if (System.Runtime.Intrinsics.X86.Fma.IsSupported)
        {
            instructionSets.Add("FMA");
        }

        if (System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
        {
            instructionSets.Add("NEON");
        }

        return new SimdSummary
        {
            IsHardwareAccelerated = Vector.IsHardwareAccelerated,
            PreferredVectorWidth = Vector<float>.Count,
            SupportedInstructionSets = instructionSets
        };
    }

    private KernelExecutionPlan CreateBasicExecutionPlan(KernelDefinition definition, WorkDimensions workDimensions, bool useVectorization)
    {
        var vectorWidth = Vector<float>.Count;
        return new KernelExecutionPlan
        {
            Analysis = new KernelAnalysis
            {
                Definition = definition,
                WorkDimensions = workDimensions,
                CanVectorize = useVectorization,
                VectorizationFactor = useVectorization ? vectorWidth : 1,
                MemoryAccessPattern = MemoryAccessPattern.Sequential,
                ComputeIntensity = ComputeIntensity.Medium,
                PreferredWorkGroupSize = 64
            },
            UseVectorization = useVectorization,
            UseParallelization = true,
            OptimalThreadCount = _threadPool.MaxConcurrency,
            VectorizationFactor = useVectorization ? vectorWidth : 1,
            VectorWidth = vectorWidth,
            WorkGroupSize = 64,
            MemoryPrefetchDistance = 8,
            EnableLoopUnrolling = true,
            InstructionSets = GetAvailableInstructionSets()
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

            // Simulate kernel workload proportional to plan characteristics
            var workItems = plan.Analysis?.TotalWorkItems ?? 1000;
            var batchSize = Math.Min(workItems, 10000);
            var data = new float[batchSize];
            if (plan.UseVectorization && Vector.IsHardwareAccelerated)
            {
                // Vectorized benchmark workload
                var vec = new Vector<float>(1.0001f);
                for (long j = 0; j < batchSize - Vector<float>.Count; j += Vector<float>.Count)
                {
                    vec *= new Vector<float>(data.AsSpan().Slice((int)j));
                }
            }
            else
            {
                // Scalar benchmark workload
                for (var j = 0; j < batchSize; j++)
                {
                    data[j] = data[j] * 1.0001f + 0.0001f;
                }
            }
            await Task.CompletedTask;

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
            var basePlan = CreateBasicExecutionPlan(definition, workDimensions, false);
            var plan = new KernelExecutionPlan
            {
                Analysis = basePlan.Analysis,
                UseVectorization = basePlan.UseVectorization,
                UseParallelization = basePlan.UseParallelization,
                VectorWidth = basePlan.VectorWidth,
                VectorizationFactor = basePlan.VectorizationFactor,
                WorkGroupSize = basePlan.WorkGroupSize,
                OptimalThreadCount = threadCount,
                MemoryPrefetchDistance = basePlan.MemoryPrefetchDistance,
                EnableLoopUnrolling = basePlan.EnableLoopUnrolling,
                InstructionSets = basePlan.InstructionSets,
                MemoryOptimizations = basePlan.MemoryOptimizations,
                CacheOptimizations = basePlan.CacheOptimizations
            };

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

    private static OptimalConfiguration DetermineOptimalConfiguration(BenchmarkResults results)
    {
        var bestPerformance = results.ScalarPerformance;
        var bestDescription = "Scalar execution";

        if (results.VectorizedPerformance != null && bestPerformance != null &&
            results.VectorizedPerformance.AverageTimeMs < bestPerformance.AverageTimeMs)
        {
            bestPerformance = results.VectorizedPerformance;
            bestDescription = "Vectorized execution";
        }

        // Fallback if no performance metrics available
        bestPerformance ??= new PerformanceMetrics { AverageTimeMs = 0, Timestamp = DateTimeOffset.UtcNow };

        return new OptimalConfiguration
        {
            Description = bestDescription,
            Performance = bestPerformance,
            UseVectorization = bestDescription.Contains("Vectorized", StringComparison.OrdinalIgnoreCase),
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

    private static Task<bool> DetectPerformanceDegradationAsync(string kernelName, ExecutionStatistics currentStats)
    {
        // Check for degradation: high variance or execution time exceeding 2x the minimum
        if (currentStats.MinExecutionTimeMs > 0 && currentStats.AverageExecutionTimeMs > currentStats.MinExecutionTimeMs * 2)
        {
            return Task.FromResult(true);
        }

        // High coefficient of variation indicates unstable performance
        if (currentStats.AverageExecutionTimeMs > 0 && currentStats.MaxExecutionTimeMs > currentStats.AverageExecutionTimeMs * 3)
        {
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private static Task<bool> OptimizeThreadPoolConfigurationAsync(KernelExecutionPlan plan)
    {
        // Suggest thread count adjustment based on work items vs current thread count
        var totalWorkItems = plan.Analysis?.TotalWorkItems ?? 0;
        if (totalWorkItems <= 0)
        {
            return Task.FromResult(false);
        }

        var optimalThreads = Math.Max(1, Math.Min(
            (int)Math.Ceiling(totalWorkItems / 256.0),
            Environment.ProcessorCount));

        if (plan.OptimalThreadCount != optimalThreads)
        {
            plan.OptimalThreadCount = optimalThreads;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private static Task<bool> OptimizeVectorizationSettingsAsync(KernelExecutionPlan plan, ExecutionStatistics stats)
    {
        // Enable vectorization if not already enabled and execution is slow enough to benefit
        if (!plan.UseVectorization && Vector.IsHardwareAccelerated && stats.AverageExecutionTimeMs > 1.0)
        {
            plan.UseVectorization = true;
            plan.VectorizationFactor = Vector<float>.Count;
            return Task.FromResult(true);
        }
        // Disable vectorization if it's making things worse (very low utilization)
        if (plan.UseVectorization && stats.AverageExecutionTimeMs < 0.01)
        {
            plan.UseVectorization = false;
            plan.VectorizationFactor = 1;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    // Utility methods for analysis

    private static bool ShouldUseVectorization(KernelAnalysis analysis, OptimizationLevel level)
    {
        return analysis.CanVectorize &&
               analysis.TotalWorkItems >= MinWorkItemsForVectorization &&
               level >= OptimizationLevel.O2;
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
            OptimizationLevel.O1 => Math.Min(2, maxThreads),
            OptimizationLevel.O2 => Math.Min((int)Math.Ceiling(totalWorkItems / 100.0), maxThreads),
            OptimizationLevel.O3 => maxThreads,
            _ => Math.Min(4, maxThreads)
        };
    }

    private int DetermineOptimalThreadCount(long totalWorkItems)
    {
        var maxThreads = _threadPool.MaxConcurrency;
        var optimalThreads = Math.Min((int)Math.Ceiling(totalWorkItems / 100.0), maxThreads);
        return Math.Max(1, optimalThreads);
    }

    private static int DetermineVectorizationFactor(KernelAnalysis analysis) => analysis.CanVectorize ? analysis.OptimalVectorWidth : 1;

    private static List<string> DetermineMemoryOptimizations(KernelAnalysis analysis, OptimizationLevel level)
    {
        var optimizations = new List<string>();

        if (level >= OptimizationLevel.O2)
        {
            if (analysis.MemoryAccessPattern == MemoryAccessPattern.Random)
            {
                optimizations.Add("EnableMemoryPrefetching");
            }

            if (analysis.ComputeIntensity <= ComputeIntensity.Low)
            {
                optimizations.Add("OptimizeDataLayout");
            }
        }

        return optimizations;
    }

    private static List<string> DetermineCacheOptimizations(KernelAnalysis analysis, OptimizationLevel level)
    {
        var optimizations = new List<string>();

        if (level >= OptimizationLevel.O3)
        {
            optimizations.Add("EnableCachePrefetching");
            optimizations.Add("OptimizeDataLocality");
        }

        return optimizations;
    }

    private static double EstimateVectorizationSpeedup(KernelDefinition definition)
        // Simplified estimation based on kernel characteristics

        => Vector<float>.Count * 0.8; // Assume 80% of theoretical speedup

    private static double EstimateParallelizationSpeedup(int optimalThreads, int currentThreads) => Math.Min(optimalThreads / (double)currentThreads, Environment.ProcessorCount);

    private static double CalculateVectorizationEfficiency(ExecutionStatistics statistics)
    {
        if (!statistics.UseVectorization || statistics.AverageExecutionTimeMs <= 0)
        {
            return 0.0;
        }

        // Compare vectorized vs theoretical speedup
        // Higher throughput per ms indicates better vectorization
        // If we have no baseline, estimate from the execution time distribution
        // Lower standard deviation relative to mean suggests consistent vectorization
        if (statistics.MinExecutionTimeMs > 0 && statistics.MaxExecutionTimeMs > 0)
        {
            var consistency = statistics.MinExecutionTimeMs / statistics.MaxExecutionTimeMs;
            return Math.Clamp(consistency, 0.0, 1.0);
        }

        return 0.8; // Default when insufficient data
    }

    private static double EstimateCacheUtilization(KernelDefinition definition, ExecutionStatistics statistics)
    {
        var accessPattern = AnalyzeMemoryAccessPattern(definition);
        var baseUtilization = accessPattern switch
        {
            MemoryAccessPattern.Sequential => 0.9, // Sequential access => good cache behavior
            MemoryAccessPattern.Strided => 0.5,    // Strided => moderate cache misses
            MemoryAccessPattern.Random => 0.2,     // Random => poor cache utilization
            _ => 0.7
        };

        // Larger workloads with vectorization tend to have better cache utilization
        if (statistics.UseVectorization)
        {
            baseUtilization = Math.Min(1.0, baseUtilization + 0.1);
        }

        return baseUtilization;
    }

    private static void GenerateOptimizationSuggestions(OptimizationRecommendations recommendations)
    {
        // Convert to a list for sorting (IList doesn't guarantee mutability for Sort)
        var suggestionsList = recommendations.Suggestions.ToList();

        // Sort suggestions by expected speedup
        suggestionsList.Sort((a, b) => b.ExpectedSpeedup.CompareTo(a.ExpectedSpeedup));

        // Limit to top 5 suggestions
        if (suggestionsList.Count > 5)
        {
            // Clear the collection and add back only the top 5
            recommendations.Suggestions.Clear();
            foreach (var suggestion in suggestionsList.Take(5))
            {
                recommendations.Suggestions.Add(suggestion);
            }
        }
        else if (suggestionsList.Count != recommendations.Suggestions.Count)
        {
            // If the collection was already sorted, replace it
            recommendations.Suggestions.Clear();
            foreach (var suggestion in suggestionsList)
            {
                recommendations.Suggestions.Add(suggestion);
            }
        }
    }

    private static string GenerateCacheKey(KernelDefinition definition, WorkDimensions workDimensions, OptimizationLevel level) => $"{definition.Name}_{workDimensions.X}x{workDimensions.Y}x{workDimensions.Z}_{level}";

    // Helper methods for missing functionality
    private static ComputeIntensity ConvertToComputeIntensity(double intensity)
    {
        return intensity switch
        {
            < 0.3 => ComputeIntensity.Low,
            < 0.6 => ComputeIntensity.Medium,
            < 0.9 => ComputeIntensity.High,
            _ => ComputeIntensity.VeryHigh
        };
    }

    private static int DetermineOptimalWorkGroupSize(WorkDimensions workDimensions)
    {
        // Use power of 2 work group sizes for optimal cache line alignment
        var totalWork = workDimensions.X * workDimensions.Y * workDimensions.Z;
        return totalWork switch
        {
            < 64 => 16,
            < 256 => 32,
            < 1024 => 64,
            < 4096 => 128,
            _ => 256
        };
    }

    private static int DetermineMemoryPrefetchDistance(KernelAnalysis analysis)
    {
        // Prefetch distance based on memory access pattern
        return analysis.MemoryAccessPattern switch
        {
            MemoryAccessPattern.Sequential => 16,
            MemoryAccessPattern.Strided => 8,
            MemoryAccessPattern.Random => 4,
            _ => 8
        };
    }

    private static IReadOnlySet<string> GetAvailableInstructionSets()
    {
        var sets = new HashSet<string>();
        if (Vector.IsHardwareAccelerated)
        {
            _ = sets.Add("SIMD");
        }
        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            _ = sets.Add("AVX2");
        }
        if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported)
        {
            _ = sets.Add("AVX512");
        }
        return sets;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

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

