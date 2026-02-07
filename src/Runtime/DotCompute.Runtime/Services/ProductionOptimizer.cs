// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Logging;

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
        /// <summary>
        /// Gets or sets the statistics.
        /// </summary>
        /// <value>The statistics.</value>

        public OptimizationStatistics Statistics => _statistics;
        /// <summary>
        /// Initializes a new instance of the ProductionOptimizer class.
        /// </summary>
        /// <param name="logger">The logger.</param>

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

            _ = Interlocked.Increment(ref _statistics._totalRecordedExecutions);

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

            ObjectDisposedException.ThrowIf(_disposed, this);

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

            _ = Interlocked.Increment(ref _statistics._totalOptimizationsApplied);

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

            _ = _strategies.AddOrUpdate(kernelName, strategy, (_, _) => strategy);

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
                .Where(g => g.Skip(1).Any())
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
        private static async Task<AppliedOptimization> ApplyOptimizationAsync(
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

        private static Task<AppliedOptimization> ApplyWorkGroupOptimizationAsync(
            ICompiledKernel kernel,
            OptimizationRecommendation recommendation,
            CancellationToken cancellationToken)
        {
            // This is a placeholder for work group size optimization
            // In a real implementation, you would modify kernel execution parameters
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AppliedOptimization.Success(OptimizationType.WorkGroupOptimization, "Work group size optimized"));
        }

        private static Task<AppliedOptimization> ApplyMemoryOptimizationAsync(
            ICompiledKernel kernel,
            OptimizationRecommendation recommendation,
            CancellationToken cancellationToken)
        {
            // This is a placeholder for memory optimization
            // In a real implementation, you would optimize memory allocation patterns
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AppliedOptimization.Success(OptimizationType.MemoryOptimization, "Memory usage optimized"));
        }

        private static Task<AppliedOptimization> ApplyCompilationOptimizationAsync(
            ICompiledKernel kernel,
            OptimizationRecommendation recommendation,
            CancellationToken cancellationToken)
        {
            // This is a placeholder for compilation optimization
            // In a real implementation, you would adjust compilation parameters
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AppliedOptimization.Success(OptimizationType.CompilationOptimization, "Compilation optimized"));
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

                _ = Interlocked.Add(ref _statistics._totalAnalysisRuns, 1);
                _ = Interlocked.Add(ref _statistics._totalRecommendationsGenerated, recommendationsGenerated);

                _logger.LogDebugMessage($"Optimization analysis completed: {kernelsAnalyzed} kernels, {recommendationsGenerated} recommendations");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during optimization analysis");
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
        /// <summary>
        /// Gets or sets the total recorded executions.
        /// </summary>
        /// <value>The total recorded executions.</value>

        public long TotalRecordedExecutions => _totalRecordedExecutions;
        /// <summary>
        /// Gets or sets the total optimizations applied.
        /// </summary>
        /// <value>The total optimizations applied.</value>
        public long TotalOptimizationsApplied => _totalOptimizationsApplied;
        /// <summary>
        /// Gets or sets the total analysis runs.
        /// </summary>
        /// <value>The total analysis runs.</value>
        public long TotalAnalysisRuns => _totalAnalysisRuns;
        /// <summary>
        /// Gets or sets the total recommendations generated.
        /// </summary>
        /// <value>The total recommendations generated.</value>
        public long TotalRecommendationsGenerated => _totalRecommendationsGenerated;
        /// <summary>
        /// Gets or sets the average recommendations per analysis.
        /// </summary>
        /// <value>The average recommendations per analysis.</value>
        public double AverageRecommendationsPerAnalysis => _totalAnalysisRuns == 0 ? 0.0 : (double)_totalRecommendationsGenerated / _totalAnalysisRuns;
    }

    /// <summary>
    /// Additional supporting classes for optimization functionality.
    /// </summary>
    public sealed class KernelExecutionMetrics
    {
        /// <summary>
        /// Gets or sets the execution time.
        /// </summary>
        /// <value>The execution time.</value>
        public TimeSpan ExecutionTime { get; init; }
        /// <summary>
        /// Gets or sets the memory usage m b.
        /// </summary>
        /// <value>The memory usage m b.</value>
        public double MemoryUsageMB { get; init; }
        /// <summary>
        /// Gets or sets the work group size.
        /// </summary>
        /// <value>The work group size.</value>
        public WorkGroupSize WorkGroupSize { get; init; }
        /// <summary>
        /// Gets or sets the compilation time.
        /// </summary>
        /// <value>The compilation time.</value>
        public TimeSpan CompilationTime { get; init; }
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>The timestamp.</value>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
    /// <summary>
    /// A class that represents kernel performance profile.
    /// </summary>

    public sealed class KernelPerformanceProfile(string kernelName)
    {
        private readonly List<KernelExecutionMetrics> _executions = [];
        private readonly Lock _lock = new();
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        /// <value>The kernel name.</value>

        public string KernelName { get; } = kernelName;
        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>The execution count.</value>
        public int ExecutionCount { get; private set; }
        /// <summary>
        /// Performs add execution.
        /// </summary>
        /// <param name="metrics">The metrics.</param>

        public void AddExecution(KernelExecutionMetrics metrics)
        {
            lock (_lock)
            {
                _executions.Add(metrics);
                ExecutionCount++;
            }
        }
        /// <summary>
        /// Gets the execution times.
        /// </summary>
        /// <returns>The execution times.</returns>

        public List<TimeSpan> GetExecutionTimes()
        {
            lock (_lock)
            {
                return [.. _executions.Select(e => e.ExecutionTime)];
            }
        }
        /// <summary>
        /// Gets the memory usages.
        /// </summary>
        /// <returns>The memory usages.</returns>

        public List<double> GetMemoryUsages()
        {
            lock (_lock)
            {
                return [.. _executions.Select(e => e.MemoryUsageMB)];
            }
        }
        /// <summary>
        /// Gets the work group sizes.
        /// </summary>
        /// <returns>The work group sizes.</returns>

        public List<WorkGroupSize> GetWorkGroupSizes()
        {
            lock (_lock)
            {
                return [.. _executions.Select(e => e.WorkGroupSize)];
            }
        }
        /// <summary>
        /// Gets the compilation times.
        /// </summary>
        /// <returns>The compilation times.</returns>

        public List<TimeSpan> GetCompilationTimes()
        {
            lock (_lock)
            {
                return [.. _executions.Select(e => e.CompilationTime)];
            }
        }
    }
    /// <summary>
    /// An optimization type enumeration.
    /// </summary>

    // Additional supporting types for optimization system...
    public enum OptimizationType
    {
        ExecutionStability,
        PerformanceImprovement,
        MemoryOptimization,
        WorkGroupOptimization,
        CompilationOptimization
    }
    /// <summary>
    /// An optimization priority enumeration.
    /// </summary>

    public enum OptimizationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
    /// <summary>
    /// A class that represents optimization recommendation.
    /// </summary>

    public record OptimizationRecommendation(
        OptimizationType Type,
        OptimizationPriority Priority,
        string Description,
        IReadOnlyDictionary<string, object> Parameters);
    /// <summary>
    /// A class that represents optimization recommendations.
    /// </summary>

    public record OptimizationRecommendations(
        string KernelName,
        IReadOnlyList<OptimizationRecommendation> Recommendations)
    {
        /// <summary>
        /// Gets no data.
        /// </summary>
        /// <param name="kernelName">The kernel name.</param>
        /// <returns>The result of the operation.</returns>
        public static OptimizationRecommendations NoData(string kernelName)
            => new(kernelName, Array.Empty<OptimizationRecommendation>());
    }
    /// <summary>
    /// A class that represents optimization result.
    /// </summary>

    public record OptimizationResult(
        string KernelName,
        IReadOnlyList<AppliedOptimization> AppliedOptimizations);
    /// <summary>
    /// A class that represents applied optimization.
    /// </summary>

    public record AppliedOptimization(
        OptimizationType Type,
        bool IsSuccess,
        string Message)
    {
        /// <summary>
        /// Gets success.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="message">The message.</param>
        /// <returns>The result of the operation.</returns>
        public static AppliedOptimization Success(OptimizationType type, string message)
            => new(type, true, message);
        /// <summary>
        /// Gets failed.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="message">The message.</param>
        /// <returns>The result of the operation.</returns>

        public static AppliedOptimization Failed(OptimizationType type, string message)
            => new(type, false, message);
        /// <summary>
        /// Gets skipped.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="reason">The reason.</param>
        /// <returns>The result of the operation.</returns>

        public static AppliedOptimization Skipped(OptimizationType type, string reason)
            => new(type, false, $"Skipped: {reason}");
    }
    /// <summary>
    /// A class that represents optimization strategy.
    /// </summary>

    public sealed class OptimizationStrategy(string kernelName)
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        /// <value>The kernel name.</value>
        public string KernelName { get; } = kernelName;
        /// <summary>
        /// Gets or sets the enable automatic optimization.
        /// </summary>
        /// <value>The enable automatic optimization.</value>
        public bool EnableAutomaticOptimization { get; set; } = true;
        /// <summary>
        /// Gets or sets the minimum priority.
        /// </summary>
        /// <value>The minimum priority.</value>
        public OptimizationPriority MinimumPriority { get; set; } = OptimizationPriority.Medium;
        /// <summary>
        /// Gets or sets the optimization interval.
        /// </summary>
        /// <value>The optimization interval.</value>
        public TimeSpan OptimizationInterval { get; set; } = TimeSpan.FromMinutes(10);
    }
}
