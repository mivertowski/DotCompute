// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Core.Pipelines.Stages;
using DotCompute.Abstractions.Interfaces.Pipelines.Interfaces;

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

    public string Id { get; } = $"Fused_{stage1.Id}_{stage2.Id}";
    public string Name { get; } = $"Fused({stage1.Name}, {stage2.Name})";
    public PipelineStageType Type => PipelineStageType.Computation;
    public IReadOnlyList<string> Dependencies => _stage1.Dependencies;
    public IReadOnlyDictionary<string, object> Metadata => _stage1.Metadata;

    public async ValueTask<DotCompute.Abstractions.Models.Pipelines.StageExecutionResult> ExecuteAsync(
        DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Execute both stages with optimized data flow
        var result1 = await _stage1.ExecuteAsync(context, cancellationToken);
        if (!result1.Success)
        {
            return new DotCompute.Abstractions.Models.Pipelines.StageExecutionResult
            {
                StageId = Id,
                Success = result1.Success,
                ExecutionTime = result1.ExecutionTime,
                OutputData = result1.OutputData ?? new Dictionary<string, object>()
            };
        }

        var intermediateContext = new DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext();
        foreach (var kvp in result1.OutputData ?? context.Inputs)
        {
            intermediateContext.Inputs[kvp.Key] = kvp.Value;
        }
        intermediateContext.SetMemoryManager(context.MemoryManager);
        intermediateContext.SetDevice(context.Device);
        intermediateContext.Options = context.Options;

        var result2 = await _stage2.ExecuteAsync(intermediateContext, cancellationToken);

        return new DotCompute.Abstractions.Models.Pipelines.StageExecutionResult
        {
            StageId = Id,
            Success = result2.Success,
            ExecutionTime = result1.ExecutionTime + result2.ExecutionTime,
            OutputData = result2.OutputData ?? new Dictionary<string, object>()
        };
    }

    public DotCompute.Abstractions.Models.Pipelines.StageValidationResult Validate()
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

        return new DotCompute.Abstractions.Models.Pipelines.StageValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Count > 0 ? errors : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

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
            AllocatedBytes = stats1.AllocatedBytes + stats2.AllocatedBytes,
            PeakBytes = Math.Max(stats1.PeakBytes, stats2.PeakBytes),
            AllocationCount = stats1.AllocationCount + stats2.AllocationCount,
            DeallocationCount = stats1.DeallocationCount + stats2.DeallocationCount
        };
    }
}

/// <summary>
/// Memory usage statistics.
/// </summary>
public sealed class MemoryUsageStats
{
    /// <summary>
    /// Gets the allocated bytes.
    /// </summary>
    public required long AllocatedBytes { get; init; }

    /// <summary>
    /// Gets the peak bytes.
    /// </summary>
    public required long PeakBytes { get; init; }

    /// <summary>
    /// Gets the allocation count.
    /// </summary>
    public required int AllocationCount { get; init; }

    /// <summary>
    /// Gets the deallocation count.
    /// </summary>
    public required int DeallocationCount { get; init; }
}