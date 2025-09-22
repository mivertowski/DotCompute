// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.Json.Serialization;
namespace DotCompute.Linq.Pipelines.Models;
{
/// <summary>
/// Represents a complete execution plan for a LINQ-to-Pipeline conversion.
/// Contains all stages, optimization hints, and execution metadata.
/// </summary>
public class PipelineExecutionPlan
{
    /// <summary>
    /// Gets or sets the list of pipeline stages in execution order.
    /// </summary>
    public List<PipelineStageInfo> Stages { get; set; } = [];
    /// Gets or sets optimization hints for the entire pipeline.
    public List<string> OptimizationHints { get; set; } = [];
    /// Gets or sets the estimated complexity of the entire pipeline.
    public int EstimatedComplexity { get; set; }
    /// Gets or sets identified opportunities for parallel execution.
    public List<ParallelizationOpportunity> ParallelizationOpportunities { get; set; } = [];
    /// Gets or sets the estimated total execution time.
    public TimeSpan EstimatedExecutionTime { get; set; }
    /// Gets or sets the estimated total memory requirements.
    public long EstimatedMemoryRequirement { get; set; }
    /// Gets or sets the recommended backend for optimal execution.
    public string? RecommendedBackend { get; set; }
    /// Gets or sets additional metadata about the pipeline.
    public Dictionary<string, object> Metadata { get; set; } = [];
}
/// Represents information about a single pipeline stage.
public class PipelineStageInfo
    {
    /// Gets or sets the unique identifier for this stage.
    public int StageId { get; set; }
    /// Gets or sets the name of the kernel to execute for this stage.
    public string KernelName { get; set; } = string.Empty;
    /// Gets or sets the type of operation this stage performs.
    public PipelineStageType StageType { get; set; }
    /// Gets or sets the parameters for kernel execution.
    public Dictionary<string, object> Parameters { get; set; } = [];
    /// Gets or sets the complexity level of this stage.
    public KernelComplexity KernelComplexity { get; set; }
    /// Gets or sets the backends that support this stage.
    public string[] SupportedBackends { get; set; } = Array.Empty<string>();
    /// Gets or sets the estimated execution time for this stage.
    /// Gets or sets the estimated memory requirement for this stage.
    public long RequiredMemory { get; set; }
    /// Gets or sets the stage IDs this stage depends on.
    public List<int> Dependencies { get; set; } = [];
    /// Gets or sets whether this stage can run in parallel with others.
    public bool CanRunInParallel { get; set; }
    /// Gets or sets optimization hints specific to this stage.
    /// Gets or sets the input data types for this stage.
    public List<Type> InputTypes { get; set; } = [];
    /// Gets or sets the output data type for this stage.
    public Type? OutputType { get; set; }
    /// Gets or sets custom configuration for this stage.
    public Dictionary<string, object> Configuration { get; set; } = [];
/// Represents an opportunity for parallel execution.
public class ParallelizationOpportunity
    {
    /// Gets or sets the stage IDs that can be parallelized.
    public int[] StageIds { get; set; } = Array.Empty<int>();
    /// Gets or sets the estimated speedup from parallelization.
    public double EstimatedSpeedup { get; set; }
    /// Gets or sets the description of the parallelization opportunity.
    public string Description { get; set; } = string.Empty;
    /// Gets or sets the confidence level in the speedup estimate.
    public double Confidence { get; set; }
    /// Gets or sets the parallelization strategy to use.
    public ParallelizationStrategy Strategy { get; set; }
/// Types of pipeline stages.
public enum PipelineStageType
    {
    /// <summary>Source data loading stage.</summary>
    Source,
    /// <summary>Data transformation stage (Select, Cast, etc.).</summary>
    Transform,
    /// <summary>Data filtering stage (Where, etc.).</summary>
    Filter,
    /// <summary>Data grouping stage (GroupBy, etc.).</summary>
    Grouping,
    /// <summary>Data joining stage (Join, etc.).</summary>
    Join,
    /// <summary>Data reduction stage (Aggregate, Sum, etc.).</summary>
    Reduction,
    /// <summary>Data sorting stage (OrderBy, etc.).</summary>
    Sorting,
    /// <summary>Data limiting stage (Take, Skip, etc.).</summary>
    Limiting,
    /// <summary>Data deduplication stage (Distinct, etc.).</summary>
    Deduplication,
    /// <summary>Set operation stage (Union, Intersect, etc.).</summary>
    SetOperation,
    /// <summary>Custom operation stage.</summary>
    Custom,
    /// <summary>Output/sink stage.</summary>
    Sink
/// Kernel complexity levels.
public enum KernelComplexity
    {
    /// <summary>Unknown complexity.</summary>
    Unknown = 0,
    /// <summary>Low complexity operations (simple arithmetic, comparisons).</summary>
    Low = 1,
    /// <summary>Medium complexity operations (loops, conditionals).</summary>
    Medium = 3,
    /// <summary>High complexity operations (nested loops, complex algorithms).</summary>
    High = 7,
    /// <summary>Very high complexity operations (sorting, joins, complex aggregations).</summary>
    VeryHigh = 15
/// Parallelization strategies.
public enum ParallelizationStrategy
    {
    /// <summary>No parallelization.</summary>
    None,
    /// <summary>Data-parallel execution.</summary>
    DataParallel,
    /// <summary>Task-parallel execution.</summary>
    TaskParallel,
    /// <summary>Pipeline parallel execution.</summary>
    PipelineParallel,
    /// <summary>Hybrid parallelization approach.</summary>
    Hybrid
/// Context for pipeline optimization analysis.
public class PipelineOptimizationContext
    {
    private readonly List<string> _hints = [];
    private readonly Dictionary<string, int> _operationCounts = [];
    private readonly List<PipelineStageInfo> _analyzedStages = [];
    /// Analyzes a pipeline stage and updates optimization context.
    /// <param name="stage">The stage to analyze</param>
    public void AnalyzeStage(PipelineStageInfo stage)
    {
        _analyzedStages.Add(stage);
        // Count operation types
        var stageTypeName = stage.StageType.ToString();
        _operationCounts.TryGetValue(stageTypeName, out var count);
        _operationCounts[stageTypeName] = count + 1;
        // Generate stage-specific hints
        GenerateStageHints(stage);
        // Check for fusion opportunities
        CheckFusionOpportunities(stage);
    }
    /// Gets the current optimization hints.
    /// <returns>List of optimization hints</returns>
    public List<string> GetOptimizationHints()
    {
        // Add global hints based on analysis
        GenerateGlobalHints();
        return _hints.ToList();
    }
    private void GenerateStageHints(PipelineStageInfo stage)
        {
        switch (stage.StageType)
        {
            case PipelineStageType.Filter when stage.KernelComplexity == KernelComplexity.Low:
                _hints.Add($"Stage {stage.StageId}: Simple filter - consider early execution");
                break;
            case PipelineStageType.Transform when IsProjectionStage(stage):
                _hints.Add($"Stage {stage.StageId}: Consider kernel fusion for projection");
            case PipelineStageType.Reduction when HasLargeDataset(stage):
                _hints.Add($"Stage {stage.StageId}: Use tree reduction for large datasets");
            case PipelineStageType.Grouping:
                _hints.Add($"Stage {stage.StageId}: Optimize hash table size for grouping");
        }
    private void CheckFusionOpportunities(PipelineStageInfo stage)
    {
        if (_analyzedStages.Count > 0)
            var previousStage = _analyzedStages[^2]; // Second to last
            if (CanFuseStages(previousStage, stage))
            {
                _hints.Add($"Fusion opportunity: Combine stages {previousStage.StageId} and {stage.StageId}");
            }
    private void GenerateGlobalHints()
    {
        // Memory optimization hints
        var totalMemory = _analyzedStages.Sum(s => s.RequiredMemory);
        if (totalMemory > 1024 * 1024 * 1024) // > 1GB
            _hints.Add("Consider streaming execution for large memory requirements");
        // Parallelization hints
        var parallelizableStages = _analyzedStages.Count(s => s.CanRunInParallel);
        if (parallelizableStages > 2)
            _hints.Add($"Pipeline has {parallelizableStages} stages that can run in parallel");
        // Backend selection hints
        var cudaCompatibleStages = _analyzedStages.Count(s => s.SupportedBackends.Contains("CUDA"));
        var totalStages = _analyzedStages.Count;
        if (cudaCompatibleStages > totalStages * 0.8)
            _hints.Add("Pipeline is highly GPU-compatible - recommend CUDA backend");
        else if (cudaCompatibleStages < totalStages * 0.3)
            _hints.Add("Pipeline has limited GPU compatibility - recommend CPU backend");
    }
    private static bool IsProjectionStage(PipelineStageInfo stage)
    {
        return stage.StageType == PipelineStageType.Transform &&
               stage.Parameters.ContainsKey("selector");
    }
    private static bool HasLargeDataset(PipelineStageInfo stage)
    {
        return stage.RequiredMemory > 10 * 1024 * 1024; // > 10MB
    }
    private static bool CanFuseStages(PipelineStageInfo first, PipelineStageInfo second)
    {
        // Simple heuristic for kernel fusion opportunities
        return (first.StageType == PipelineStageType.Transform || first.StageType == PipelineStageType.Filter) &&
               (second.StageType == PipelineStageType.Transform || second.StageType == PipelineStageType.Filter) &&
               first.SupportedBackends.Intersect(second.SupportedBackends).Any();
/// Performance report for pipeline analysis.
public class PipelinePerformanceReport
    {
    /// Gets or sets the estimated execution time.
    /// Gets or sets the estimated memory usage.
    public long EstimatedMemoryUsage { get; set; }
    /// Gets or sets performance optimization recommendations.
    public List<string> Recommendations { get; set; } = [];
    /// Gets or sets bottleneck analysis results.
    public List<BottleneckInfo> Bottlenecks { get; set; } = [];
    /// Gets or sets the confidence level in the estimates.
    public double ConfidenceLevel { get; set; }
    /// Gets or sets alternative execution strategies.
    public List<ExecutionStrategy> AlternativeStrategies { get; set; } = [];
    /// Gets or sets the parallelization potential (0.0 to 1.0).
    public double ParallelizationPotential { get; set; }
    /// Gets the estimated execution time in milliseconds.
    public double EstimatedExecutionTimeMs => EstimatedExecutionTime.TotalMilliseconds;
    /// Gets the estimated memory usage in megabytes.
    public double EstimatedMemoryUsageMB => EstimatedMemoryUsage / (1024.0 * 1024.0);
/// Information about performance bottlenecks.
public class BottleneckInfo
    {
    /// Gets or sets the stage ID where the bottleneck occurs.
    /// Gets or sets the type of bottleneck.
    public BottleneckType Type { get; set; }
    /// Gets or sets the severity of the bottleneck.
    public double Severity { get; set; }
    /// Gets or sets the description of the bottleneck.
    /// Gets or sets suggested mitigations.
    public List<string> Mitigations { get; set; } = [];
/// Types of performance bottlenecks.
public enum BottleneckType
    {
    /// <summary>Memory bandwidth bottleneck.</summary>
    MemoryBandwidth,
    /// <summary>Compute throughput bottleneck.</summary>
    ComputeThroughput,
    /// <summary>Memory capacity bottleneck.</summary>
    MemoryCapacity,
    /// <summary>Data transfer bottleneck.</summary>
    DataTransfer,
    /// <summary>Algorithm complexity bottleneck.</summary>
    AlgorithmComplexity
/// Alternative execution strategy.
public class ExecutionStrategy
    {
    /// Gets or sets the strategy name.
    public string Name { get; set; } = string.Empty;
    /// Gets or sets the estimated performance improvement.
    public double PerformanceImprovement { get; set; }
    /// Gets or sets the implementation complexity.
    public string Complexity { get; set; } = string.Empty;
    /// Gets or sets the strategy description.
// BackendRecommendation is defined in MissingInterfaces.cs
/// Performance estimate for a specific backend.
public class BackendPerformanceEstimate
    {
    public TimeSpan EstimatedTime { get; set; }
    public long EstimatedMemory { get; set; }
    /// Gets or sets the estimated throughput.
    public double EstimatedThroughput { get; set; }
    /// Gets or sets backend-specific limitations.
    public List<string> Limitations { get; set; } = [];
/// Memory usage estimate for pipeline execution.
public class MemoryEstimate
    {
    /// Gets or sets the peak memory usage estimate.
    public long PeakMemoryUsage { get; set; }
    /// Gets or sets the average memory usage estimate.
    public long AverageMemoryUsage { get; set; }
    /// Gets or sets memory usage by stage.
    public Dictionary<int, long> MemoryByStage { get; set; } = [];
    /// Gets or sets memory optimization opportunities.
    public List<string> OptimizationOpportunities { get; set; } = [];
/// Configuration options for data pipeline initialization.
public sealed class DataPipelineOptions
    {
    /// Gets or sets whether to create a copy of the input data.
    public bool CreateCopy { get; set; }
/// Configuration options for streaming pipeline initialization.
public sealed class StreamPipelineOptions
    {
    /// Gets or sets the batch size for micro-batching operations.
    public int BatchSize { get; set; } = 1000;
    /// Gets or sets the window size for sliding window operations.
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromSeconds(1);
    /// Gets or sets whether backpressure handling is enabled.
    public bool EnableBackpressure { get; set; } = true;
}
