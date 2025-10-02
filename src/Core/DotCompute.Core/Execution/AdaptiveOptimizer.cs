// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Models.Device;
using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Pipeline;
using DotCompute.Core.Execution.Workload;
using DotCompute.Core.Execution.Metrics;
using Microsoft.Extensions.Logging;
using ExecutionStrategyType = DotCompute.Abstractions.Types.ExecutionStrategyType;

namespace DotCompute.Core.Execution;

/// <summary>
/// Provides adaptive optimization recommendations based on machine learning.
/// </summary>
public class AdaptiveOptimizer
{
    public AdaptiveOptimizer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static ExecutionStrategyRecommendation RecommendStrategy(
        string kernelName,
        int[] inputSizes,
        AcceleratorType[] availableAcceleratorTypes,
        ExecutionRecord[] recentExecutions,
        KernelPerformanceProfile? kernelProfile)
    {
        // Simple heuristic-based recommendation (could be replaced with ML model)
        var totalElements = inputSizes.Aggregate(1L, (a, b) => a * b);
        var hasMultipleGpus = availableAcceleratorTypes.Count(t => t != AcceleratorType.CPU) > 1;

        ExecutionStrategyType recommendedStrategy;
        double confidenceScore;
        string reasoning;

        if (totalElements > 1_000_000 && hasMultipleGpus)
        {
            // Large problem, multiple GPUs available
            var dataParallelPerformance = GetStrategyPerformance(ExecutionStrategyType.DataParallel, recentExecutions);
            var workStealingPerformance = GetStrategyPerformance(ExecutionStrategyType.WorkStealing, recentExecutions);

            if (workStealingPerformance > dataParallelPerformance * 1.1)
            {
                recommendedStrategy = ExecutionStrategyType.WorkStealing;
                reasoning = "Work stealing shows better performance for irregular workloads";
                confidenceScore = 0.8;
            }
            else
            {
                recommendedStrategy = ExecutionStrategyType.DataParallel;
                reasoning = "Large problem size benefits from data parallelism across multiple GPUs";
                confidenceScore = 0.9;
            }
        }
        else if (totalElements > 100_000)
        {
            // Medium problem size
            recommendedStrategy = hasMultipleGpus ? ExecutionStrategyType.DataParallel : ExecutionStrategyType.Single;
            reasoning = hasMultipleGpus ? "Medium problem size can benefit from multi-GPU execution" : "Single GPU sufficient for medium problem size";
            confidenceScore = 0.7;
        }
        else
        {
            // Small problem size
            recommendedStrategy = ExecutionStrategyType.Single;
            reasoning = "Small problem size - parallel overhead likely exceeds benefits";
            confidenceScore = 0.95;
        }

        // Adjust based on kernel profile
        if (kernelProfile != null)
        {
            var kernelCharacteristics = AnalyzeKernelCharacteristics(kernelProfile);
            if (kernelCharacteristics.IsMemoryBound && recommendedStrategy != ExecutionStrategyType.PipelineParallel)
            {
                reasoning += "; Consider pipeline parallelism for memory-bound kernels";
            }
        }

        return new ExecutionStrategyRecommendation
        {
            Strategy = (ExecutionStrategyType)recommendedStrategy,
            ConfidenceScore = confidenceScore,
            Reasoning = reasoning,
            ExpectedImprovementPercentage = EstimateImprovement(recommendedStrategy, recentExecutions)
        };
    }

    private static double GetStrategyPerformance(ExecutionStrategyType strategy, ExecutionRecord[] executions)
    {
        var strategyExecutions = executions.Where(e => e.Strategy == strategy && e.Success).ToArray();
        return strategyExecutions.Length > 0 ? strategyExecutions.Average(e => e.EfficiencyPercentage) : 0;
    }

    private static KernelCharacteristics AnalyzeKernelCharacteristics(KernelPerformanceProfile profile)
    {
        // Analyze kernel to determine if it's compute-bound or memory-bound
        var avgThroughput = profile.DeviceExecutions.SelectMany(kvp => kvp.Value).Average(e => e.ThroughputGFLOPS);
        var avgBandwidth = profile.DeviceExecutions.SelectMany(kvp => kvp.Value)
            .Where(e => e.MemoryBandwidthGBps > 0)
            .DefaultIfEmpty(new KernelExecution())
            .Average(e => e.MemoryBandwidthGBps);

        return new KernelCharacteristics
        {
            IsMemoryBound = avgBandwidth > avgThroughput * 4, // Simple heuristic
            IsComputeBound = avgThroughput > avgBandwidth,
            AverageThroughput = avgThroughput,
            AverageMemoryBandwidth = avgBandwidth
        };
    }

