// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Logging;
using DotCompute.Backends.CUDA.Monitoring;
using Microsoft.Extensions.Logging;
using PublicDim3 = DotCompute.Abstractions.Types.Dim3;

namespace DotCompute.Backends.CUDA.Optimization
{
    /// <summary>
    /// Adaptive launch configuration optimizer that uses profiling data to dynamically
    /// tune kernel launch parameters for optimal performance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This optimizer analyzes runtime profiling data including warp divergence, memory
    /// coalescing efficiency, and Ring Kernel specific metrics to adaptively select
    /// the best launch configuration strategy.
    /// </para>
    /// <para>
    /// <strong>Optimization Strategies:</strong>
    /// <list type="bullet">
    /// <item><description>Warp Divergence Mitigation: Adjusts block size to reduce divergent branches</description></item>
    /// <item><description>Memory Coalescing Optimization: Selects configurations that improve memory access patterns</description></item>
    /// <item><description>Occupancy Maximization: Balances register/shared memory usage with thread count</description></item>
    /// <item><description>Ring Kernel Specialization: Optimizes for persistent kernel workloads</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal sealed partial class AdaptiveLaunchConfigurationOptimizer
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 6870,
            Level = LogLevel.Information,
            Message = "Adaptive optimization started for kernel {KernelName}")]
        private static partial void LogOptimizationStarted(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 6871,
            Level = LogLevel.Information,
            Message = "Selected optimization strategy: {Strategy} (Priority: {Priority})")]
        private static partial void LogStrategySelected(ILogger logger, string strategy, string priority);

        [LoggerMessage(
            EventId = 6872,
            Level = LogLevel.Information,
            Message = "Optimized configuration: GridSize={GridSize}, BlockSize={BlockSize}, " +
                     "Occupancy={Occupancy:F2}%, EstimatedSpeedup={Speedup:F2}x")]
        private static partial void LogOptimizedConfig(
            ILogger logger,
            int gridSize,
            int blockSize,
            double occupancy,
            double speedup);

        [LoggerMessage(
            EventId = 6873,
            Level = LogLevel.Warning,
            Message = "Optimization produced suboptimal configuration due to conflicting constraints")]
        private static partial void LogSuboptimalConfig(ILogger logger);

        #endregion

        private readonly ILogger _logger;
        private readonly CudaOccupancyCalculator _occupancyCalculator;
        private readonly WarpDivergenceAnalyzer? _divergenceAnalyzer;
        private readonly MemoryCoalescingAnalyzer? _coalescingAnalyzer;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveLaunchConfigurationOptimizer"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="occupancyCalculator">The CUDA occupancy calculator.</param>
        /// <param name="divergenceAnalyzer">Optional warp divergence analyzer.</param>
        /// <param name="coalescingAnalyzer">Optional memory coalescing analyzer.</param>
        public AdaptiveLaunchConfigurationOptimizer(
            ILogger<AdaptiveLaunchConfigurationOptimizer> logger,
            CudaOccupancyCalculator occupancyCalculator,
            WarpDivergenceAnalyzer? divergenceAnalyzer = null,
            MemoryCoalescingAnalyzer? coalescingAnalyzer = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _occupancyCalculator = occupancyCalculator ?? throw new ArgumentNullException(nameof(occupancyCalculator));
            _divergenceAnalyzer = divergenceAnalyzer;
            _coalescingAnalyzer = coalescingAnalyzer;
        }

        /// <summary>
        /// Optimizes kernel launch configuration based on profiling data.
        /// </summary>
        /// <param name="kernelFunc">Pointer to the kernel function.</param>
        /// <param name="kernelName">Name of the kernel.</param>
        /// <param name="deviceId">GPU device ID.</param>
        /// <param name="profilingData">Profiling data from previous kernel executions.</param>
        /// <param name="constraints">Optional user-defined constraints.</param>
        /// <param name="dynamicSharedMemory">Dynamic shared memory requirement in bytes.</param>
        /// <returns>Optimized launch configuration with estimated performance improvement.</returns>
        public async Task<OptimizedLaunchConfiguration> OptimizeAsync(
            IntPtr kernelFunc,
            string kernelName,
            int deviceId,
            KernelProfilingData? profilingData = null,
            LaunchConstraints? constraints = null,
            nuint dynamicSharedMemory = 0)
        {
            ArgumentNullException.ThrowIfNull(kernelName);

            LogOptimizationStarted(_logger, kernelName);

            // Analyze profiling data to determine optimization strategy
            var strategy = DetermineOptimizationStrategy(profilingData);
            LogStrategySelected(_logger, strategy.Strategy.ToString(), strategy.Priority.ToString());

            // Build adaptive constraints based on profiling data
            var adaptiveConstraints = BuildAdaptiveConstraints(
                profilingData,
                strategy,
                constraints ?? LaunchConstraints.Default);

            // Calculate optimal configuration using occupancy calculator
            var baseConfig = await _occupancyCalculator.CalculateOptimalLaunchConfigAsync(
                kernelFunc,
                deviceId,
                dynamicSharedMemory,
                adaptiveConstraints);

            // Refine configuration based on specific profiling insights
            var refinedConfig = RefineConfiguration(baseConfig, profilingData, strategy);

            // Estimate performance improvement
            var estimatedSpeedup = EstimatePerformanceImprovement(profilingData, refinedConfig, strategy);

            LogOptimizedConfig(
                _logger,
                refinedConfig.GridSize.X,
                refinedConfig.BlockSize.X,
                refinedConfig.TheoreticalOccupancy * 100,
                estimatedSpeedup);

            if (estimatedSpeedup < 1.05)
            {
                LogSuboptimalConfig(_logger);
            }

            return new OptimizedLaunchConfiguration
            {
                Configuration = refinedConfig,
                Strategy = strategy.Strategy,
                EstimatedSpeedup = estimatedSpeedup,
                OptimizationMetadata = new OptimizationMetadata
                {
                    PrimaryBottleneck = strategy.Priority,
                    RecommendedIterations = CalculateRecommendedIterations(profilingData),
                    ConfidenceScore = CalculateConfidenceScore(profilingData, strategy)
                }
            };
        }

        /// <summary>
        /// Determines the optimal optimization strategy based on profiling data analysis.
        /// </summary>
        private static OptimizationStrategy DetermineOptimizationStrategy(KernelProfilingData? profilingData)
        {
            if (profilingData == null)
            {
                // No profiling data - use balanced strategy
                return new OptimizationStrategy
                {
                    Strategy = OptimizationHint.Balanced,
                    Priority = OptimizationPriority.Occupancy
                };
            }

            var priorities = new List<(OptimizationPriority Priority, double Severity)>();

            // Analyze warp divergence
            if (profilingData.DivergenceAnalysis != null)
            {
                var divergenceSeverity = profilingData.DivergenceAnalysis.Severity switch
                {
                    DivergenceSeverity.Severe => 10.0,
                    DivergenceSeverity.Moderate => 7.0,
                    DivergenceSeverity.Low => 3.0,
                    _ => 0.0
                };

                if (divergenceSeverity > 0)
                {
                    priorities.Add((OptimizationPriority.WarpDivergence, divergenceSeverity));
                }
            }

            // Analyze memory coalescing
            if (profilingData.CoalescingAnalysis != null)
            {
                var coalescingSeverity = profilingData.CoalescingAnalysis.CoalescingQuality switch
                {
                    CoalescingQuality.Poor => 10.0,
                    CoalescingQuality.Acceptable => 5.0,
                    CoalescingQuality.Good => 2.0,
                    _ => 0.0
                };

                if (coalescingSeverity > 0)
                {
                    priorities.Add((OptimizationPriority.MemoryCoalescing, coalescingSeverity));
                }
            }

            // Analyze Ring Kernel metrics
            if (profilingData.RingKernelData != null)
            {
                var messageLatency = profilingData.RingKernelData.AverageLatencyMicroseconds;
                if (messageLatency > 1000.0) // > 1 microsecond is concerning
                {
                    priorities.Add((OptimizationPriority.Latency, 6.0));
                }
            }

            // Select highest priority issue
            if (priorities.Count > 0)
            {
                var primaryPriority = priorities.OrderByDescending(p => p.Severity).First();

                var hint = primaryPriority.Priority switch
                {
                    OptimizationPriority.WarpDivergence => OptimizationHint.ComputeBound,
                    OptimizationPriority.MemoryCoalescing => OptimizationHint.MemoryBound,
                    OptimizationPriority.Latency => OptimizationHint.Latency,
                    _ => OptimizationHint.Balanced
                };

                return new OptimizationStrategy
                {
                    Strategy = hint,
                    Priority = primaryPriority.Priority,
                    Severity = primaryPriority.Severity
                };
            }

            // All metrics are good - maximize occupancy
            return new OptimizationStrategy
            {
                Strategy = OptimizationHint.Balanced,
                Priority = OptimizationPriority.Occupancy
            };
        }

        /// <summary>
        /// Builds adaptive launch constraints based on profiling insights.
        /// </summary>
        private static LaunchConstraints BuildAdaptiveConstraints(
            KernelProfilingData? profilingData,
            OptimizationStrategy strategy,
            LaunchConstraints baseConstraints)
        {
            var adaptiveConstraints = new LaunchConstraints
            {
                MinBlockSize = baseConstraints.MinBlockSize,
                MaxBlockSize = baseConstraints.MaxBlockSize,
                ProblemSize = baseConstraints.ProblemSize,
                RequireWarpMultiple = baseConstraints.RequireWarpMultiple,
                RequirePowerOfTwo = baseConstraints.RequirePowerOfTwo,
                OptimizationHint = strategy.Strategy
            };

            if (profilingData == null)
            {
                return adaptiveConstraints;
            }

            // Adjust block size constraints based on warp divergence
            if (profilingData.DivergenceAnalysis?.Severity >= DivergenceSeverity.Moderate)
            {
                // Severe divergence - use smaller blocks to improve warp uniformity
                adaptiveConstraints.MaxBlockSize = Math.Min(
                    adaptiveConstraints.MaxBlockSize ?? 1024,
                    256);
                adaptiveConstraints.RequireWarpMultiple = true;
            }

            // Adjust for memory coalescing issues
            if (profilingData.CoalescingAnalysis?.CoalescingQuality <= CoalescingQuality.Acceptable)
            {
                // Poor coalescing - use larger blocks that are multiples of 128
                // This helps with memory transaction efficiency
                adaptiveConstraints.MinBlockSize = Math.Max(
                    adaptiveConstraints.MinBlockSize ?? 32,
                    128);
                adaptiveConstraints.RequireWarpMultiple = true;
            }

            // Adjust for Ring Kernel workloads
            if (profilingData.RingKernelData != null)
            {
                // Ring Kernels benefit from moderate block sizes for better queue performance
                adaptiveConstraints.MinBlockSize = Math.Max(
                    adaptiveConstraints.MinBlockSize ?? 32,
                    64);
                adaptiveConstraints.MaxBlockSize = Math.Min(
                    adaptiveConstraints.MaxBlockSize ?? 1024,
                    512);
            }

            return adaptiveConstraints;
        }

        /// <summary>
        /// Refines the base configuration using specific profiling insights.
        /// </summary>
        private static LaunchConfiguration RefineConfiguration(
            LaunchConfiguration baseConfig,
            KernelProfilingData? profilingData,
            OptimizationStrategy strategy)
        {
            if (profilingData == null)
            {
                return baseConfig;
            }

            var refinedConfig = new LaunchConfiguration
            {
                BlockSize = baseConfig.BlockSize,
                GridSize = baseConfig.GridSize,
                SharedMemoryBytes = baseConfig.SharedMemoryBytes,
                TheoreticalOccupancy = baseConfig.TheoreticalOccupancy,
                ActiveWarps = baseConfig.ActiveWarps,
                ActiveBlocks = baseConfig.ActiveBlocks,
                RegistersPerThread = baseConfig.RegistersPerThread,
                DeviceId = baseConfig.DeviceId
            };

            // Apply strategy-specific refinements
            switch (strategy.Priority)
            {
                case OptimizationPriority.MemoryCoalescing:
                    // For poor coalescing, ensure block size is optimal for memory transactions
                    if (refinedConfig.BlockSize.X % 128 != 0)
                    {
                        var newBlockSize = ((refinedConfig.BlockSize.X / 128) + 1) * 128;
                        if (newBlockSize <= 1024)
                        {
                            refinedConfig.BlockSize = new PublicDim3(newBlockSize, 1, 1);
                            // Recalculate grid size
                            var totalThreads = baseConfig.GridSize.X * baseConfig.BlockSize.X;
                            refinedConfig.GridSize = new PublicDim3(
                                (totalThreads + newBlockSize - 1) / newBlockSize,
                                1,
                                1);
                        }
                    }
                    break;

                case OptimizationPriority.WarpDivergence:
                    // For divergence, prefer smaller warp-aligned blocks
                    if (refinedConfig.BlockSize.X > 256)
                    {
                        var newBlockSize = 256;
                        refinedConfig.BlockSize = new PublicDim3(newBlockSize, 1, 1);
                        var totalThreads = baseConfig.GridSize.X * baseConfig.BlockSize.X;
                        refinedConfig.GridSize = new PublicDim3(
                            (totalThreads + newBlockSize - 1) / newBlockSize,
                            1,
                            1);
                    }
                    break;

                case OptimizationPriority.Latency:
                    // For latency-sensitive workloads, use larger blocks to reduce grid size
                    if (refinedConfig.BlockSize.X < 512 && refinedConfig.GridSize.X > 1)
                    {
                        var newBlockSize = Math.Min(512, 1024);
                        refinedConfig.BlockSize = new PublicDim3(newBlockSize, 1, 1);
                        var totalThreads = baseConfig.GridSize.X * baseConfig.BlockSize.X;
                        refinedConfig.GridSize = new PublicDim3(
                            Math.Max(1, (totalThreads + newBlockSize - 1) / newBlockSize),
                            1,
                            1);
                    }
                    break;
            }

            return refinedConfig;
        }

        /// <summary>
        /// Estimates the expected performance improvement from optimization.
        /// </summary>
        private static double EstimatePerformanceImprovement(
            KernelProfilingData? profilingData,
            LaunchConfiguration optimizedConfig,
            OptimizationStrategy strategy)
        {
            if (profilingData == null)
            {
                return 1.0; // No baseline to compare against
            }

            var estimatedSpeedup = 1.0;

            // Factor in occupancy improvement
            var occupancyBoost = optimizedConfig.TheoreticalOccupancy / Math.Max(0.1, profilingData.BaselineOccupancy ?? 0.5);
            estimatedSpeedup *= Math.Min(occupancyBoost, 2.0); // Cap at 2x from occupancy alone

            // Factor in divergence mitigation
            if (profilingData.DivergenceAnalysis != null)
            {
                var divergenceImpact = profilingData.DivergenceAnalysis.EstimatedPerformanceImpact / 100.0;
                // If we're addressing divergence, we can recover some of the lost performance
                if (strategy.Priority == OptimizationPriority.WarpDivergence && divergenceImpact > 0.1)
                {
                    var divergenceRecovery = 1.0 + (divergenceImpact * 0.5); // Recover 50% of divergence penalty
                    estimatedSpeedup *= divergenceRecovery;
                }
            }

            // Factor in coalescing improvement
            if (profilingData.CoalescingAnalysis != null)
            {
                var coalescingEfficiency = profilingData.CoalescingAnalysis.GlobalLoadEfficiency ?? 50.0;
                if (strategy.Priority == OptimizationPriority.MemoryCoalescing && coalescingEfficiency < 70.0)
                {
                    // Assume we can improve to at least 70% efficiency
                    var coalescingBoost = 70.0 / Math.Max(coalescingEfficiency, 10.0);
                    estimatedSpeedup *= Math.Min(coalescingBoost, 2.0); // Cap at 2x from coalescing
                }
            }

            // Factor in latency reduction for Ring Kernels
            if (profilingData.RingKernelData != null && strategy.Priority == OptimizationPriority.Latency)
            {
                // Latency improvements are typically modest (10-30%)
                estimatedSpeedup *= 1.2;
            }

            // Cap total estimated speedup at reasonable maximum
            return Math.Min(estimatedSpeedup, 5.0);
        }

        /// <summary>
        /// Calculates the recommended number of profiling iterations needed for stable metrics.
        /// </summary>
        private static int CalculateRecommendedIterations(KernelProfilingData? profilingData)
        {
            if (profilingData == null)
            {
                return 10; // Default minimum
            }

            // More iterations needed if metrics show high variance or are at thresholds
            var baseIterations = 10;

            // Need more data if we're near decision boundaries
            if (profilingData.DivergenceAnalysis?.BranchEfficiency >= 68.0 &&
                profilingData.DivergenceAnalysis?.BranchEfficiency <= 72.0)
            {
                baseIterations += 5; // Near moderate/severe boundary
            }

            if (profilingData.CoalescingAnalysis?.GlobalLoadEfficiency >= 48.0 &&
                profilingData.CoalescingAnalysis?.GlobalLoadEfficiency <= 52.0)
            {
                baseIterations += 5; // Near acceptable/poor boundary
            }

            return Math.Min(baseIterations, 30); // Cap at 30 iterations
        }

        /// <summary>
        /// Calculates confidence score for the optimization (0.0 - 1.0).
        /// </summary>
        private static double CalculateConfidenceScore(
            KernelProfilingData? profilingData,
            OptimizationStrategy strategy)
        {
            if (profilingData == null)
            {
                return 0.5; // Moderate confidence without data
            }

            var confidence = 0.7; // Base confidence

            // Higher confidence if profiling data is comprehensive
            if (profilingData.DivergenceAnalysis != null)
            {
                confidence += 0.1;
            }

            if (profilingData.CoalescingAnalysis != null)
            {
                confidence += 0.1;
            }

            // Higher confidence for clear-cut issues
            if (strategy.Severity >= 8.0)
            {
                confidence += 0.1; // Clear optimization target
            }

            return Math.Min(confidence, 1.0);
        }
    }

    /// <summary>
    /// Kernel profiling data container for adaptive optimization.
    /// </summary>
    public sealed class KernelProfilingData
    {
        /// <summary>
        /// Gets or sets the warp divergence analysis results.
        /// </summary>
        public WarpDivergenceAnalysisResult? DivergenceAnalysis { get; set; }

        /// <summary>
        /// Gets or sets the memory coalescing analysis results.
        /// </summary>
        public MemoryCoalescingAnalysisResult? CoalescingAnalysis { get; set; }

        /// <summary>
        /// Gets or sets the Ring Kernel specific profiling data.
        /// </summary>
        public RingKernelProfilingResult? RingKernelData { get; set; }

        /// <summary>
        /// Gets or sets the baseline occupancy from previous configuration.
        /// </summary>
        public double? BaselineOccupancy { get; set; }
    }

    /// <summary>
    /// Optimization strategy determined from profiling analysis.
    /// </summary>
    internal sealed class OptimizationStrategy
    {
        /// <summary>
        /// Gets or sets the recommended optimization hint.
        /// </summary>
        public OptimizationHint Strategy { get; set; }

        /// <summary>
        /// Gets or sets the primary optimization priority.
        /// </summary>
        public OptimizationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the severity score of the primary bottleneck (0-10).
        /// </summary>
        public double Severity { get; set; }
    }

    /// <summary>
    /// Optimization priorities based on profiling analysis.
    /// </summary>
    public enum OptimizationPriority
    {
        /// <summary>
        /// Maximize GPU occupancy.
        /// </summary>
        Occupancy,

        /// <summary>
        /// Mitigate warp divergence.
        /// </summary>
        WarpDivergence,

        /// <summary>
        /// Improve memory coalescing.
        /// </summary>
        MemoryCoalescing,

        /// <summary>
        /// Reduce kernel launch and execution latency.
        /// </summary>
        Latency
    }

    /// <summary>
    /// Optimized launch configuration with metadata.
    /// </summary>
    public sealed class OptimizedLaunchConfiguration
    {
        /// <summary>
        /// Gets or sets the optimized launch configuration.
        /// </summary>
        public required LaunchConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets or sets the optimization strategy used.
        /// </summary>
        public OptimizationHint Strategy { get; set; }

        /// <summary>
        /// Gets or sets the estimated performance speedup (1.0 = no improvement).
        /// </summary>
        public double EstimatedSpeedup { get; set; }

        /// <summary>
        /// Gets or sets additional optimization metadata.
        /// </summary>
        public required OptimizationMetadata OptimizationMetadata { get; set; }

        /// <summary>
        /// Generates a human-readable summary of the optimization.
        /// </summary>
        public string GenerateSummary()
        {
            var summary = new System.Text.StringBuilder();

            summary.AppendLine(CultureInfo.InvariantCulture, $"Optimized Launch Configuration");
            summary.AppendLine(CultureInfo.InvariantCulture, $"Grid: ({Configuration.GridSize.X}, {Configuration.GridSize.Y}, {Configuration.GridSize.Z})");
            summary.AppendLine(CultureInfo.InvariantCulture, $"Block: ({Configuration.BlockSize.X}, {Configuration.BlockSize.Y}, {Configuration.BlockSize.Z})");
            summary.AppendLine(CultureInfo.InvariantCulture, $"Shared Memory: {Configuration.SharedMemoryBytes} bytes");
            summary.AppendLine();

            summary.AppendLine(CultureInfo.InvariantCulture, $"Strategy: {Strategy}");
            summary.AppendLine(CultureInfo.InvariantCulture, $"Primary Bottleneck: {OptimizationMetadata.PrimaryBottleneck}");
            summary.AppendLine(CultureInfo.InvariantCulture, $"Theoretical Occupancy: {Configuration.TheoreticalOccupancy:P2}");
            summary.AppendLine(CultureInfo.InvariantCulture, $"Estimated Speedup: {EstimatedSpeedup:F2}x");
            summary.AppendLine(CultureInfo.InvariantCulture, $"Confidence: {OptimizationMetadata.ConfidenceScore:P0}");
            summary.AppendLine();

            summary.AppendLine(CultureInfo.InvariantCulture, $"Recommended Profiling Iterations: {OptimizationMetadata.RecommendedIterations}");

            return summary.ToString();
        }
    }

    /// <summary>
    /// Additional metadata about the optimization process.
    /// </summary>
    public sealed class OptimizationMetadata
    {
        /// <summary>
        /// Gets or sets the primary performance bottleneck identified.
        /// </summary>
        public OptimizationPriority PrimaryBottleneck { get; set; }

        /// <summary>
        /// Gets or sets the recommended number of profiling iterations for stable metrics.
        /// </summary>
        public int RecommendedIterations { get; set; }

        /// <summary>
        /// Gets or sets the confidence score for the optimization (0.0 - 1.0).
        /// </summary>
        public double ConfidenceScore { get; set; }
    }
}
