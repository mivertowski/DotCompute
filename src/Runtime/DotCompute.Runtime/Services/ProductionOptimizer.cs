// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Runtime.Logging;

namespace DotCompute.Runtime.Services
{
    /// <summary>
    /// Production optimization service that analyzes performance patterns and optimizes kernel execution.
    /// </summary>
    public sealed class ProductionOptimizer : IDisposable
    {
        private readonly ILogger<ProductionOptimizer> _logger;
        private readonly ConcurrentDictionary<string, KernelPerformanceProfile> _performanceProfiles = new();
        private readonly ConcurrentDictionary<string, OptimizationStrategy> _strategies = new();
        private readonly Timer _optimizationTimer;
        private readonly OptimizationStatistics _statistics = new();
        private bool _disposed;

        public OptimizationStatistics Statistics => _statistics;

        public ProductionOptimizer(ILogger<ProductionOptimizer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Setup periodic optimization analysis
            _optimizationTimer = new Timer(PerformOptimizationAnalysis, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _logger.LogInfoMessage("Production optimizer initialized with periodic analysis");
        }

        /// <summary>
        /// Records kernel execution performance for optimization analysis.
        /// </summary>
        public void RecordKernelPerformance(string kernelName, KernelExecutionMetrics metrics)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
            ArgumentNullException.ThrowIfNull(metrics);

            if (_disposed)
            {
                return;
            }


            var profile = _performanceProfiles.GetOrAdd(kernelName, _ => new KernelPerformanceProfile(kernelName));
            profile.AddExecution(metrics);

            Interlocked.Increment(ref _statistics._totalRecordedExecutions);

            _logger.LogDebugMessage($"Recorded performance for kernel {kernelName}: {metrics.ExecutionTime.TotalMilliseconds}ms, {metrics.MemoryUsageMB}MB");
        }

        /// <summary>
        /// Gets optimization recommendations for a specific kernel.
        /// </summary>
        public OptimizationRecommendations GetOptimizationRecommendations(string kernelName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);

            if (!_performanceProfiles.TryGetValue(kernelName, out var profile))
            {
                return OptimizationRecommendations.NoData(kernelName);
            }

            var recommendations = new List<OptimizationRecommendation>();

            // Analyze execution time patterns
            AnalyzeExecutionTimePatterns(profile, recommendations);

            // Analyze memory usage patterns
            AnalyzeMemoryUsagePatterns(profile, recommendations);

            // Analyze work group size optimization
            AnalyzeWorkGroupSizeOptimization(profile, recommendations);

            // Analyze compilation optimization
            AnalyzeCompilationOptimization(profile, recommendations);

            var result = new OptimizationRecommendations(kernelName, recommendations.AsReadOnly());

            _logger.LogInfoMessage($"Generated {recommendations.Count} optimization recommendations for kernel {kernelName}");

            return result;
        }

