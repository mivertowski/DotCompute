// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Pipelines.Models;
using DotCompute.Core.Pipelines.Optimization.Models;
using OptimizationLevel = DotCompute.Abstractions.Types.OptimizationLevel;

namespace DotCompute.Core.Pipelines.Optimization.Strategies;

/// <summary>
/// Dead code elimination strategy.
/// </summary>
internal sealed class DeadCodeEliminationStrategy : IOptimizationStrategy
{
    public string Name => "DeadCodeElimination";
    public OptimizationType SupportedOptimizations => OptimizationType.DeadCodeElimination;
    public OptimizationType Type => OptimizationType.DeadCodeElimination;

    public bool CanOptimize(IKernelPipeline pipeline) => pipeline?.Stages?.Any() == true;
    public bool CanApply(IKernelPipeline pipeline) => CanOptimize(pipeline);

    public async Task<IKernelPipeline> OptimizeAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
    {
        return await ApplyAsync(pipeline, cancellationToken);
    }

    public async Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
    {
        var settings = new PipelineOptimizationSettings
        {
            OptimizationTypes = OptimizationType.DeadCodeElimination,
            Level = (DotCompute.Abstractions.Pipelines.Models.OptimizationLevel)OptimizationLevel.Balanced
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
        var optimizedStages = new List<IPipelineStage>();
        var eliminatedStages = new List<string>();

        // Find stages whose outputs are never used
        var usedOutputs = FindUsedOutputs(stages);

        foreach (var stage in stages)
        {
            if (HasUsefulOutput(stage, usedOutputs))
            {
                optimizedStages.Add(stage);
            }
            else
            {
                eliminatedStages.Add(stage.Id);
            }
        }

        var wasApplied = eliminatedStages.Count > 0;
        var estimatedImpact = wasApplied ? eliminatedStages.Count * 0.05 : 0.0; // 5% per eliminated stage

        return new ValueTask<OptimizationResult>(new OptimizationResult
        {
            WasApplied = wasApplied,
            OptimizedStages = optimizedStages,
            Description = $"Eliminated {eliminatedStages.Count} unused stages",
            AffectedStages = eliminatedStages,
            EstimatedImpact = estimatedImpact,
            EstimatedMemorySavings = eliminatedStages.Count * 256 * 1024 // 256KB per stage
        });
    }

    private static HashSet<string> FindUsedOutputs(List<IPipelineStage> stages)
        // Simplified - in practice would analyze data flow

        => [.. stages.SelectMany(s => s.Dependencies)];

    private static bool HasUsefulOutput(IPipelineStage stage, HashSet<string> usedOutputs)
        // Check if stage produces useful output or has side effects

        => usedOutputs.Contains(stage.Id) || stage.Type == PipelineStageType.Computation;

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