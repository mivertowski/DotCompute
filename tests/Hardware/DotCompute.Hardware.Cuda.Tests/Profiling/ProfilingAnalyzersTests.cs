// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Monitoring;
using DotCompute.Tests.Common;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.Profiling
{
    /// <summary>
    /// Comprehensive tests for profiling analyzers (WarpDivergenceAnalyzer, MemoryCoalescingAnalyzer).
    /// </summary>
    /// <remarks>
    /// These tests verify the analyzer logic with various metric scenarios, including:
    /// - Branch efficiency classification (excellent, good, moderate, poor)
    /// - Divergence pattern detection (threadID, data-dependent, loop, early-exit)
    /// - Memory coalescing quality assessment
    /// - Access pattern detection (strided, unaligned, cache, queue, random)
    /// - Recommendation generation based on severity
    /// </remarks>
    [Collection("CUDA Hardware Tests")]
    [Trait("Category", "Hardware")]
    [Trait("Category", "Profiling")]
    public sealed class ProfilingAnalyzersTests(ITestOutputHelper output) : ConsolidatedTestBase(output)
    {
        #region WarpDivergenceAnalyzer Tests

        [SkippableFact(DisplayName = "WarpDivergenceAnalyzer - Excellent branch efficiency (>95%)")]
        public void WarpDivergence_ExcellentBranchEfficiency_ReturnsNoneSeverity()
        {
            // Arrange
            var analyzer = new WarpDivergenceAnalyzer(GetLogger<WarpDivergenceAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["branch_efficiency"] = 98.5; // Excellent

            // Act
            var result = analyzer.Analyze("test_kernel", metrics);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test_kernel", result.KernelName);
            Assert.Equal(98.5, result.BranchEfficiency);
            Assert.Equal(DivergenceSeverity.None, result.Severity);
            Assert.Empty(result.DivergencePatterns);
            Assert.Contains("excellent warp convergence", result.Recommendations[0], StringComparison.OrdinalIgnoreCase);

            Log($"✓ Excellent branch efficiency (98.5%) correctly classified as None severity");
        }

        [SkippableFact(DisplayName = "WarpDivergenceAnalyzer - Low divergence (85-95%)")]
        public void WarpDivergence_LowDivergence_ReturnsLowSeverity()
        {
            // Arrange
            var analyzer = new WarpDivergenceAnalyzer(GetLogger<WarpDivergenceAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["branch_efficiency"] = 88.0; // Low severity

            // Act
            var result = analyzer.Analyze("test_kernel", metrics);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(88.0, result.BranchEfficiency);
            Assert.Equal(DivergenceSeverity.Low, result.Severity);
            Assert.True(result.EstimatedPerformanceImpact > 0);

            Log($"✓ Low divergence (88%) correctly classified with {result.Recommendations.Count} recommendations");
        }

        [SkippableFact(DisplayName = "WarpDivergenceAnalyzer - Moderate divergence (70-85%)")]
        public void WarpDivergence_ModerateDivergence_ReturnsModerate()
        {
            // Arrange
            var analyzer = new WarpDivergenceAnalyzer(GetLogger<WarpDivergenceAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["branch_efficiency"] = 75.0; // Moderate
            metrics.MetricValues["warp_execution_efficiency"] = 70.0;

            // Act
            var result = analyzer.Analyze("test_kernel", metrics);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(75.0, result.BranchEfficiency);
            Assert.Equal(DivergenceSeverity.Moderate, result.Severity);
            Assert.NotEmpty(result.DivergencePatterns);

            // Should detect patterns at this severity
            Assert.Contains(result.DivergencePatterns,
                p => p.PatternType == DivergencePatternType.EarlyExit ||
                     p.PatternType == DivergencePatternType.LoopIteration);

            Log($"✓ Moderate divergence (75%) detected {result.DivergencePatterns.Count} patterns");
        }

        [SkippableFact(DisplayName = "WarpDivergenceAnalyzer - Severe divergence (<70%)")]
        public void WarpDivergence_SevereDivergence_ReturnsSevere()
        {
            // Arrange
            var analyzer = new WarpDivergenceAnalyzer(GetLogger<WarpDivergenceAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["branch_efficiency"] = 45.0; // Severe
            metrics.MetricValues["warp_execution_efficiency"] = 50.0;

            // Act
            var result = analyzer.Analyze("test_kernel", metrics);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(45.0, result.BranchEfficiency);
            Assert.Equal(DivergenceSeverity.Severe, result.Severity);
            Assert.NotEmpty(result.DivergencePatterns);

            // Should detect multiple patterns
            Assert.True(result.DivergencePatterns.Count >= 2,
                $"Expected >= 2 patterns, got {result.DivergencePatterns.Count}");

            // Should have CRITICAL recommendation
            Assert.Contains(result.Recommendations,
                r => r.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase));

            Log($"✓ Severe divergence (45%) detected {result.DivergencePatterns.Count} patterns with critical recommendations");
        }

        [SkippableFact(DisplayName = "WarpDivergenceAnalyzer - Pattern detection: ThreadIdConditional")]
        public void WarpDivergence_ThreadIdPattern_DetectsCorrectly()
        {
            // Arrange
            var analyzer = new WarpDivergenceAnalyzer(GetLogger<WarpDivergenceAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["branch_efficiency"] = 65.0; // Low enough for threadID pattern

            // Act
            var result = analyzer.Analyze("test_kernel", metrics);

            // Assert
            Assert.Contains(result.DivergencePatterns,
                p => p.PatternType == DivergencePatternType.ThreadIdConditional);

            var pattern = result.DivergencePatterns.First(
                p => p.PatternType == DivergencePatternType.ThreadIdConditional);
            Assert.True(pattern.Confidence > 0);

            Log($"✓ ThreadIdConditional pattern detected with {pattern.Confidence:F1}% confidence");
        }

        [SkippableFact(DisplayName = "WarpDivergenceAnalyzer - GenerateSummary produces valid output")]
        public void WarpDivergence_GenerateSummary_ProducesValidOutput()
        {
            // Arrange
            var analyzer = new WarpDivergenceAnalyzer(GetLogger<WarpDivergenceAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["branch_efficiency"] = 75.0;
            metrics.MetricValues["warp_execution_efficiency"] = 70.0;

            // Act
            var result = analyzer.Analyze("test_kernel", metrics);
            var summary = result.GenerateSummary();

            // Assert
            Assert.NotNull(summary);
            Assert.NotEmpty(summary);
            Assert.Contains("test_kernel", summary, StringComparison.Ordinal);
            Assert.Contains("Branch Efficiency: 75", summary, StringComparison.Ordinal);
            Assert.Contains("Moderate", summary, StringComparison.Ordinal);

            Log("✓ Summary generated successfully:");
            Log(summary);
        }

        #endregion

        #region MemoryCoalescingAnalyzer Tests

        [SkippableFact(DisplayName = "MemoryCoalescingAnalyzer - Excellent coalescing (>90%)")]
        public void MemoryCoalescing_ExcellentEfficiency_ReturnsExcellent()
        {
            // Arrange
            var analyzer = new MemoryCoalescingAnalyzer(GetLogger<MemoryCoalescingAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["gld_efficiency"] = 95.0;
            metrics.MetricValues["gst_efficiency"] = 92.0;

            // Act
            var result = analyzer.Analyze("test_kernel", metrics, isRingKernel: false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(95.0, result.GlobalLoadEfficiency);
            Assert.Equal(92.0, result.GlobalStoreEfficiency);
            Assert.Equal(CoalescingQuality.Excellent, result.CoalescingQuality);

            Log($"✓ Excellent coalescing (95% load, 92% store) correctly classified");
        }

        [SkippableFact(DisplayName = "MemoryCoalescingAnalyzer - Good coalescing (70-90%)")]
        public void MemoryCoalescing_GoodEfficiency_ReturnsGood()
        {
            // Arrange
            var analyzer = new MemoryCoalescingAnalyzer(GetLogger<MemoryCoalescingAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["gld_efficiency"] = 80.0;
            metrics.MetricValues["gst_efficiency"] = 75.0;
            metrics.MetricValues["l1_cache_hit_rate"] = 75.0;

            // Act
            var result = analyzer.Analyze("test_kernel", metrics, isRingKernel: false);

            // Assert
            Assert.Equal(80.0, result.GlobalLoadEfficiency);
            Assert.Equal(CoalescingQuality.Good, result.CoalescingQuality);
            // Good quality (70-90%) doesn't necessarily have problematic patterns
            // Patterns are only detected for sub-optimal cases (< 70%)

            Log($"✓ Good coalescing (80%) correctly classified, {result.AccessPatterns.Count} patterns detected");
        }

        [SkippableFact(DisplayName = "MemoryCoalescingAnalyzer - Poor coalescing (<50%)")]
        public void MemoryCoalescing_PoorEfficiency_ReturnsPoor()
        {
            // Arrange
            var analyzer = new MemoryCoalescingAnalyzer(GetLogger<MemoryCoalescingAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["gld_efficiency"] = 30.0; // Poor
            metrics.MetricValues["gst_efficiency"] = 25.0;
            metrics.MetricValues["l1_cache_hit_rate"] = 40.0;
            metrics.MetricValues["l2_cache_hit_rate"] = 60.0;

            // Act
            var result = analyzer.Analyze("test_kernel", metrics, isRingKernel: false);

            // Assert
            Assert.Equal(30.0, result.GlobalLoadEfficiency);
            Assert.Equal(CoalescingQuality.Poor, result.CoalescingQuality);
            Assert.NotEmpty(result.AccessPatterns);

            // Should detect critical issues
            Assert.Contains(result.Recommendations,
                r => r.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase));

            Log($"✓ Poor coalescing (30%) detected with critical recommendations");
        }

        [SkippableFact(DisplayName = "MemoryCoalescingAnalyzer - Ring Kernel mode detects queue pattern")]
        public void MemoryCoalescing_RingKernelMode_DetectsQueuePattern()
        {
            // Arrange
            var analyzer = new MemoryCoalescingAnalyzer(GetLogger<MemoryCoalescingAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["gld_efficiency"] = 75.0; // Below optimal
            metrics.MetricValues["gst_efficiency"] = 70.0;

            // Act
            var result = analyzer.Analyze("ring_kernel", metrics, isRingKernel: true);

            // Assert
            Assert.True(result.IsRingKernel);
            Assert.Contains(result.AccessPatterns,
                p => p.PatternType == MemoryAccessPatternType.QueueAccess);

            var queuePattern = result.AccessPatterns.First(
                p => p.PatternType == MemoryAccessPatternType.QueueAccess);
            Assert.Contains("queue", queuePattern.Description, StringComparison.OrdinalIgnoreCase);

            Log($"✓ Ring Kernel queue pattern detected with {queuePattern.Confidence:F1}% confidence");
        }

        [SkippableFact(DisplayName = "MemoryCoalescingAnalyzer - Strided access pattern detection")]
        public void MemoryCoalescing_StridedPattern_DetectsCorrectly()
        {
            // Arrange
            var analyzer = new MemoryCoalescingAnalyzer(GetLogger<MemoryCoalescingAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["gld_efficiency"] = 55.0; // Low enough for strided
            metrics.MetricValues["gst_efficiency"] = 50.0;
            metrics.MetricValues["l1_cache_hit_rate"] = 85.0; // Good cache partially helps

            // Act
            var result = analyzer.Analyze("test_kernel", metrics, isRingKernel: false);

            // Assert
            Assert.Contains(result.AccessPatterns,
                p => p.PatternType == MemoryAccessPatternType.Strided);

            Log($"✓ Strided access pattern detected");
        }

        [SkippableFact(DisplayName = "MemoryCoalescingAnalyzer - Poor cache utilization detection")]
        public void MemoryCoalescing_PoorCache_DetectsPattern()
        {
            // Arrange
            var analyzer = new MemoryCoalescingAnalyzer(GetLogger<MemoryCoalescingAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["gld_efficiency"] = 70.0;
            metrics.MetricValues["l1_cache_hit_rate"] = 45.0; // Poor L1
            metrics.MetricValues["l2_cache_hit_rate"] = 60.0; // Below threshold

            // Act
            var result = analyzer.Analyze("test_kernel", metrics, isRingKernel: false);

            // Assert
            Assert.Contains(result.AccessPatterns,
                p => p.PatternType == MemoryAccessPatternType.PoorCacheUtilization);

            Log($"✓ Poor cache utilization pattern detected");
        }

        [SkippableFact(DisplayName = "MemoryCoalescingAnalyzer - GenerateSummary produces valid output")]
        public void MemoryCoalescing_GenerateSummary_ProducesValidOutput()
        {
            // Arrange
            var analyzer = new MemoryCoalescingAnalyzer(GetLogger<MemoryCoalescingAnalyzer>());
            var metrics = new KernelMetrics
            {
                KernelExecutions = 1
            };
            metrics.MetricValues["gld_efficiency"] = 75.0;
            metrics.MetricValues["gst_efficiency"] = 70.0;
            metrics.MetricValues["l1_cache_hit_rate"] = 65.0;
            metrics.MetricValues["l2_cache_hit_rate"] = 80.0;

            // Act
            var result = analyzer.Analyze("test_kernel", metrics, isRingKernel: false);
            var summary = result.GenerateSummary();

            // Assert
            Assert.NotNull(summary);
            Assert.NotEmpty(summary);
            Assert.Contains("test_kernel", summary, StringComparison.Ordinal);
            Assert.Contains("75.00%", summary, StringComparison.Ordinal);
            Assert.Contains("Good", summary, StringComparison.Ordinal);

            Log("✓ Summary generated successfully:");
            Log(summary);
        }

        #endregion

        #region RingKernelProfiler Tests

        [SkippableFact(DisplayName = "RingKernelProfiler - Initializes successfully")]
        public void RingKernelProfiler_Initialization_Succeeds()
        {
            // Arrange & Act
            using var profiler = new RingKernelProfiler(
                GetLogger<RingKernelProfiler>(),
                enableNsightCompute: false,
                enableCupti: false);

            // Assert
            Assert.NotNull(profiler);

            Log("✓ RingKernelProfiler initialized successfully");
        }

        [SkippableFact(DisplayName = "RingKernelProfiler - Disposes cleanly")]
        public void RingKernelProfiler_Dispose_DisposesCleanly()
        {
            // Arrange
            var profiler = new RingKernelProfiler(
                GetLogger<RingKernelProfiler>(),
                enableNsightCompute: false,
                enableCupti: false);

            // Act & Assert (should not throw)
            profiler.Dispose();
            profiler.Dispose(); // Double dispose should be safe

            Log("✓ RingKernelProfiler disposed cleanly (including double-dispose)");
        }

        #endregion

        #region NsightComputeProfiler Tests

        [SkippableFact(DisplayName = "NsightComputeProfiler - Initializes and checks for ncu")]
        public void NsightComputeProfiler_Initialization_ChecksForNcu()
        {
            // Arrange & Act
            using var profiler = new NsightComputeProfiler(GetLogger<NsightComputeProfiler>());
            var success = profiler.Initialize();

            // Assert
            // Either succeeds if ncu is found, or fails gracefully
            if (success)
            {
                Log("✓ Nsight Compute (ncu) found and initialized successfully");
            }
            else
            {
                Log("✓ Nsight Compute (ncu) not found - initialization handled gracefully");
            }

            // No exception should be thrown regardless of ncu availability
        }

        [SkippableFact(DisplayName = "NsightComputeProfiler - GetAvailableMetrics handles missing ncu")]
        public void NsightComputeProfiler_GetAvailableMetrics_HandlesNcuNotFound()
        {
            // Arrange
            using var profiler = new NsightComputeProfiler(GetLogger<NsightComputeProfiler>());

            // Act
            var metrics = profiler.GetAvailableMetrics(deviceId: 0);

            // Assert
            Assert.NotNull(metrics);
            // May be empty if ncu not available, or populated if it is

            if (metrics.Count > 0)
            {
                Log($"✓ Found {metrics.Count} available metrics from Nsight Compute");
            }
            else
            {
                Log("✓ GetAvailableMetrics returned empty list (ncu not available)");
            }
        }

        #endregion
    }
}