    private static double EstimateImprovement(ExecutionStrategyType strategy, ExecutionRecord[] executions)
    {
        // Estimate expected improvement based on historical data
        var currentBestPerformance = executions.Where(e => e.Success).DefaultIfEmpty().Max(e => e?.EfficiencyPercentage ?? 0);

        return strategy switch
        {
            ExecutionStrategyType.DataParallel => Math.Max(0, 80 - currentBestPerformance),
            ExecutionStrategyType.WorkStealing => Math.Max(0, 85 - currentBestPerformance),
            ExecutionStrategyType.PipelineParallel => Math.Max(0, 75 - currentBestPerformance),
            _ => 0
        };
    }

    /// <summary>
    /// Estimates execution time for data parallel kernels.
    /// </summary>
    public static double EstimateExecutionTime(string kernelName, ComputeDeviceType[] deviceTypes, int dataSize)
    {
        // Simple estimation based on historical data or defaults
        const double baseTimeMs = 10.0; // Base execution time
        const double dataSizeMultiplier = 0.001; // Time per data element

        var deviceMultiplier = deviceTypes.Length > 0 ? 1.0 / deviceTypes.Length : 1.0;
        return (baseTimeMs + (dataSize * dataSizeMultiplier)) * deviceMultiplier;
    }

    /// <summary>
    /// Estimates execution time for model parallel execution.
    /// </summary>
    public static double EstimateModelParallelExecutionTime<T>(ModelParallelWorkload<T> workload, Dictionary<int, IAccelerator> layerAssignments) where T : unmanaged
    {
        // Estimate based on layer count and device distribution
        const double baseLayerTimeMs = 5.0;
        var totalLayers = workload.ModelLayers.Count;
        var deviceCount = layerAssignments.Values.Distinct().Count();

        return totalLayers * baseLayerTimeMs / Math.Max(1, deviceCount);
    }

    /// <summary>
    /// Estimates execution time for pipeline execution.
    /// </summary>
    public static double EstimatePipelineExecutionTime(List<PipelineStageDefinition> pipelineStages, MicrobatchConfiguration microbatchConfig)
    {
        // Estimate based on stage count and microbatch configuration
        const double baseStageTimeMs = 8.0;
        var stageCount = pipelineStages.Count;
        var microbatchOverhead = microbatchConfig.Count * 0.5; // Small overhead per microbatch

        return (stageCount * baseStageTimeMs) + microbatchOverhead;
    }

    /// <summary>
    /// Estimates processing time for a pipeline stage.
    /// </summary>
    public static double EstimateStageProcessingTime(PipelineStageDefinition stage, MicrobatchConfiguration microbatchConfig)
    {
        // Estimate based on stage complexity and microbatch size
        const double baseProcessingTimeMs = 5.0;
        var microbatchMultiplier = microbatchConfig.Count * 0.3;

        return baseProcessingTimeMs + microbatchMultiplier;
    }
}

// Supporting data structures
public class ExecutionRecord
{
    public required Guid Id { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required ExecutionStrategyType Strategy { get; set; }
    public required bool Success { get; set; }
    public required double TotalExecutionTimeMs { get; set; }
    public required double ThroughputGFLOPS { get; set; }
    public required double MemoryBandwidthGBps { get; set; }
    public required double EfficiencyPercentage { get; set; }
    public required DeviceExecutionResult[] DeviceResults { get; set; }
    public string? ErrorMessage { get; set; }
}

public class KernelPerformanceProfile
{
    public required string KernelName { get; set; }
    public Dictionary<string, List<KernelExecution>> DeviceExecutions { get; set; } = [];

    public void AddExecution(string deviceId, double executionTimeMs, double throughputGFLOPS)
    {
        if (!DeviceExecutions.TryGetValue(deviceId, out var executions))
        {
            executions = [];
            DeviceExecutions[deviceId] = executions;
        }

        executions.Add(new KernelExecution
        {
            Timestamp = DateTimeOffset.UtcNow,
            ExecutionTimeMs = executionTimeMs,
            ThroughputGFLOPS = throughputGFLOPS
        });

        // Limit history per device
        if (executions.Count > 1000)
        {
            executions.RemoveAt(0);
        }
    }
}

public class KernelExecution
{
    public DateTimeOffset Timestamp { get; set; }
    public double ExecutionTimeMs { get; set; }
    public double ThroughputGFLOPS { get; set; }
    public double MemoryBandwidthGBps { get; set; }
}

public class KernelCharacteristics
{
    public bool IsMemoryBound { get; set; }
    public bool IsComputeBound { get; set; }
    public double AverageThroughput { get; set; }
    public double AverageMemoryBandwidth { get; set; }
}