// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Pipelines.Optimization.Models;

namespace DotCompute.Core.Pipelines.Optimization.Strategies;

/// <summary>
/// Dead code elimination strategy.
/// </summary>
internal sealed class DeadCodeEliminationStrategy : IOptimizationStrategy
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name => "DeadCodeElimination";
    /// <summary>
    /// Gets or sets the supported optimizations.
    /// </summary>
    /// <value>The supported optimizations.</value>
    public OptimizationType SupportedOptimizations => OptimizationType.DeadCodeElimination;
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public OptimizationType Type => OptimizationType.DeadCodeElimination;
    /// <summary>
    /// Determines whether optimize.
    /// </summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

    public bool CanOptimize(IKernelPipeline pipeline) => pipeline?.Stages?.Any() == true;
    /// <summary>
    /// Determines whether apply.
    /// </summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>
    public bool CanApply(IKernelPipeline pipeline) => CanOptimize(pipeline);
    /// <summary>
    /// Gets optimize asynchronously.
    /// </summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<IKernelPipeline> OptimizeAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default) => await ApplyAsync(pipeline, cancellationToken);
    /// <summary>
    /// Gets apply asynchronously.
    /// </summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
    {
        var settings = new PipelineOptimizationSettings
        {
            OptimizationTypes = OptimizationType.DeadCodeElimination,
            Level = OptimizationLevel.Balanced
        };

        var result = await ApplyInternalAsync([.. pipeline.Stages], settings, cancellationToken);
        if (result.WasApplied)
        {
            return CreateOptimizedPipeline(pipeline, result.OptimizedStages, settings);
        }
        return pipeline;
    }
    /// <summary>
    /// Gets apply internal asynchronously.
    /// </summary>
    /// <param name="stages">The stages.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public static ValueTask<OptimizationResult> ApplyInternalAsync(
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

    private static HashSet<string> FindUsedOutputs(IReadOnlyList<IPipelineStage> stages)
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
