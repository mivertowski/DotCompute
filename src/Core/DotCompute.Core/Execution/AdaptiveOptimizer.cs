// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Models.Device;
using DotCompute.Core.Execution.Metrics;
using DotCompute.Core.Execution.Pipeline;
using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Workload;
using Microsoft.Extensions.Logging;
using ExecutionStrategyType = DotCompute.Abstractions.Types.ExecutionStrategyType;

namespace DotCompute.Core.Execution;

/// <summary>
/// Provides adaptive optimization recommendations based on machine learning.
/// </summary>
public class AdaptiveOptimizer
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the AdaptiveOptimizer class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public AdaptiveOptimizer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    /// <summary>
    /// Gets recommend strategy.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="inputSizes">The input sizes.</param>
    /// <param name="availableAcceleratorTypes">The available accelerator types.</param>
    /// <param name="recentExecutions">The recent executions.</param>
    /// <param name="kernelProfile">The kernel profile.</param>
    /// <returns>The result of the operation.</returns>

    public static ExecutionStrategyRecommendation RecommendStrategy(
        string kernelName,
        IReadOnlyList<int> inputSizes,
        IReadOnlyList<AcceleratorType> availableAcceleratorTypes,
        IReadOnlyList<ExecutionRecord> recentExecutions,
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
            var dataParallelPerformance = GetStrategyPerformance(ExecutionStrategyType.DataParallel, [.. recentExecutions]);
            var workStealingPerformance = GetStrategyPerformance(ExecutionStrategyType.WorkStealing, [.. recentExecutions]);

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
            if (kernelCharacteristics.IsMemoryBound)
            {
                reasoning += "; Consider pipeline parallelism for memory-bound kernels";
            }
        }

        return new ExecutionStrategyRecommendation
        {
            Strategy = (ExecutionStrategyType)recommendedStrategy,
            ConfidenceScore = confidenceScore,
            Reasoning = reasoning,
            ExpectedImprovementPercentage = EstimateImprovement(recommendedStrategy, [.. recentExecutions])
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
    public static double EstimateExecutionTime(string kernelName, IReadOnlyList<ComputeDeviceType> deviceTypes, int dataSize)
    {
        // Simple estimation based on historical data or defaults
        const double baseTimeMs = 10.0; // Base execution time
        const double dataSizeMultiplier = 0.001; // Time per data element

        var deviceMultiplier = deviceTypes.Count > 0 ? 1.0 / deviceTypes.Count : 1.0;
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
    public static double EstimatePipelineExecutionTime(IReadOnlyList<PipelineStageDefinition> pipelineStages, MicrobatchConfiguration microbatchConfig)
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
/// <summary>
/// Represents a record of a kernel execution for performance tracking.
/// </summary>
public class ExecutionRecord
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public required Guid Id { get; set; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public required DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public required ExecutionStrategyType Strategy { get; set; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public required bool Success { get; set; }
    /// <summary>
    /// Gets or sets the total execution time ms.
    /// </summary>
    /// <value>The total execution time ms.</value>
    public required double TotalExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the throughput g f l o p s.
    /// </summary>
    /// <value>The throughput g f l o p s.</value>
    public required double ThroughputGFLOPS { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth g bps.
    /// </summary>
    /// <value>The memory bandwidth g bps.</value>
    public required double MemoryBandwidthGBps { get; set; }
    /// <summary>
    /// Gets or sets the efficiency percentage.
    /// </summary>
    /// <value>The efficiency percentage.</value>
    public required double EfficiencyPercentage { get; set; }
    /// <summary>
    /// Gets or sets the device results.
    /// </summary>
    /// <value>The device results.</value>
    public required IReadOnlyList<DeviceExecutionResult> DeviceResults { get; init; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Performance profile for a kernel across different devices.
/// </summary>
public class KernelPerformanceProfile
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the device executions.
    /// </summary>
    /// <value>The device executions.</value>
    public Dictionary<string, List<KernelExecution>> DeviceExecutions { get; } = [];
    /// <summary>
    /// Performs add execution.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="executionTimeMs">The execution time ms.</param>
    /// <param name="throughputGFLOPS">The throughput G F L O P S.</param>

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

/// <summary>
/// Represents a single kernel execution result.
/// </summary>
public class KernelExecution
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the execution time ms.
    /// </summary>
    /// <value>The execution time ms.</value>
    public double ExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the throughput g f l o p s.
    /// </summary>
    /// <value>The throughput g f l o p s.</value>
    public double ThroughputGFLOPS { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth g bps.
    /// </summary>
    /// <value>The memory bandwidth g bps.</value>
    public double MemoryBandwidthGBps { get; set; }
}

/// <summary>
/// Characteristics of a kernel's performance profile.
/// </summary>
public class KernelCharacteristics
{
    /// <summary>
    /// Gets or sets a value indicating whether memory bound.
    /// </summary>
    /// <value>The is memory bound.</value>
    public bool IsMemoryBound { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether compute bound.
    /// </summary>
    /// <value>The is compute bound.</value>
    public bool IsComputeBound { get; set; }
    /// <summary>
    /// Gets or sets the average throughput.
    /// </summary>
    /// <value>The average throughput.</value>
    public double AverageThroughput { get; set; }
    /// <summary>
    /// Gets or sets the average memory bandwidth.
    /// </summary>
    /// <value>The average memory bandwidth.</value>
    public double AverageMemoryBandwidth { get; set; }
}
