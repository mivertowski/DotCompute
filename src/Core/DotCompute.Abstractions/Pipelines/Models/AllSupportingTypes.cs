// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Interfaces.Pipelines;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Supporting types and enums for pipeline infrastructure.
/// This file contains all the small supporting types to resolve compilation issues.
/// </summary>

// Enum types
public enum BackendFallbackStrategy
{
    None,
    NextAvailable,
    Auto,
    Manual
}

// SynchronizationMode is defined in DotCompute.Abstractions.Types namespace

public enum LoadBalancingStrategy
{
    RoundRobin,
    LeastLoaded,
    Random,
    Weighted,
    Adaptive
}

public enum ParallelErrorStrategy
{
    FailFast,
    ContinueOnError,
    RetryFailures,
    Aggregate
}

public enum MemorySharingMode
{
    None,
    ReadOnly,
    Shared,
    Exclusive
}

public enum ResourceAllocationStrategy
{
    FirstFit,
    BestFit,
    Proportional,
    Dynamic
}

public enum AdaptationPolicy
{
    Conservative,
    Aggressive,
    Balanced,
    Custom
}

// Supporting model classes
public sealed class ParallelKernelConfig
{
    public required string KernelName { get; init; }
    public object[]? Parameters { get; init; }
    public object[]? Arguments { get; init; }
    public int[]? GlobalWorkSize { get; init; }
    public DotCompute.Abstractions.Pipelines.Enums.ExecutionPriority Priority { get; init; } = ExecutionPriority.Normal;
}

public sealed class AffinityRule
{
    public required string KernelPattern { get; init; }
    public required string ComputeUnitPattern { get; init; }
    public double Weight { get; init; } = 1.0;
}

public sealed class FusionCriteria
{
    public bool AllowMemoryFusion { get; set; } = true;
    public bool AllowComputeFusion { get; set; } = true;
    public int MaxFusionDepth { get; set; } = 3;
    public double MinEfficiencyGain { get; set; } = 0.1;
}

public sealed class MemoryConstraints
{
    public long MaxMemoryUsage { get; set; } = long.MaxValue;
    public bool PreferMemoryEfficiency { get; set; }

    public int MemoryAlignment { get; set; } = 64;

    public MemoryConstraints Clone()
    {
        return new MemoryConstraints
        {
            MaxMemoryUsage = MaxMemoryUsage,
            PreferMemoryEfficiency = PreferMemoryEfficiency,
            MemoryAlignment = MemoryAlignment
        };
    }
}

public sealed class ParallelismGoals
{
    public int TargetParallelism { get; set; } = Environment.ProcessorCount;
    public double MinEfficiency { get; set; } = 0.8;
    public bool AllowNesting { get; set; } = true;
}

public sealed class LoopOptimizations
{
    public bool EnableUnrolling { get; set; } = true;
    public bool EnableVectorization { get; set; } = true;
    public bool EnableTiling { get; set; } = true;
    public int MaxUnrollFactor { get; set; } = 8;
}

public sealed class DataLayoutPreferences
{
    public bool PreferStructureOfArrays { get; set; }

    public bool PreferArrayOfStructures { get; set; }

    public bool AllowReordering { get; set; } = true;
    public int PreferredAlignment { get; set; } = 64;
}

public sealed class OptimizationValidationResult
{
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public double AccuracyScore { get; set; } = 1.0;
    public IList<string> Warnings { get; set; } = [];
}

public sealed class OptimizationImpactEstimate
{
    public double EstimatedSpeedup { get; set; } = 1.0;
    public double EstimatedMemoryReduction { get; set; }

    public double ConfidenceLevel { get; set; } = 0.5;
    public TimeSpan EstimatedOptimizationTime { get; set; }
}

public sealed class PipelineAnalysisResult
{
    public bool Success { get; set; }
    public IList<string> Issues { get; set; } = [];
    public IList<string> Recommendations { get; set; } = [];
    public IDictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
}

// Interface placeholders for types referenced in the pipeline interfaces
public interface IOptimizationPass
{
    public string Name { get; }
    public OptimizationType OptimizationType { get; }
    public Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default);
}

public interface IOptimizationStrategy
{
    public string Name { get; }
    public OptimizationType SupportedOptimizations { get; }
    public OptimizationType Type { get; }
    public bool CanOptimize(IKernelPipeline pipeline);
    public bool CanApply(IKernelPipeline pipeline);
    public Task<IKernelPipeline> OptimizeAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default);
    public Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default);
}


// IKernelPipeline is defined in DotCompute.Abstractions.Interfaces.Pipelines namespace