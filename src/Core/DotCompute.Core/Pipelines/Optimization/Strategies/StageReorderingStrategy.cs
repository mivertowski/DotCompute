// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Pipelines.Models;
using DotCompute.Core.Pipelines.Optimization.Models;

namespace DotCompute.Core.Pipelines.Optimization.Strategies;

/// <summary>
/// Stage reordering optimization strategy.
/// </summary>
internal sealed class StageReorderingStrategy : IOptimizationStrategy
{
    public string Name => "StageReordering";
    public OptimizationType SupportedOptimizations => OptimizationType.StageReordering;
    public OptimizationType Type => OptimizationType.StageReordering;

    public bool CanOptimize(IKernelPipeline pipeline)
        => pipeline?.Stages?.Count > 1;
    public bool CanApply(IKernelPipeline pipeline) => CanOptimize(pipeline);

    public async Task<IKernelPipeline> OptimizeAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
    {
        return await ApplyAsync(pipeline, cancellationToken);
    }

    public async Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
    {
        var settings = new PipelineOptimizationSettings
        {
            OptimizationTypes = OptimizationType.StageReordering
        };

        var result = await ApplyInternalAsync(pipeline.Stages.ToList(), settings, cancellationToken);
        if (result.WasApplied)
        {
            return CreateOptimizedPipeline(pipeline, result.OptimizedStages, settings);
        }
        return pipeline;
    }

    public static ValueTask<OptimizationResult> ApplyInternalAsync(
        List<IPipelineStage> stages,
        PipelineOptimizationSettings settings,
        CancellationToken cancellationToken = default)
    {
        var optimizedStages = new List<IPipelineStage>(stages);
        var reorderedStages = new List<string>();

        // Topological sort with optimization for parallel execution
        var sorted = TopologicalSortWithOptimization(optimizedStages);

        var wasReordered = !optimizedStages.SequenceEqual(sorted);

        if (wasReordered)
        {
            optimizedStages = sorted;
            reorderedStages = [.. sorted.Select(s => s.Id)];
        }

        var estimatedImpact = wasReordered ? 0.08 : 0.0; // 8% improvement from reordering

        return new ValueTask<OptimizationResult>(new OptimizationResult
        {
            WasApplied = wasReordered,
            OptimizedStages = optimizedStages,
            Description = wasReordered ? "Reordered stages for better parallelism" : "No reordering needed",
            AffectedStages = reorderedStages,
            EstimatedImpact = estimatedImpact,
            EstimatedMemorySavings = 0
        });
    }

    private static List<IPipelineStage> TopologicalSortWithOptimization(List<IPipelineStage> stages)
    {
        // Simplified topological sort
        // In practice, this would optimize for cache locality and parallelism
        var sorted = new List<IPipelineStage>();
        var visited = new HashSet<string>();
        var stageMap = stages.ToDictionary(s => s.Id);

        foreach (var stage in stages)
        {
            TopologicalSortVisit(stage, stageMap, visited, sorted);
        }

        return sorted;
    }

    private static void TopologicalSortVisit(
        IPipelineStage stage,
        Dictionary<string, IPipelineStage> stageMap,
        HashSet<string> visited,
        List<IPipelineStage> sorted)
    {
        if (visited.Contains(stage.Id))
        {
            return;
        }

        _ = visited.Add(stage.Id);

        // Visit dependencies first
        foreach (var depId in stage.Dependencies)
        {
            if (stageMap.TryGetValue(depId, out var depStage))
            {
                TopologicalSortVisit(depStage, stageMap, visited, sorted);
            }
        }

        sorted.Add(stage);
    }

    private static IKernelPipeline CreateOptimizedPipeline(
        IKernelPipeline originalPipeline,
        List<IPipelineStage> optimizedStages,
        PipelineOptimizationSettings settings)
    {
        var builder = KernelPipelineBuilder.Create()
            .WithName($"{originalPipeline.Name}_Optimized")
            .WithOptimization(opt =>
            {
                opt.EnableKernelFusion = (settings.OptimizationTypes & OptimizationType.KernelFusion) != 0;
                opt.EnableStageReordering = (settings.OptimizationTypes & OptimizationType.StageReordering) != 0;
                opt.EnableMemoryOptimization = (settings.OptimizationTypes & OptimizationType.MemoryAccess) != 0;
                opt.EnableParallelMerging = (settings.OptimizationTypes & OptimizationType.ParallelMerging) != 0;
                opt.Level = settings.Level;
            });

        // Copy metadata
        foreach (var (key, value) in originalPipeline.Metadata)
        {
            _ = builder.WithMetadata(key, value);
        }

        // Add optimized stages
        foreach (var stage in optimizedStages)
        {
            _ = builder.AddStage(stage);
        }

        return builder.Build();
    }
}