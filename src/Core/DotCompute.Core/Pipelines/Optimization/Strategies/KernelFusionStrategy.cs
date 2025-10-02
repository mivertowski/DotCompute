// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Pipelines.Stages;
using DotCompute.Core.Pipelines.Optimization.Models;
using DotCompute.Core.Pipelines.Optimization.Calculators;

namespace DotCompute.Core.Pipelines.Optimization.Strategies;

/// <summary>
/// Kernel fusion optimization strategy.
/// </summary>
internal sealed class KernelFusionStrategy : IOptimizationStrategy
{
    public string Name => "KernelFusion";
    public OptimizationType SupportedOptimizations => OptimizationType.KernelFusion;
    public OptimizationType Type => OptimizationType.KernelFusion;

    public bool CanOptimize(IKernelPipeline pipeline)
        => pipeline?.Stages?.Any(s => s.Type == PipelineStageType.Computation) == true;
    public bool CanApply(IKernelPipeline pipeline) => CanOptimize(pipeline);

    public async Task<IKernelPipeline> OptimizeAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default) => await ApplyAsync(pipeline, cancellationToken);

    public async Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default)
    {
        var settings = new PipelineOptimizationSettings
        {
            OptimizationTypes = OptimizationType.KernelFusion
        };

        var result = await ApplyInternalAsync([.. pipeline.Stages], settings, cancellationToken);
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
        var fusedStages = new List<string>();
        var memoryReduction = 0L;

        // Find consecutive kernel stages that can be fused
        for (var i = 0; i < optimizedStages.Count - 1; i++)
        {
            if (optimizedStages[i] is KernelStage kernelStage1 &&
                optimizedStages[i + 1] is KernelStage kernelStage2)
            {
                if (CanFuseKernels(kernelStage1, kernelStage2))
                {
                    var fusedStage = CreateFusedKernel(kernelStage1, kernelStage2);

                    optimizedStages.RemoveAt(i + 1);
                    optimizedStages[i] = fusedStage;

                    fusedStages.Add(kernelStage1.Id);
                    fusedStages.Add(kernelStage2.Id);

                    // Estimate memory reduction (intermediate buffer elimination)
                    memoryReduction += EstimateIntermediateBufferSize(kernelStage1, kernelStage2);

                    i--; // Check this position again
                }
            }
        }

        var wasApplied = fusedStages.Count > 0;
        var estimatedImpact = wasApplied ? 0.15 : 0.0; // 15% improvement from fusion

        return new ValueTask<OptimizationResult>(new OptimizationResult
        {
            WasApplied = wasApplied,
            OptimizedStages = optimizedStages,
            Description = $"Fused {fusedStages.Count / 2} kernel pairs",
            AffectedStages = fusedStages,
            EstimatedImpact = estimatedImpact,
            EstimatedMemorySavings = memoryReduction
        });
    }

    private static bool CanFuseKernels(KernelStage stage1, KernelStage stage2)
    {
        // Simplified fusion check - in practice this would be much more complex
        // Check if stage2 depends only on stage1's output
        return stage2.Dependencies.Count <= 1 &&
               (stage2.Dependencies.Count == 0 || stage2.Dependencies.Contains(stage1.Id));
    }

    private static IPipelineStage CreateFusedKernel(KernelStage stage1, KernelStage stage2)
        // Create a new fused kernel that combines both stages into a single execution unit
        // This optimization reduces kernel launch overhead and intermediate memory transfers

        => new FusedKernelStage(stage1, stage2);

    private static long EstimateIntermediateBufferSize(KernelStage stage1, KernelStage stage2)
    {
        // Intelligent buffer size calculation that considers multiple optimization factors
        var calculator = new IntelligentBufferSizeCalculator();

        return IntelligentBufferSizeCalculator.CalculateOptimalBufferSize(stage1, stage2);
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