// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

namespace DotCompute.Linq.Pipelines.Models;

/// <summary>
/// Represents a complete execution plan for a LINQ-to-Pipeline conversion.
/// Contains all stages, optimization hints, and execution metadata.
/// </summary>
public class PipelineExecutionPlan
{
    /// <summary>
    /// Gets or sets the list of pipeline stages in execution order.
    /// </summary>
    public List<PipelineStageInfo> Stages { get; set; } = new();

    /// <summary>
    /// Gets or sets optimization hints for the entire pipeline.
    /// </summary>
    public List<string> OptimizationHints { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated complexity of the entire pipeline.
    /// </summary>
    public int EstimatedComplexity { get; set; }

    /// <summary>
    /// Gets or sets identified opportunities for parallel execution.
    /// </summary>
    public List<ParallelizationOpportunity> ParallelizationOpportunities { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated total execution time.
    /// </summary>
    public TimeSpan EstimatedExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated total memory requirements.
    /// </summary>
    public long EstimatedMemoryRequirement { get; set; }

    /// <summary>
    /// Gets or sets the recommended backend for optimal execution.
    /// </summary>
    public string? RecommendedBackend { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the pipeline.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents information about a single pipeline stage.
/// </summary>
public class PipelineStageInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for this stage.
    /// </summary>
    public int StageId { get; set; }

    /// <summary>
    /// Gets or sets the name of the kernel to execute for this stage.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of operation this stage performs.
    /// </summary>
    public PipelineStageType StageType { get; set; }

    /// <summary>
    /// Gets or sets the parameters for kernel execution.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the complexity level of this stage.
    /// </summary>
    public KernelComplexity KernelComplexity { get; set; }

    /// <summary>
    /// Gets or sets the backends that support this stage.
    /// </summary>
    public string[] SupportedBackends { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the estimated execution time for this stage.
    /// </summary>
    public TimeSpan EstimatedExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory requirement for this stage.
    /// </summary>
    public long RequiredMemory { get; set; }

    /// <summary>
    /// Gets or sets the stage IDs this stage depends on.
    /// </summary>
    public List<int> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this stage can run in parallel with others.
    /// </summary>
    public bool CanRunInParallel { get; set; }

    /// <summary>
    /// Gets or sets optimization hints specific to this stage.
    /// </summary>
    public List<string> OptimizationHints { get; set; } = new();

    /// <summary>
    /// Gets or sets the input data types for this stage.
    /// </summary>
    public List<Type> InputTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the output data type for this stage.
    /// </summary>
    public Type? OutputType { get; set; }

    /// <summary>
    /// Gets or sets custom configuration for this stage.
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Represents an opportunity for parallel execution.
/// </summary>
public class ParallelizationOpportunity
{
    /// <summary>
    /// Gets or sets the stage IDs that can be parallelized.
    /// </summary>
    public int[] StageIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets the estimated speedup from parallelization.
    /// </summary>
    public double EstimatedSpeedup { get; set; }

    /// <summary>
    /// Gets or sets the description of the parallelization opportunity.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the confidence level in the speedup estimate.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the parallelization strategy to use.
    /// </summary>
    public ParallelizationStrategy Strategy { get; set; }
}

/// <summary>
/// Types of pipeline stages.
/// </summary>
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
}

/// <summary>
/// Kernel complexity levels.
/// </summary>
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
}

/// <summary>
/// Parallelization strategies.
/// </summary>
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
}

/// <summary>
/// Context for pipeline optimization analysis.
/// </summary>
public class PipelineOptimizationContext
{
    private readonly List<string> _hints = new();
    private readonly Dictionary<string, int> _operationCounts = new();
    private readonly List<PipelineStageInfo> _analyzedStages = new();

    /// <summary>
    /// Analyzes a pipeline stage and updates optimization context.
    /// </summary>
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

    /// <summary>
    /// Gets the current optimization hints.
    /// </summary>
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
                break;
                
            case PipelineStageType.Reduction when HasLargeDataset(stage):
                _hints.Add($"Stage {stage.StageId}: Use tree reduction for large datasets");
                break;
                
            case PipelineStageType.Grouping:
                _hints.Add($"Stage {stage.StageId}: Optimize hash table size for grouping");
                break;
        }
    }

    private void CheckFusionOpportunities(PipelineStageInfo stage)
    {
        if (_analyzedStages.Count > 0)
        {
            var previousStage = _analyzedStages[^2]; // Second to last
            
            if (CanFuseStages(previousStage, stage))
            {
                _hints.Add($"Fusion opportunity: Combine stages {previousStage.StageId} and {stage.StageId}");
            }
        }
    }

