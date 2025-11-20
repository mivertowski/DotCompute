// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Backends.CUDA.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Monitoring
{
    /// <summary>
    /// Analyzes CUDA kernel memory access patterns for coalescing efficiency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Memory coalescing occurs when threads in a warp access consecutive memory addresses,
    /// allowing the GPU to combine multiple memory transactions into fewer, larger transactions.
    /// Poor coalescing can severely impact kernel performance.
    /// </para>
    /// <para>
    /// <strong>Key Metrics:</strong>
    /// <list type="bullet">
    /// <item><description>Global Load Efficiency: Percentage of requested global load throughput utilized</description></item>
    /// <item><description>Global Store Efficiency: Percentage of requested global store throughput utilized</description></item>
    /// <item><description>L1 Cache Hit Rate: Percentage of global memory accesses served by L1 cache</description></item>
    /// <item><description>L2 Cache Hit Rate: Percentage of global memory accesses served by L2 cache</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Ring Kernel Specific Analysis:</strong>
    /// Ring kernels often perform message passing through shared memory queues. This analyzer
    /// provides specific recommendations for optimizing these access patterns.
    /// </para>
    /// </remarks>
    public sealed partial class MemoryCoalescingAnalyzer(ILogger logger)
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 6870,
            Level = LogLevel.Warning,
            Message = "Poor memory coalescing detected in kernel {KernelName}: {GlobalLoadEfficiency}% load efficiency")]
        private static partial void LogPoorCoalescing(ILogger logger, string kernelName, double globalLoadEfficiency);

        [LoggerMessage(
            EventId = 6871,
            Level = LogLevel.Information,
            Message = "Memory coalescing analysis completed for {KernelName}")]
        private static partial void LogAnalysisCompleted(ILogger logger, string kernelName);

        #endregion

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Thresholds for coalescing efficiency classification
        private const double OPTIMAL_LOAD_EFFICIENCY = 90.0;      // >90% is optimal
        private const double ACCEPTABLE_LOAD_EFFICIENCY = 70.0;   // 70-90% is acceptable
        private const double POOR_LOAD_EFFICIENCY = 50.0;         // <50% is poor

        private const double GOOD_L1_HIT_RATE = 70.0;             // >70% L1 hit rate is good

        /// <summary>
        /// Analyzes memory access patterns for coalescing efficiency.
        /// </summary>
        /// <param name="kernelName">Name of the analyzed kernel.</param>
        /// <param name="metrics">Profiling metrics from CUPTI or Nsight Compute.</param>
        /// <param name="isRingKernel">Indicates if this is a Ring Kernel requiring specialized analysis.</param>
        /// <returns>Memory coalescing analysis result with recommendations.</returns>
        public MemoryCoalescingAnalysisResult Analyze(
            string kernelName,
            KernelMetrics metrics,
            bool isRingKernel = false)
        {
            ArgumentNullException.ThrowIfNull(kernelName);
            ArgumentNullException.ThrowIfNull(metrics);

            var result = new MemoryCoalescingAnalysisResult
            {
                KernelName = kernelName,
                AnalysisTime = DateTime.UtcNow,
                IsRingKernel = isRingKernel
            };

            // Extract memory efficiency metrics
            result.GlobalLoadEfficiency = ExtractGlobalLoadEfficiency(metrics);
            result.GlobalStoreEfficiency = ExtractGlobalStoreEfficiency(metrics);
            result.L1CacheHitRate = ExtractL1CacheHitRate(metrics);
            result.L2CacheHitRate = ExtractL2CacheHitRate(metrics);

            // Determine coalescing quality
            result.CoalescingQuality = ClassifyCoalescingQuality(
                result.GlobalLoadEfficiency,
                result.GlobalStoreEfficiency);

            // Detect memory access patterns
            result.AccessPatterns = DetectAccessPatterns(
                metrics,
                result.GlobalLoadEfficiency,
                result.GlobalStoreEfficiency,
                result.L1CacheHitRate,
                result.L2CacheHitRate,
                isRingKernel);

            // Generate optimization recommendations
            result.Recommendations = GenerateRecommendations(
                result.CoalescingQuality,
                result.AccessPatterns,
                metrics,
                isRingKernel);

            // Estimate performance impact
            result.EstimatedPerformanceImpact = EstimatePerformanceImpact(
                result.GlobalLoadEfficiency,
                result.GlobalStoreEfficiency,
                result.L1CacheHitRate,
                result.L2CacheHitRate);

            // Log poor coalescing issues
            if (result.CoalescingQuality == CoalescingQuality.Poor)
            {
                LogPoorCoalescing(_logger, kernelName, result.GlobalLoadEfficiency ?? 0);
            }

            LogAnalysisCompleted(_logger, kernelName);

            return result;
        }

        /// <summary>
        /// Analyzes Nsight Compute profiling results for memory coalescing.
        /// </summary>
        /// <param name="nsightResult">Nsight Compute profiling result.</param>
        /// <param name="isRingKernel">Indicates if this is a Ring Kernel.</param>
        /// <returns>Memory coalescing analysis result.</returns>
        public MemoryCoalescingAnalysisResult Analyze(NsightComputeResult nsightResult, bool isRingKernel = false)
        {
            ArgumentNullException.ThrowIfNull(nsightResult);

            var kernelMetrics = nsightResult.ToKernelMetrics();
            return Analyze(nsightResult.KernelName, kernelMetrics, isRingKernel);
        }

        private static double? ExtractGlobalLoadEfficiency(KernelMetrics metrics)
        {
            var candidates = new[]
            {
                "global_load_efficiency",
                "GLOBAL_LOAD_EFFICIENCY",
                "gld_efficiency",
                "GLD_EFFICIENCY",
                "smsp__sass_average_data_bytes_per_sector_mem_global_op_ld.pct"  // Nsight Compute
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

        private static double? ExtractGlobalStoreEfficiency(KernelMetrics metrics)
        {
            var candidates = new[]
            {
                "global_store_efficiency",
                "GLOBAL_STORE_EFFICIENCY",
                "gst_efficiency",
                "GST_EFFICIENCY",
                "smsp__sass_average_data_bytes_per_sector_mem_global_op_st.pct"  // Nsight Compute
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

        private static double? ExtractL1CacheHitRate(KernelMetrics metrics)
        {
            var candidates = new[]
            {
                "l1_cache_hit_rate",
                "L1_CACHE_HIT_RATE",
                "l1_cache_global_hit_rate",
                "L1_CACHE_GLOBAL_HIT_RATE",
                "l1tex__t_sector_hit_rate.pct"  // Nsight Compute
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

        private static double? ExtractL2CacheHitRate(KernelMetrics metrics)
        {
            var candidates = new[]
            {
                "l2_cache_hit_rate",
                "L2_CACHE_HIT_RATE",
                "l2_global_load_hit_rate",
                "L2_GLOBAL_LOAD_HIT_RATE",
                "lts__t_sector_hit_rate.pct"  // Nsight Compute
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

        private static CoalescingQuality ClassifyCoalescingQuality(
            double? globalLoadEfficiency,
            double? globalStoreEfficiency)
        {
            if (!globalLoadEfficiency.HasValue && !globalStoreEfficiency.HasValue)
            {
                return CoalescingQuality.Unknown;
            }

            // Use the worse of load/store efficiency
            var efficiency = Math.Min(
                globalLoadEfficiency ?? 100.0,
                globalStoreEfficiency ?? 100.0);

            return efficiency switch
            {
                >= OPTIMAL_LOAD_EFFICIENCY => CoalescingQuality.Excellent,
                >= ACCEPTABLE_LOAD_EFFICIENCY => CoalescingQuality.Good,
                >= POOR_LOAD_EFFICIENCY => CoalescingQuality.Acceptable,
                _ => CoalescingQuality.Poor
            };
        }

        private static IReadOnlyList<MemoryAccessPattern> DetectAccessPatterns(
            KernelMetrics metrics,
            double? globalLoadEff,
            double? globalStoreEff,
            double? l1HitRate,
            double? l2HitRate,
            bool isRingKernel)
        {
            var patterns = new List<MemoryAccessPattern>();

            // Pattern 1: Strided access
            if (globalLoadEff.HasValue && globalLoadEff.Value < ACCEPTABLE_LOAD_EFFICIENCY)
            {
                patterns.Add(new MemoryAccessPattern
                {
                    PatternType = MemoryAccessPatternType.Strided,
                    Description = "Non-coalesced memory accesses detected (likely strided pattern)",
                    Confidence = CalculatePatternConfidence(globalLoadEff.Value, 0, ACCEPTABLE_LOAD_EFFICIENCY)
                });
            }

            // Pattern 2: Unaligned access
            if (globalLoadEff.HasValue && globalStoreEff.HasValue)
            {
                var avgEfficiency = (globalLoadEff.Value + globalStoreEff.Value) / 2.0;
                if (avgEfficiency < ACCEPTABLE_LOAD_EFFICIENCY && avgEfficiency > POOR_LOAD_EFFICIENCY)
                {
                    patterns.Add(new MemoryAccessPattern
                    {
                        PatternType = MemoryAccessPatternType.Unaligned,
                        Description = "Unaligned memory accesses reducing coalescing efficiency",
                        Confidence = CalculatePatternConfidence(avgEfficiency, POOR_LOAD_EFFICIENCY, ACCEPTABLE_LOAD_EFFICIENCY)
                    });
                }
            }

            // Pattern 3: Poor cache utilization
            if (l1HitRate.HasValue && l1HitRate.Value < GOOD_L1_HIT_RATE)
            {
                patterns.Add(new MemoryAccessPattern
                {
                    PatternType = MemoryAccessPatternType.PoorCacheUtilization,
                    Description = $"Low L1 cache hit rate ({l1HitRate.Value:F1}%) indicates poor temporal locality",
                    Confidence = CalculatePatternConfidence(l1HitRate.Value, 0, GOOD_L1_HIT_RATE)
                });
            }

            // Pattern 4: Ring Kernel specific - Queue access pattern
            if (isRingKernel && globalLoadEff.HasValue)
            {
                if (globalLoadEff.Value < OPTIMAL_LOAD_EFFICIENCY)
                {
                    patterns.Add(new MemoryAccessPattern
                    {
                        PatternType = MemoryAccessPatternType.QueueAccess,
                        Description = "Ring Kernel queue accesses may not be optimally coalesced",
                        Confidence = CalculatePatternConfidence(globalLoadEff.Value, ACCEPTABLE_LOAD_EFFICIENCY, OPTIMAL_LOAD_EFFICIENCY)
                    });
                }
            }

            // Pattern 5: Random access
            if (l1HitRate.HasValue && l2HitRate.HasValue)
            {
                if (l1HitRate.Value < 40.0 && l2HitRate.Value < 60.0)
                {
                    patterns.Add(new MemoryAccessPattern
                    {
                        PatternType = MemoryAccessPatternType.Random,
                        Description = "Low cache hit rates suggest random or scattered memory access pattern",
                        Confidence = Math.Min(
                            CalculatePatternConfidence(l1HitRate.Value, 0, 40),
                            CalculatePatternConfidence(l2HitRate.Value, 0, 60))
                    });
                }
            }

            return patterns;
        }

        private static double CalculatePatternConfidence(double value, double minThreshold, double maxThreshold)
        {
            // Lower values = higher confidence in problematic pattern
            var normalized = (maxThreshold - value) / (maxThreshold - minThreshold);
            return Math.Clamp(normalized * 100, 0, 100);
        }

        private static IReadOnlyList<string> GenerateRecommendations(
            CoalescingQuality quality,
            IReadOnlyList<MemoryAccessPattern> patterns,
            KernelMetrics metrics,
            bool isRingKernel)
        {
            var recommendations = new List<string>();

            if (quality == CoalescingQuality.Excellent)
            {
                recommendations.Add("Memory coalescing is excellent. No optimization needed.");
                return recommendations;
            }

            // General recommendations based on quality
            if (quality >= CoalescingQuality.Acceptable)
            {
                recommendations.Add("CRITICAL: Poor memory coalescing detected. Immediate optimization recommended.");
            }

            // Pattern-specific recommendations
            foreach (var pattern in patterns)
            {
                switch (pattern.PatternType)
                {
                    case MemoryAccessPatternType.Strided:
                        recommendations.Add(
                            "• Strided access detected. Restructure data layout for sequential access within warps.");
                        recommendations.Add(
                            "• Consider using AoS (Array of Structures) → SoA (Structure of Arrays) transformation.");
                        recommendations.Add(
                            "• Ensure thread IDs map directly to consecutive memory addresses.");
                        break;

                    case MemoryAccessPatternType.Unaligned:
                        recommendations.Add(
                            "• Unaligned accesses detected. Ensure data structures are aligned to cache line boundaries (128 bytes).");
                        recommendations.Add(
                            "• Use __align__ directives for structure members in device code.");
                        recommendations.Add(
                            "• Check that base pointers are properly aligned when allocating device memory.");
                        break;

                    case MemoryAccessPatternType.PoorCacheUtilization:
                        recommendations.Add(
                            "• Poor cache utilization detected. Increase temporal locality by reordering memory accesses.");
                        recommendations.Add(
                            "• Consider using shared memory to cache frequently accessed global memory.");
                        recommendations.Add(
                            "• Optimize data access patterns to maximize cache line reuse within warps.");
                        break;

                    case MemoryAccessPatternType.QueueAccess:
                        if (isRingKernel)
                        {
                            recommendations.Add(
                                "• Ring Kernel queue accesses not optimally coalesced. Use warp-aligned queue structures.");
                            recommendations.Add(
                                "• Consider batching messages to improve memory transaction efficiency.");
                            recommendations.Add(
                                "• Ensure queue head/tail pointers and message buffers are cache-line aligned.");
                        }
                        break;

                    case MemoryAccessPatternType.Random:
                        recommendations.Add(
                            "• Random access pattern detected. This is inherently inefficient on GPUs.");
                        recommendations.Add(
                            "• Consider sorting or partitioning data to improve spatial locality.");
                        recommendations.Add(
                            "• Use texture memory for read-only random accesses (better cache behavior).");
                        recommendations.Add(
                            "• If access pattern cannot be improved, consider CPU processing for random workloads.");
                        break;
                }
            }

            // Ring Kernel specific recommendations
            if (isRingKernel)
            {
                recommendations.Add("• Ring Kernel Optimization: Use circular buffers with power-of-2 sizes for efficient modulo operations.");
                recommendations.Add("• Ring Kernel Optimization: Align message structures to 128-byte boundaries for optimal coalescing.");
                recommendations.Add("• Ring Kernel Optimization: Consider using shared memory staging for frequently accessed messages.");
            }

            // General optimization strategies
            if (quality <= CoalescingQuality.Good)
            {
                recommendations.Add("• Profile with NVIDIA Nsight Compute's Memory Workload Analysis for detailed coalescing metrics.");
                recommendations.Add("• Review kernel source for memory access patterns and data structure layouts.");
                recommendations.Add("• Consider using vectorized loads/stores (float4, int4) for improved transaction efficiency.");
            }

            return recommendations;
        }

        private static double EstimatePerformanceImpact(
            double? globalLoadEff,
            double? globalStoreEff,
            double? l1HitRate,
            double? l2HitRate)
        {
            if (!globalLoadEff.HasValue && !globalStoreEff.HasValue)
            {
                return 0;
            }

            // Calculate memory inefficiency
            var loadLoss = globalLoadEff.HasValue ? (100 - globalLoadEff.Value) / 100.0 : 0;
            var storeLoss = globalStoreEff.HasValue ? (100 - globalStoreEff.Value) / 100.0 : 0;

            // Weight loads more heavily as they're typically more frequent
            var coalescingLoss = (loadLoss * 0.7 + storeLoss * 0.3);

            // Factor in cache miss penalties
            var cachePenalty = 0.0;
            if (l1HitRate.HasValue && l2HitRate.HasValue)
            {
                var l1MissRate = (100 - l1HitRate.Value) / 100.0;
                var l2MissRate = (100 - l2HitRate.Value) / 100.0;

                // L1 miss ~= 28 cycles, L2 miss ~= 200+ cycles
                cachePenalty = (l1MissRate * 0.3 + l2MissRate * 0.7) * 0.5;
            }

            var totalImpact = (coalescingLoss + cachePenalty) * 100;
            return Math.Clamp(totalImpact, 0, 100);
        }
    }

    /// <summary>
    /// Result of memory coalescing analysis.
    /// </summary>
    public sealed class MemoryCoalescingAnalysisResult
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
        /// Gets or sets whether this is a Ring Kernel.
        /// </summary>
        public bool IsRingKernel { get; init; }

        /// <summary>
        /// Gets or sets the global load efficiency percentage (0-100).
        /// Higher is better. &gt;90% is optimal.
        /// </summary>
        public double? GlobalLoadEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the global store efficiency percentage (0-100).
        /// Higher is better. &gt;85% is optimal.
        /// </summary>
        public double? GlobalStoreEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the L1 cache hit rate percentage (0-100).
        /// </summary>
        public double? L1CacheHitRate { get; set; }

        /// <summary>
        /// Gets or sets the L2 cache hit rate percentage (0-100).
        /// </summary>
        public double? L2CacheHitRate { get; set; }

        /// <summary>
        /// Gets or sets the overall coalescing quality classification.
        /// </summary>
        public CoalescingQuality CoalescingQuality { get; set; }

        /// <summary>
        /// Gets or sets detected memory access patterns.
        /// </summary>
        public IReadOnlyList<MemoryAccessPattern> AccessPatterns { get; set; } = [];

        /// <summary>
        /// Gets or sets optimization recommendations.
        /// </summary>
        public IReadOnlyList<string> Recommendations { get; set; } = [];

        /// <summary>
        /// Gets or sets estimated performance impact as percentage (0-100).
        /// Represents estimated performance loss due to poor coalescing.
        /// </summary>
        public double EstimatedPerformanceImpact { get; set; }

        /// <summary>
        /// Generates a human-readable summary of the analysis.
        /// </summary>
        /// <returns>Formatted analysis summary.</returns>
        public string GenerateSummary()
        {
            var summary = new System.Text.StringBuilder();

            summary.AppendLine(CultureInfo.InvariantCulture, $"Memory Coalescing Analysis: {KernelName}");
            if (IsRingKernel)
            {
                summary.AppendLine("Kernel Type: Ring Kernel");
            }

            summary.AppendLine(CultureInfo.InvariantCulture, $"Analysis Time: {AnalysisTime:yyyy-MM-dd HH:mm:ss UTC}");
            summary.AppendLine();

            summary.AppendLine("Metrics:");
            if (GlobalLoadEfficiency.HasValue)
            {
                summary.AppendLine(CultureInfo.InvariantCulture, $"  Global Load Efficiency: {GlobalLoadEfficiency.Value:F2}%");
            }

            if (GlobalStoreEfficiency.HasValue)
            {
                summary.AppendLine(CultureInfo.InvariantCulture, $"  Global Store Efficiency: {GlobalStoreEfficiency.Value:F2}%");
            }

            if (L1CacheHitRate.HasValue)
            {
                summary.AppendLine(CultureInfo.InvariantCulture, $"  L1 Cache Hit Rate: {L1CacheHitRate.Value:F2}%");
            }

            if (L2CacheHitRate.HasValue)
            {
                summary.AppendLine(CultureInfo.InvariantCulture, $"  L2 Cache Hit Rate: {L2CacheHitRate.Value:F2}%");
            }

            summary.AppendLine(CultureInfo.InvariantCulture, $"  Estimated Performance Impact: {EstimatedPerformanceImpact:F2}%");
            summary.AppendLine();

            summary.AppendLine(CultureInfo.InvariantCulture, $"Coalescing Quality: {CoalescingQuality}");
            summary.AppendLine();

            if (AccessPatterns.Count > 0)
            {
                summary.AppendLine("Detected Patterns:");
                foreach (var pattern in AccessPatterns)
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
    /// Detected memory access pattern.
    /// </summary>
    public sealed class MemoryAccessPattern
    {
        /// <summary>
        /// Gets or sets the type of memory access pattern.
        /// </summary>
        public MemoryAccessPatternType PatternType { get; init; }

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
    /// Types of memory access patterns.
    /// </summary>
    public enum MemoryAccessPatternType
    {
        /// <summary>
        /// Strided memory access pattern (non-unit stride).
        /// </summary>
        Strided,

        /// <summary>
        /// Unaligned memory accesses.
        /// </summary>
        Unaligned,

        /// <summary>
        /// Poor cache utilization.
        /// </summary>
        PoorCacheUtilization,

        /// <summary>
        /// Ring Kernel queue access pattern.
        /// </summary>
        QueueAccess,

        /// <summary>
        /// Random or scattered memory access pattern.
        /// </summary>
        Random
    }

    /// <summary>
    /// Overall quality classification for memory coalescing.
    /// </summary>
    public enum CoalescingQuality
    {
        /// <summary>
        /// Unable to determine coalescing quality (missing metrics).
        /// </summary>
        Unknown,

        /// <summary>
        /// Excellent coalescing (&gt;90% efficiency).
        /// </summary>
        Excellent,

        /// <summary>
        /// Good coalescing (70-90% efficiency).
        /// </summary>
        Good,

        /// <summary>
        /// Acceptable coalescing (50-70% efficiency).
        /// </summary>
        Acceptable,

        /// <summary>
        /// Poor coalescing (&lt;50% efficiency).
        /// </summary>
        Poor
    }
}
