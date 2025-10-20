// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Results;
using DotCompute.Core.Pipelines.Stages;

// Using aliases to resolve IStageMetrics conflicts
using IStageMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IStageMetrics;

namespace DotCompute.Core.Pipelines.Optimization.Models;

/// <summary>
/// Wrapper for fused kernels.
/// </summary>
internal sealed class FusedKernelStage(KernelStage stage1, KernelStage stage2) : IPipelineStage
{
    private readonly KernelStage _stage1 = stage1;
    private readonly KernelStage _stage2 = stage2;
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>

    public string Id { get; } = $"Fused_{stage1.Id}_{stage2.Id}";
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; } = $"Fused({stage1.Name}, {stage2.Name})";
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public PipelineStageType Type => PipelineStageType.Computation;
    /// <summary>
    /// Gets or sets the dependencies.
    /// </summary>
    /// <value>The dependencies.</value>
    public IReadOnlyList<string> Dependencies => _stage1.Dependencies;
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    /// <value>The metadata.</value>
    public IReadOnlyDictionary<string, object> Metadata => _stage1.Metadata;
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask<AbstractionsMemory.Models.Pipelines.StageExecutionResult> ExecuteAsync(
        AbstractionsMemory.Models.Pipelines.PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Execute both stages with optimized data flow
        var result1 = await _stage1.ExecuteAsync(context, cancellationToken);
        if (!result1.Success)
        {
            return new AbstractionsMemory.Models.Pipelines.StageExecutionResult
            {
                StageId = Id,
                Success = result1.Success,
                ExecutionTime = result1.ExecutionTime,
                OutputData = result1.OutputData ?? []
            };
        }

        var intermediateContext = new Pipelines.Models.PipelineExecutionContext();
        foreach (var kvp in result1.OutputData ?? context.Inputs)
        {
            intermediateContext.Inputs[kvp.Key] = kvp.Value;
        }
        intermediateContext.SetMemoryManager(context.MemoryManager);
        intermediateContext.SetDevice(context.Device);
        intermediateContext.Options = context.Options;

        var result2 = await _stage2.ExecuteAsync(intermediateContext, cancellationToken);

        return new AbstractionsMemory.Models.Pipelines.StageExecutionResult
        {
            StageId = Id,
            Success = result2.Success,
            ExecutionTime = result1.ExecutionTime + result2.ExecutionTime,
            OutputData = result2.OutputData ?? []
        };
    }
    /// <summary>
    /// Validates the .
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public AbstractionsMemory.Models.Pipelines.StageValidationResult Validate()
    {
        var result1 = _stage1.Validate();
        var result2 = _stage2.Validate();

        var errors = new List<string>();
        var warnings = new List<string>();

        if (result1.Errors != null)
        {
            errors.AddRange(result1.Errors);
        }

        if (result2.Errors != null)
        {
            errors.AddRange(result2.Errors);
        }

        if (result1.Warnings != null)
        {
            warnings.AddRange(result1.Warnings);
        }

        if (result2.Warnings != null)
        {
            warnings.AddRange(result2.Warnings);
        }

        return new AbstractionsMemory.Models.Pipelines.StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }
    /// <summary>
    /// Gets the metrics.
    /// </summary>
    /// <returns>The metrics.</returns>

    public IStageMetrics GetMetrics()
        // Combine metrics from both stages


        => _stage1.GetMetrics(); // Simplified

    private static MemoryUsageStats? CombineMemoryStats(MemoryUsageStats? stats1, MemoryUsageStats? stats2)
    {
        if (stats1 == null && stats2 == null)
        {
            return null;
        }

        if (stats1 == null)
        {
            return stats2;
        }

        if (stats2 == null)
        {
            return stats1;
        }

        return new MemoryUsageStats
        {
            TotalAllocatedBytes = stats1.TotalAllocatedBytes + stats2.TotalAllocatedBytes,
            PeakMemoryUsageBytes = Math.Max(stats1.PeakMemoryUsageBytes, stats2.PeakMemoryUsageBytes),
            AllocationCount = stats1.AllocationCount + stats2.AllocationCount
        };
    }
}


// MemoryUsageStats now uses the canonical version from DotCompute.Abstractions.Pipelines.Results