    private void GenerateGlobalHints()
    {
        // Memory optimization hints
        var totalMemory = _analyzedStages.Sum(s => s.RequiredMemory);
        if (totalMemory > 1024 * 1024 * 1024) // > 1GB
        {
            _hints.Add("Consider streaming execution for large memory requirements");
        }
        
        // Parallelization hints
        var parallelizableStages = _analyzedStages.Count(s => s.CanRunInParallel);
        if (parallelizableStages > 2)
        {
            _hints.Add($"Pipeline has {parallelizableStages} stages that can run in parallel");
        }
        
        // Backend selection hints
        var cudaCompatibleStages = _analyzedStages.Count(s => s.SupportedBackends.Contains("CUDA"));
        var totalStages = _analyzedStages.Count;
        
        if (cudaCompatibleStages > totalStages * 0.8)
        {
            _hints.Add("Pipeline is highly GPU-compatible - recommend CUDA backend");
        }
        else if (cudaCompatibleStages < totalStages * 0.3)
        {
            _hints.Add("Pipeline has limited GPU compatibility - recommend CPU backend");
        }
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
    }
}

/// <summary>
/// Performance report for pipeline analysis.
/// </summary>
public class PipelinePerformanceReport
{
    /// <summary>
    /// Gets or sets the estimated execution time.
    /// </summary>
    public TimeSpan EstimatedExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory usage.
    /// </summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets performance optimization recommendations.
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Gets or sets bottleneck analysis results.
    /// </summary>
    public List<BottleneckInfo> Bottlenecks { get; set; } = new();

    /// <summary>
    /// Gets or sets the confidence level in the estimates.
    /// </summary>
    public double ConfidenceLevel { get; set; }

    /// <summary>
    /// Gets or sets alternative execution strategies.
    /// </summary>
    public List<ExecutionStrategy> AlternativeStrategies { get; set; } = new();
}

/// <summary>
/// Information about performance bottlenecks.
/// </summary>
public class BottleneckInfo
{
    /// <summary>
    /// Gets or sets the stage ID where the bottleneck occurs.
    /// </summary>
    public int StageId { get; set; }

    /// <summary>
    /// Gets or sets the type of bottleneck.
    /// </summary>
    public BottleneckType Type { get; set; }

    /// <summary>
    /// Gets or sets the severity of the bottleneck.
    /// </summary>
    public double Severity { get; set; }

    /// <summary>
    /// Gets or sets the description of the bottleneck.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets suggested mitigations.
    /// </summary>
    public List<string> Mitigations { get; set; } = new();
}

/// <summary>
/// Types of performance bottlenecks.
/// </summary>
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
}

/// <summary>
/// Alternative execution strategy.
/// </summary>
public class ExecutionStrategy
{
    /// <summary>
    /// Gets or sets the strategy name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the estimated performance improvement.
    /// </summary>
    public double PerformanceImprovement { get; set; }

    /// <summary>
    /// Gets or sets the implementation complexity.
    /// </summary>
    public string Complexity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the strategy description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

// BackendRecommendation is defined in MissingInterfaces.cs

/// <summary>
/// Performance estimate for a specific backend.
/// </summary>
public class BackendPerformanceEstimate
{
    /// <summary>
    /// Gets or sets the estimated execution time.
    /// </summary>
    public TimeSpan EstimatedTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory usage.
    /// </summary>
    public long EstimatedMemory { get; set; }

    /// <summary>
    /// Gets or sets the estimated throughput.
    /// </summary>
    public double EstimatedThroughput { get; set; }

    /// <summary>
    /// Gets or sets backend-specific limitations.
    /// </summary>
    public List<string> Limitations { get; set; } = new();
}

/// <summary>
/// Memory usage estimate for pipeline execution.
/// </summary>
public class MemoryEstimate
{
    /// <summary>
    /// Gets or sets the peak memory usage estimate.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage estimate.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets memory usage by stage.
    /// </summary>
    public Dictionary<int, long> MemoryByStage { get; set; } = new();

    /// <summary>
    /// Gets or sets memory optimization opportunities.
    /// </summary>
    public List<string> OptimizationOpportunities { get; set; } = new();
}

/// <summary>
/// Configuration options for data pipeline initialization.
/// </summary>
public sealed class DataPipelineOptions
{
    /// <summary>
    /// Gets or sets whether to create a copy of the input data.
    /// </summary>
    public bool CreateCopy { get; set; }
}

/// <summary>
/// Configuration options for streaming pipeline initialization.
/// </summary>
public sealed class StreamPipelineOptions
{
    /// <summary>
    /// Gets or sets the batch size for micro-batching operations.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the window size for sliding window operations.
    /// </summary>
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether backpressure handling is enabled.
    /// </summary>
    public bool EnableBackpressure { get; set; } = true;
}

