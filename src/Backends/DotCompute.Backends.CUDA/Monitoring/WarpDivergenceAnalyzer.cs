// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Backends.CUDA.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Monitoring
{
    /// <summary>
    /// Analyzes CUDA kernel profiling data to detect and diagnose warp divergence issues.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Warp divergence occurs when threads within a warp take different execution paths,
    /// causing serialization and performance degradation. This analyzer detects divergence
    /// patterns and provides actionable recommendations for optimization.
    /// </para>
    /// <para>
    /// <strong>Key Metrics:</strong>
    /// <list type="bullet">
    /// <item><description>Branch Efficiency: Percentage of branches where all threads follow the same path</description></item>
    /// <item><description>Warp Execution Efficiency: Ratio of average active threads to maximum threads per warp</description></item>
    /// <item><description>Divergent Branch Count: Number of branches causing divergence</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed partial class WarpDivergenceAnalyzer(ILogger logger)
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 6868,
            Level = LogLevel.Warning,
            Message = "Severe warp divergence detected in kernel {KernelName}: {BranchEfficiency}% branch efficiency")]
        private static partial void LogSevereDivergence(ILogger logger, string kernelName, double branchEfficiency);

        [LoggerMessage(
            EventId = 6869,
            Level = LogLevel.Information,
            Message = "Warp divergence analysis completed for {KernelName}")]
        private static partial void LogAnalysisCompleted(ILogger logger, string kernelName);

        #endregion

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Thresholds for divergence severity classification
        private const double SEVERE_DIVERGENCE_THRESHOLD = 70.0;  // < 70% branch efficiency
        private const double MODERATE_DIVERGENCE_THRESHOLD = 85.0; // 70-85% branch efficiency
        private const double OPTIMAL_BRANCH_EFFICIENCY = 95.0;    // > 95% is optimal

        /// <summary>
        /// Analyzes kernel metrics for warp divergence issues.
        /// </summary>
        /// <param name="kernelName">Name of the analyzed kernel.</param>
        /// <param name="metrics">Profiling metrics from CUPTI or Nsight Compute.</param>
        /// <returns>Divergence analysis result with severity and recommendations.</returns>
        public WarpDivergenceAnalysisResult Analyze(string kernelName, KernelMetrics metrics)
        {
            ArgumentNullException.ThrowIfNull(kernelName);
            ArgumentNullException.ThrowIfNull(metrics);

            var result = new WarpDivergenceAnalysisResult
            {
                KernelName = kernelName,
                AnalysisTime = DateTime.UtcNow
            };

            // Extract branch efficiency (primary divergence indicator)
            var branchEfficiency = ExtractBranchEfficiency(metrics);
            result.BranchEfficiency = branchEfficiency;

            // Extract warp execution efficiency
            var warpExecEfficiency = ExtractWarpExecutionEfficiency(metrics);
            result.WarpExecutionEfficiency = warpExecEfficiency;

            // Determine severity based on branch efficiency
            result.Severity = ClassifyDivergenceSeverity(branchEfficiency);

            // Detect specific divergence patterns
            result.DivergencePatterns = DetectDivergencePatterns(metrics, branchEfficiency);

            // Generate optimization recommendations
            result.Recommendations = GenerateRecommendations(result.Severity, result.DivergencePatterns, metrics);

            // Calculate performance impact estimate
            result.EstimatedPerformanceImpact = EstimatePerformanceImpact(branchEfficiency, warpExecEfficiency);

            // Log severe divergence issues
            if (result.Severity == DivergenceSeverity.Severe)
            {
                LogSevereDivergence(_logger, kernelName, branchEfficiency ?? 0);
            }

            LogAnalysisCompleted(_logger, kernelName);

            return result;
        }

        /// <summary>
        /// Analyzes Nsight Compute profiling results for warp divergence.
        /// </summary>
        /// <param name="nsightResult">Nsight Compute profiling result.</param>
        /// <returns>Divergence analysis result.</returns>
        public WarpDivergenceAnalysisResult Analyze(NsightComputeResult nsightResult)
        {
            ArgumentNullException.ThrowIfNull(nsightResult);

            // Convert Nsight Compute result to KernelMetrics
            var kernelMetrics = nsightResult.ToKernelMetrics();

            return Analyze(nsightResult.KernelName, kernelMetrics);
        }

        private static double? ExtractBranchEfficiency(KernelMetrics metrics)
        {
            // Try multiple metric name variations
            var candidates = new[]
            {
                "branch_efficiency",
                "BRANCH_EFFICIENCY",
                "Branch Efficiency",
                "smsp__sass_average_branch_targets_threads_uniform.pct"  // Nsight Compute metric
            };

            foreach (var name in candidates)
            {
                if (metrics.MetricValues.TryGetValue(name, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static double? ExtractWarpExecutionEfficiency(KernelMetrics metrics)
        {
            var candidates = new[]
            {
                "warp_execution_efficiency",
                "WARP_EXECUTION_EFFICIENCY",
                "Warp Execution Efficiency",
                "smsp__thread_inst_executed_per_inst_executed.ratio"
            };

            foreach (var name in candidates)
            {
                if (metrics.MetricValues.TryGetValue(name, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static DivergenceSeverity ClassifyDivergenceSeverity(double? branchEfficiency)
        {
            if (!branchEfficiency.HasValue)
            {
                return DivergenceSeverity.Unknown;
            }

            var efficiency = branchEfficiency.Value;

            return efficiency switch
            {
                >= OPTIMAL_BRANCH_EFFICIENCY => DivergenceSeverity.None,
                >= MODERATE_DIVERGENCE_THRESHOLD => DivergenceSeverity.Low,
                >= SEVERE_DIVERGENCE_THRESHOLD => DivergenceSeverity.Moderate,
                _ => DivergenceSeverity.Severe
            };
        }

        private static IReadOnlyList<DivergencePattern> DetectDivergencePatterns(
            KernelMetrics metrics,
            double? branchEfficiency)
        {
            var patterns = new List<DivergencePattern>();

            // Pattern 1: Conditional branching on thread ID
            if (branchEfficiency.HasValue && branchEfficiency.Value < MODERATE_DIVERGENCE_THRESHOLD)
            {
                patterns.Add(new DivergencePattern
                {
                    PatternType = DivergencePatternType.ThreadIdConditional,
                    Description = "Likely conditional branches based on thread ID or similar divergent conditions",
                    Confidence = CalculatePatternConfidence(branchEfficiency.Value, 50, 85)
                });
            }

            // Pattern 2: Data-dependent branching
            var warpExecEff = ExtractWarpExecutionEfficiency(metrics);
            if (branchEfficiency.HasValue && warpExecEff.HasValue)
            {
                if (branchEfficiency.Value < 90 && warpExecEff.Value < 85)
                {
                    patterns.Add(new DivergencePattern
                    {
                        PatternType = DivergencePatternType.DataDependent,
                        Description = "Data-dependent branching causing irregular warp execution",
                        Confidence = CalculatePatternConfidence(branchEfficiency.Value, 60, 90)
                    });
                }
            }

            // Pattern 3: Loop divergence
            if (branchEfficiency.HasValue && branchEfficiency.Value < 80)
            {
                patterns.Add(new DivergencePattern
                {
                    PatternType = DivergencePatternType.LoopIteration,
                    Description = "Possible loop iteration count variation across threads",
                    Confidence = CalculatePatternConfidence(branchEfficiency.Value, 40, 80)
                });
            }

            // Pattern 4: Early exit/return divergence
            if (branchEfficiency.HasValue && branchEfficiency.Value < 75)
            {
                patterns.Add(new DivergencePattern
                {
                    PatternType = DivergencePatternType.EarlyExit,
                    Description = "Threads may be exiting early at different points",
                    Confidence = CalculatePatternConfidence(branchEfficiency.Value, 45, 75)
                });
            }

            return patterns;
        }

        private static double CalculatePatternConfidence(double branchEfficiency, double minThreshold, double maxThreshold)
        {
            // Convert branch efficiency to confidence (lower efficiency = higher confidence in pattern)
            var normalized = (maxThreshold - branchEfficiency) / (maxThreshold - minThreshold);
            return Math.Clamp(normalized * 100, 0, 100);
        }

        private static IReadOnlyList<string> GenerateRecommendations(
            DivergenceSeverity severity,
            IReadOnlyList<DivergencePattern> patterns,
            KernelMetrics metrics)
        {
            var recommendations = new List<string>();

            if (severity == DivergenceSeverity.None)
            {
                recommendations.Add("Kernel exhibits excellent warp convergence. No optimization needed.");
                return recommendations;
            }

            // General recommendations based on severity
            if (severity >= DivergenceSeverity.Moderate)
            {
                recommendations.Add("CRITICAL: Significant warp divergence detected. Immediate optimization recommended.");
            }

            // Pattern-specific recommendations
            foreach (var pattern in patterns)
            {
                switch (pattern.PatternType)
                {
                    case DivergencePatternType.ThreadIdConditional:
                        recommendations.Add(
                            "• Avoid conditional branches based on threadIdx. " +
                            "Consider restructuring to ensure all threads in a warp follow the same path.");
                        recommendations.Add(
                            "• Use predication instead of branching where possible (ternary operators, min/max).");
                        break;

                    case DivergencePatternType.DataDependent:
                        recommendations.Add(
                            "• Data-dependent divergence detected. Consider sorting or partitioning input data " +
                            "to group similar execution paths within warps.");
                        recommendations.Add(
                            "• Explore using warp-cooperative algorithms or separate kernels for different code paths.");
                        break;

                    case DivergencePatternType.LoopIteration:
                        recommendations.Add(
                            "• Loop iteration divergence detected. Ensure all threads in a warp execute the same " +
                            "number of loop iterations, or use warp-level synchronization.");
                        recommendations.Add(
                            "• Consider padding data or using sentinel values to normalize loop counts.");
                        break;

                    case DivergencePatternType.EarlyExit:
                        recommendations.Add(
                            "• Early exit patterns detected. Restructure to have all threads complete execution " +
                            "together, even if some threads do no-op work.");
                        recommendations.Add(
                            "• Consider using a separate compaction pass for active threads.");
                        break;
                }
            }

            // Add general optimization strategies
            if (severity >= DivergenceSeverity.Low)
            {
                recommendations.Add("• Profile with NVIDIA Nsight Compute for detailed source-level divergence analysis.");
                recommendations.Add("• Review kernel source for if/else statements and loops with variable iteration counts.");
                recommendations.Add(
                    "• Consider increasing work per thread to amortize divergence costs, " +
                    "if memory bandwidth is not a bottleneck.");
            }

            return recommendations;
        }

        private static double EstimatePerformanceImpact(double? branchEfficiency, double? warpExecEfficiency)
        {
            if (!branchEfficiency.HasValue)
            {
                return 0;
            }

            // Estimate performance loss due to divergence
            // Perfect efficiency (100%) = 0% loss, 50% efficiency = ~50% loss
            var branchLoss = (100 - branchEfficiency.Value) / 100.0;

            // Factor in warp execution efficiency if available
            if (warpExecEfficiency.HasValue)
            {
                var warpLoss = (100 - warpExecEfficiency.Value) / 100.0;
                return (branchLoss + warpLoss) / 2.0 * 100; // Average and convert to percentage
            }

            return branchLoss * 100;
        }
    }

    /// <summary>
    /// Result of warp divergence analysis.
    /// </summary>
    public sealed class WarpDivergenceAnalysisResult
    {
        /// <summary>
        /// Gets or sets the name of the analyzed kernel.
        /// </summary>
        public string KernelName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp of the analysis.
        /// </summary>
        public DateTime AnalysisTime { get; init; }

        /// <summary>
        /// Gets or sets the branch efficiency percentage (0-100).
        /// Higher is better. >95% is optimal.
        /// </summary>
        public double? BranchEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the warp execution efficiency percentage (0-100).
        /// </summary>
        public double? WarpExecutionEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the severity of detected divergence.
        /// </summary>
        public DivergenceSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets detected divergence patterns.
        /// </summary>
        public IReadOnlyList<DivergencePattern> DivergencePatterns { get; set; } = [];

        /// <summary>
        /// Gets or sets optimization recommendations.
        /// </summary>
        public IReadOnlyList<string> Recommendations { get; set; } = [];

        /// <summary>
        /// Gets or sets estimated performance impact as percentage (0-100).
        /// Represents estimated performance loss due to divergence.
        /// </summary>
        public double EstimatedPerformanceImpact { get; set; }

        /// <summary>
        /// Generates a human-readable summary of the analysis.
        /// </summary>
        /// <returns>Formatted analysis summary.</returns>
        public string GenerateSummary()
        {
            var summary = new System.Text.StringBuilder();

            summary.AppendLine(CultureInfo.InvariantCulture, $"Warp Divergence Analysis: {KernelName}");
            summary.AppendLine(CultureInfo.InvariantCulture, $"Analysis Time: {AnalysisTime:yyyy-MM-dd HH:mm:ss UTC}");
            summary.AppendLine();

            summary.AppendLine("Metrics:");
            if (BranchEfficiency.HasValue)
            {
                summary.AppendLine(CultureInfo.InvariantCulture, $"  Branch Efficiency: {BranchEfficiency.Value:F2}%");
            }

            if (WarpExecutionEfficiency.HasValue)
            {
                summary.AppendLine(CultureInfo.InvariantCulture, $"  Warp Execution Efficiency: {WarpExecutionEfficiency.Value:F2}%");
            }

            summary.AppendLine(CultureInfo.InvariantCulture, $"  Estimated Performance Impact: {EstimatedPerformanceImpact:F2}%");
            summary.AppendLine();

            summary.AppendLine(CultureInfo.InvariantCulture, $"Severity: {Severity}");
            summary.AppendLine();

            if (DivergencePatterns.Count > 0)
            {
                summary.AppendLine("Detected Patterns:");
                foreach (var pattern in DivergencePatterns)
                {
                    summary.AppendLine(CultureInfo.InvariantCulture, $"  • {pattern.PatternType}: {pattern.Description}");
                    summary.AppendLine(CultureInfo.InvariantCulture, $"    Confidence: {pattern.Confidence:F1}%");
                }
                summary.AppendLine();
            }

            if (Recommendations.Count > 0)
            {
                summary.AppendLine("Recommendations:");
                foreach (var rec in Recommendations)
                {
                    summary.AppendLine(CultureInfo.InvariantCulture, $"  {rec}");
                }
            }

            return summary.ToString();
        }
    }

    /// <summary>
    /// Detected divergence pattern.
    /// </summary>
    public sealed class DivergencePattern
    {
        /// <summary>
        /// Gets or sets the type of divergence pattern.
        /// </summary>
        public DivergencePatternType PatternType { get; init; }

        /// <summary>
        /// Gets or sets a description of the pattern.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets confidence in pattern detection (0-100).
        /// </summary>
        public double Confidence { get; init; }
    }

    /// <summary>
    /// Types of warp divergence patterns.
    /// </summary>
    public enum DivergencePatternType
    {
        /// <summary>
        /// Conditional branches based on thread ID causing divergence.
        /// </summary>
        ThreadIdConditional,

        /// <summary>
        /// Data-dependent branching causing divergence.
        /// </summary>
        DataDependent,

        /// <summary>
        /// Variable loop iteration counts across threads.
        /// </summary>
        LoopIteration,

        /// <summary>
        /// Early exit/return statements causing threads to exit at different times.
        /// </summary>
        EarlyExit
    }

    /// <summary>
    /// Severity levels for warp divergence.
    /// </summary>
    public enum DivergenceSeverity
    {
        /// <summary>
        /// Unable to determine divergence (missing metrics).
        /// </summary>
        Unknown,

        /// <summary>
        /// No significant divergence detected (>95% branch efficiency).
        /// </summary>
        None,

        /// <summary>
        /// Minor divergence (85-95% branch efficiency).
        /// </summary>
        Low,

        /// <summary>
        /// Moderate divergence affecting performance (70-85% branch efficiency).
        /// </summary>
        Moderate,

        /// <summary>
        /// Severe divergence significantly impacting performance (&lt;70% branch efficiency).
        /// </summary>
        Severe
    }
}
