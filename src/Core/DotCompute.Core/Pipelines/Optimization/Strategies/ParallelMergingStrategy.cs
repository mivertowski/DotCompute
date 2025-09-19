// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Pipelines.Models;
using DotCompute.Core.Pipelines.Stages;
using DotCompute.Core.Pipelines.Optimization.Models;

namespace DotCompute.Core.Pipelines.Optimization.Strategies;

/// <summary>
/// Parallel merging optimization strategy.
/// </summary>
internal sealed class ParallelMergingStrategy : IOptimizationStrategy
{
    public string Name => "ParallelMerging";
    public OptimizationType SupportedOptimizations => OptimizationType.ParallelMerging;
    public OptimizationType Type => OptimizationType.ParallelMerging;

    public bool CanOptimize(IKernelPipeline pipeline) =>
        pipeline?.Stages?.Count > 1;
    public bool CanApply(IKernelPipeline pipeline) => CanOptimize(pipeline);

    public async Task<IKernelPipeline> OptimizeAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
    {
        return await ApplyAsync(pipeline, cancellationToken);
    }

    public async Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
    {
        var settings = new PipelineOptimizationSettings
        {
            OptimizationTypes = OptimizationType.ParallelMerging
        };

        var result = await ApplyInternalAsync(pipeline.Stages.ToList(), settings, cancellationToken);
        if (result.WasApplied)
        {
            return CreateOptimizedPipeline(pipeline, result.OptimizedStages, settings);
        }
        return pipeline;
    }

    public ValueTask<OptimizationResult> ApplyInternalAsync(
        List<IPipelineStage> stages,
        PipelineOptimizationSettings settings,
        CancellationToken cancellationToken = default)
    {
        var optimizedStages = new List<IPipelineStage>(stages);
        var mergedStages = new List<string>();

        // Find independent stages that can be parallelized
        var independentGroups = FindIndependentStageGroups(stages);

        foreach (var group in independentGroups.Where(g => g.Count > 1))
        {
            var parallelStage = CreateParallelStage(group);

            // Remove original stages and add parallel stage
            foreach (var stage in group)
            {
                _ = optimizedStages.Remove(stage);
                mergedStages.Add(stage.Id);
            }

            optimizedStages.Add(parallelStage);
        }

        var wasApplied = mergedStages.Count > 0;
        var estimatedImpact = wasApplied ? 0.25 : 0.0; // 25% improvement from parallelization

        return new ValueTask<OptimizationResult>(new OptimizationResult
        {
            WasApplied = wasApplied,
            OptimizedStages = optimizedStages,
            Description = $"Merged {mergedStages.Count} stages into parallel execution",
            AffectedStages = mergedStages,
            EstimatedImpact = estimatedImpact,
            EstimatedMemorySavings = 0
        });
    }

    private static List<List<IPipelineStage>> FindIndependentStageGroups(List<IPipelineStage> stages)
    {
        var groups = new List<List<IPipelineStage>>();
        var processed = new HashSet<string>();

        foreach (var stage in stages)
        {
            if (processed.Contains(stage.Id))
            {
                continue;
            }

            var group = new List<IPipelineStage> { stage };
            _ = processed.Add(stage.Id);

            // Find other stages that can run in parallel
            foreach (var otherStage in stages)
            {
                if (processed.Contains(otherStage.Id))
                {
                    continue;
                }

                if (CanRunInParallel(stage, otherStage, stages))
                {
                    group.Add(otherStage);
                    _ = processed.Add(otherStage.Id);
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private static bool CanRunInParallel(IPipelineStage stage1, IPipelineStage stage2, List<IPipelineStage> allStages)
    {
        // Check if stages have no dependencies on each other
        return !stage1.Dependencies.Contains(stage2.Id) &&
               !stage2.Dependencies.Contains(stage1.Id);
    }

    private static IPipelineStage CreateParallelStage(List<IPipelineStage> stages)
    {
        return new ParallelStage(
            $"Parallel_{Guid.NewGuid():N}",
            "Merged Parallel Execution",
            stages,
            Environment.ProcessorCount,
            SynchronizationMode.WaitAll,
            true);
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