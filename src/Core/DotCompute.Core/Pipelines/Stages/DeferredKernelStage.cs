// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using AbsStageExecutionResult = DotCompute.Abstractions.Models.Pipelines.StageExecutionResult;
using IStageMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IStageMetrics;
using PipelineExecutionContext = DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext;
using PipelineStageType = DotCompute.Abstractions.Pipelines.Enums.PipelineStageType;
using StageValidationResult = DotCompute.Abstractions.Models.Pipelines.StageValidationResult;
using ValidationIssue = DotCompute.Abstractions.Validation.ValidationIssue;

namespace DotCompute.Core.Pipelines.Stages;

/// <summary>
/// A deferred kernel stage that represents a kernel reference by name.
/// The actual kernel instance is resolved at pipeline execution time
/// via a kernel registry or compilation service.
/// </summary>
/// <remarks>
/// This stage type is used when building pipelines where the kernel
/// is not yet compiled or the kernel instance is managed by a registry.
/// Before execution, the pipeline executor must resolve this stage
/// to a concrete KernelStage using the kernel name.
/// </remarks>
internal sealed class DeferredKernelStage : IPipelineStage
{
    private readonly string _kernelName;
    private readonly Action<IKernelStageBuilder>? _stageBuilderAction;
    private readonly StageMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredKernelStage"/> class.
    /// </summary>
    /// <param name="kernelName">The name of the kernel to be resolved.</param>
    /// <param name="stageBuilderAction">Optional configuration action for the stage.</param>
    public DeferredKernelStage(string kernelName, Action<IKernelStageBuilder>? stageBuilderAction)
    {
        _kernelName = kernelName ?? throw new ArgumentNullException(nameof(kernelName));
        _stageBuilderAction = stageBuilderAction;
        Id = $"DeferredKernel_{kernelName}_{Guid.NewGuid():N}";
        _metrics = new StageMetrics(Id);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Name => $"Deferred:{_kernelName}";

    /// <inheritdoc/>
    public PipelineStageType Type => PipelineStageType.Computation;

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies { get; } = Array.Empty<string>();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>
    {
        ["IsDeferred"] = true,
        ["RequiresResolution"] = true
    };

    /// <summary>
    /// Gets the kernel name that needs to be resolved.
    /// </summary>
    public string KernelName => _kernelName;

    /// <summary>
    /// Gets the stage builder action if any configuration was provided.
    /// </summary>
    public Action<IKernelStageBuilder>? StageBuilderAction => _stageBuilderAction;

    /// <inheritdoc/>
    public ValueTask<AbsStageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Deferred stages cannot be executed directly - they must be resolved first
        _metrics.RecordExecution(TimeSpan.Zero, false);

        return ValueTask.FromResult(new AbsStageExecutionResult
        {
            StageId = Id,
            Success = false,
            ExecutionTime = TimeSpan.Zero,
            OutputData = new Dictionary<string, object>(),
            Error = new InvalidOperationException(
                $"Deferred kernel stage '{_kernelName}' must be resolved to a concrete kernel " +
                $"before execution. Use a kernel resolver or registry to resolve the kernel by name.")
        });
    }

    /// <inheritdoc/>
    public StageValidationResult Validate()
    {
        var errors = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(_kernelName))
        {
            errors.Add(new ValidationIssue(
                "DEFERRED_001",
                "Kernel name is required for deferred kernel stages",
                DotCompute.Abstractions.Validation.ValidationSeverity.Error));
        }

        // Add a warning that this stage needs resolution
        var warnings = new List<string>
        {
            $"Deferred kernel '{_kernelName}' must be resolved before pipeline execution"
        };

        return new StageValidationResult
        {
            IsValid = errors.Count == 0,
            Issues = errors.Count > 0 ? errors : null,
            Warnings = warnings
        };
    }

    /// <inheritdoc/>
    public IStageMetrics GetMetrics() => _metrics;
}