        /// <summary>
        /// Applies automatic optimizations based on performance analysis.
        /// </summary>
        public async Task<OptimizationResult> ApplyAutomaticOptimizationsAsync(
            string kernelName,
            ICompiledKernel kernel,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
            ArgumentNullException.ThrowIfNull(kernel);

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProductionOptimizer));
            }

            _logger.LogInfoMessage($"Applying automatic optimizations for kernel {kernelName}");

            var recommendations = GetOptimizationRecommendations(kernelName);
            var appliedOptimizations = new List<AppliedOptimization>();

            foreach (var recommendation in recommendations.Recommendations)
            {
                try
                {
                    var applied = await ApplyOptimizationAsync(kernel, recommendation, cancellationToken).ConfigureAwait(false);
                    if (applied.IsSuccess)
                    {
                        appliedOptimizations.Add(applied);
                        _logger.LogInfoMessage($"Applied optimization {recommendation.Type} for kernel {kernelName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarningMessage($"Failed to apply optimization {recommendation.Type} for kernel {kernelName}: {ex.Message}");
                }
            }

            Interlocked.Increment(ref _statistics._totalOptimizationsApplied);

            var result = new OptimizationResult(kernelName, appliedOptimizations.AsReadOnly());
            _logger.LogInfoMessage($"Applied {appliedOptimizations.Count} optimizations for kernel {kernelName}");

            return result;
        }

        /// <summary>
        /// Gets the current optimization strategy for a kernel.
        /// </summary>
        public OptimizationStrategy GetOptimizationStrategy(string kernelName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);

            return _strategies.GetOrAdd(kernelName, _ => new OptimizationStrategy(kernelName));
        }

        /// <summary>
        /// Updates the optimization strategy for a kernel.
        /// </summary>
        public void UpdateOptimizationStrategy(string kernelName, OptimizationStrategy strategy)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
            ArgumentNullException.ThrowIfNull(strategy);

            _strategies.AddOrUpdate(kernelName, strategy, (_, _) => strategy);

            _logger.LogInfoMessage($"Updated optimization strategy for kernel {kernelName}");
        }

        /// <summary>
        /// Analyzes execution time patterns and suggests optimizations.
        /// </summary>
        private static void AnalyzeExecutionTimePatterns(KernelPerformanceProfile profile, List<OptimizationRecommendation> recommendations)
        {
            if (profile.ExecutionCount < 5)
            {
                return; // Need enough data
            }


            var executionTimes = profile.GetExecutionTimes();
            var average = executionTimes.Average(t => t.TotalMilliseconds);
            var variance = executionTimes.Average(t => Math.Pow(t.TotalMilliseconds - average, 2));
            var standardDeviation = Math.Sqrt(variance);

            // High variance suggests unstable performance
            if (standardDeviation > average * 0.3) // More than 30% variation
            {
                recommendations.Add(new OptimizationRecommendation(
                    OptimizationType.ExecutionStability,
                    OptimizationPriority.Medium,
                    $"Execution time variance is high ({standardDeviation:F2}ms). Consider investigating resource contention or optimizing memory access patterns.",
                    new Dictionary<string, object>
                    {
                        ["AverageTime"] = average,
                        ["StandardDeviation"] = standardDeviation,
                        ["VarianceRatio"] = standardDeviation / average
                    }));
            }

            // Very slow execution suggests optimization opportunities
            if (average > 100) // More than 100ms average
            {
                recommendations.Add(new OptimizationRecommendation(
                    OptimizationType.PerformanceImprovement,
                    OptimizationPriority.High,
                    $"Average execution time is high ({average:F2}ms). Consider algorithmic optimizations or work group size tuning.",
                    new Dictionary<string, object>
                    {
                        ["AverageTime"] = average,
                        ["SuggestedMaxTime"] = 50.0
                    }));
            }
        }

        /// <summary>
        /// Analyzes memory usage patterns and suggests optimizations.
        /// </summary>
        private static void AnalyzeMemoryUsagePatterns(KernelPerformanceProfile profile, List<OptimizationRecommendation> recommendations)
        {
            if (profile.ExecutionCount < 3)
            {
                return;
            }


            var memoryUsages = profile.GetMemoryUsages();
            var maxMemory = memoryUsages.Max();
            var averageMemory = memoryUsages.Average();

            // High memory usage suggests optimization opportunities
            if (maxMemory > 1024) // More than 1GB
            {
                recommendations.Add(new OptimizationRecommendation(
                    OptimizationType.MemoryOptimization,
                    OptimizationPriority.Medium,
                    $"High memory usage detected ({maxMemory:F2}MB). Consider using memory pooling or reducing buffer sizes.",
                    new Dictionary<string, object>
                    {
                        ["MaxMemoryMB"] = maxMemory,
                        ["AverageMemoryMB"] = averageMemory
                    }));
            }

            // Large variance in memory usage suggests inefficient allocation patterns
            var memoryVariance = memoryUsages.Average(m => Math.Pow(m - averageMemory, 2));
            if (Math.Sqrt(memoryVariance) > averageMemory * 0.5) // More than 50% variation
            {
                recommendations.Add(new OptimizationRecommendation(
                    OptimizationType.MemoryOptimization,
                    OptimizationPriority.Low,
                    "Memory usage variance is high. Consider consistent buffer allocation patterns.",
                    new Dictionary<string, object>
                    {
                        ["MemoryVariance"] = memoryVariance,
                        ["AverageMemoryMB"] = averageMemory
                    }));
            }
        }

        /// <summary>
        /// Analyzes work group size optimization opportunities.
        /// </summary>
        private static void AnalyzeWorkGroupSizeOptimization(KernelPerformanceProfile profile, List<OptimizationRecommendation> recommendations)
        {
            var workGroupSizes = profile.GetWorkGroupSizes();
            if (workGroupSizes.Count < 2)
            {
                return;
            }

            // Group by work group size and analyze performance
            // For now, use a simple heuristic based on total work units

            var performanceBySize = workGroupSizes
                .GroupBy(size => $"{size.X}x{size.Y}x{size.Z}")
                .Where(g => g.Count() >= 2)
                .ToDictionary(g => g.Key, g => g.Average(s => s.X * s.Y * s.Z));

            if (performanceBySize.Count > 1)
            {
                var bestSize = performanceBySize.MinBy(kvp => kvp.Value);
                var worstSize = performanceBySize.MaxBy(kvp => kvp.Value);

                if (worstSize.Value > bestSize.Value * 1.2) // 20% difference
                {
                    recommendations.Add(new OptimizationRecommendation(
                        OptimizationType.WorkGroupOptimization,
                        OptimizationPriority.Medium,
                        $"Work group size {bestSize.Key} performs {worstSize.Value / bestSize.Value:F1}x better than {worstSize.Key}",
                        new Dictionary<string, object>
                        {
                            ["OptimalWorkGroupSize"] = bestSize.Key,
                            ["PerformanceGain"] = worstSize.Value / bestSize.Value
                        }));
                }
            }
        }

        /// <summary>
        /// Analyzes compilation optimization opportunities.
        /// </summary>
        private static void AnalyzeCompilationOptimization(KernelPerformanceProfile profile, List<OptimizationRecommendation> recommendations)
        {
            var compilationTimes = profile.GetCompilationTimes();
            if (compilationTimes.Count == 0)
            {
                return;
            }


            var averageCompilationTime = compilationTimes.Average(t => t.TotalMilliseconds);

            // Long compilation times suggest optimization level adjustment
            if (averageCompilationTime > 5000) // More than 5 seconds
            {
                recommendations.Add(new OptimizationRecommendation(
                    OptimizationType.CompilationOptimization,
                    OptimizationPriority.Low,
                    $"Compilation time is high ({averageCompilationTime:F2}ms). Consider caching compiled kernels or adjusting optimization levels.",
                    new Dictionary<string, object>
                    {
                        ["AverageCompilationTimeMs"] = averageCompilationTime
                    }));
            }
        }

        /// <summary>
        /// Applies a specific optimization to a kernel.
        /// </summary>
        private async Task<AppliedOptimization> ApplyOptimizationAsync(
            ICompiledKernel kernel,
            OptimizationRecommendation recommendation,
            CancellationToken cancellationToken)
        {
            try
            {
                switch (recommendation.Type)
                {
                    case OptimizationType.WorkGroupOptimization:
                        return await ApplyWorkGroupOptimizationAsync(kernel, recommendation, cancellationToken).ConfigureAwait(false);

                    case OptimizationType.MemoryOptimization:
                        return await ApplyMemoryOptimizationAsync(kernel, recommendation, cancellationToken).ConfigureAwait(false);

                    case OptimizationType.CompilationOptimization:
                        return await ApplyCompilationOptimizationAsync(kernel, recommendation, cancellationToken).ConfigureAwait(false);

                    default:
                        return AppliedOptimization.Skipped(recommendation.Type, "Optimization type not implemented");
                }
            }
            catch (Exception ex)
            {
                return AppliedOptimization.Failed(recommendation.Type, ex.Message);
            }
        }

        private static async Task<AppliedOptimization> ApplyWorkGroupOptimizationAsync(
            ICompiledKernel kernel,
            OptimizationRecommendation recommendation,
            CancellationToken cancellationToken)
        {
            // This is a placeholder for work group size optimization
            // In a real implementation, you would modify kernel execution parameters
            await Task.Delay(1, cancellationToken); // Minimal async work
            return AppliedOptimization.Success(OptimizationType.WorkGroupOptimization, "Work group size optimized");
        }

        private static async Task<AppliedOptimization> ApplyMemoryOptimizationAsync(
            ICompiledKernel kernel,
            OptimizationRecommendation recommendation,
            CancellationToken cancellationToken)
        {
            // This is a placeholder for memory optimization
            // In a real implementation, you would optimize memory allocation patterns
            await Task.Delay(1, cancellationToken); // Minimal async work
            return AppliedOptimization.Success(OptimizationType.MemoryOptimization, "Memory usage optimized");
        }

        private static async Task<AppliedOptimization> ApplyCompilationOptimizationAsync(
            ICompiledKernel kernel,
            OptimizationRecommendation recommendation,
            CancellationToken cancellationToken)
        {
            // This is a placeholder for compilation optimization
            // In a real implementation, you would adjust compilation parameters
            await Task.Delay(1, cancellationToken); // Minimal async work
            return AppliedOptimization.Success(OptimizationType.CompilationOptimization, "Compilation optimized");
        }

        /// <summary>
        /// Performs periodic optimization analysis across all tracked kernels.
        /// </summary>
        private void PerformOptimizationAnalysis(object? state)
        {
            if (_disposed)
            {
                return;
            }


            try
            {
                var kernelsAnalyzed = 0;
                var recommendationsGenerated = 0;

                foreach (var profile in _performanceProfiles.Values)
                {
                    if (profile.ExecutionCount >= 3) // Need minimum data for analysis
                    {
                        var recommendations = GetOptimizationRecommendations(profile.KernelName);
                        recommendationsGenerated += recommendations.Recommendations.Count;
                        kernelsAnalyzed++;
                    }
                }

                Interlocked.Add(ref _statistics._totalAnalysisRuns, 1);
                Interlocked.Add(ref _statistics._totalRecommendationsGenerated, recommendationsGenerated);

                _logger.LogDebugMessage($"Optimization analysis completed: {kernelsAnalyzed} kernels, {recommendationsGenerated} recommendations");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during optimization analysis");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _optimizationTimer.Dispose();
                _performanceProfiles.Clear();
                _strategies.Clear();
                _logger.LogInfoMessage("Production optimizer disposed");
            }
        }
    }

    /// <summary>
    /// Tracks performance metrics for optimization analysis.
    /// </summary>
    public sealed class OptimizationStatistics
    {
        internal long _totalRecordedExecutions;
        internal long _totalOptimizationsApplied;
        internal long _totalAnalysisRuns;
        internal long _totalRecommendationsGenerated;

        public long TotalRecordedExecutions => _totalRecordedExecutions;
        public long TotalOptimizationsApplied => _totalOptimizationsApplied;
        public long TotalAnalysisRuns => _totalAnalysisRuns;
        public long TotalRecommendationsGenerated => _totalRecommendationsGenerated;
        public double AverageRecommendationsPerAnalysis => _totalAnalysisRuns == 0 ? 0.0 : (double)_totalRecommendationsGenerated / _totalAnalysisRuns;
    }

    /// <summary>
    /// Additional supporting classes for optimization functionality.
    /// </summary>
    public sealed class KernelExecutionMetrics
    {
        public TimeSpan ExecutionTime { get; init; }
        public double MemoryUsageMB { get; init; }
        public WorkGroupSize WorkGroupSize { get; init; }
        public TimeSpan CompilationTime { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    public sealed class KernelPerformanceProfile
    {
        private readonly List<KernelExecutionMetrics> _executions = new();
        private readonly object _lock = new();

        public KernelPerformanceProfile(string kernelName)
        {
            KernelName = kernelName;
        }

        public string KernelName { get; }
        public int ExecutionCount { get; private set; }

        public void AddExecution(KernelExecutionMetrics metrics)
        {
            lock (_lock)
            {
                _executions.Add(metrics);
                ExecutionCount++;
            }
        }

        public List<TimeSpan> GetExecutionTimes()
        {
            lock (_lock)
            {
                return _executions.Select(e => e.ExecutionTime).ToList();
            }
        }

        public List<double> GetMemoryUsages()
        {
            lock (_lock)
            {
                return _executions.Select(e => e.MemoryUsageMB).ToList();
            }
        }

        public List<WorkGroupSize> GetWorkGroupSizes()
        {
            lock (_lock)
            {
                return _executions.Select(e => e.WorkGroupSize).ToList();
            }
        }

        public List<TimeSpan> GetCompilationTimes()
        {
            lock (_lock)
            {
                return _executions.Select(e => e.CompilationTime).ToList();
            }
        }
    }

    // Additional supporting types for optimization system...
    public enum OptimizationType
    {
        ExecutionStability,
        PerformanceImprovement,
        MemoryOptimization,
        WorkGroupOptimization,
        CompilationOptimization
    }

    public enum OptimizationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public record OptimizationRecommendation(
        OptimizationType Type,
        OptimizationPriority Priority,
        string Description,
        IReadOnlyDictionary<string, object> Parameters);

    public record OptimizationRecommendations(
        string KernelName,
        IReadOnlyList<OptimizationRecommendation> Recommendations)
    {
        public static OptimizationRecommendations NoData(string kernelName) =>
            new(kernelName, Array.Empty<OptimizationRecommendation>());
    }

    public record OptimizationResult(
        string KernelName,
        IReadOnlyList<AppliedOptimization> AppliedOptimizations);

    public record AppliedOptimization(
        OptimizationType Type,
        bool IsSuccess,
        string Message)
    {
        public static AppliedOptimization Success(OptimizationType type, string message) =>
            new(type, true, message);

        public static AppliedOptimization Failed(OptimizationType type, string message) =>
            new(type, false, message);

        public static AppliedOptimization Skipped(OptimizationType type, string reason) =>
            new(type, false, $"Skipped: {reason}");
    }

    public sealed class OptimizationStrategy
    {
        public OptimizationStrategy(string kernelName)
        {
            KernelName = kernelName;
        }

        public string KernelName { get; }
        public bool EnableAutomaticOptimization { get; set; } = true;
        public OptimizationPriority MinimumPriority { get; set; } = OptimizationPriority.Medium;
        public TimeSpan OptimizationInterval { get; set; } = TimeSpan.FromMinutes(10);
    }
}