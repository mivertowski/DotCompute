// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Interfaces.Pipelines;

// Type aliases to resolve ambiguous references
using PipelineExecutionContext = DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext;
using AbsStageExecutionResult = DotCompute.Abstractions.Models.Pipelines.StageExecutionResult;
using PipelineStageType = DotCompute.Abstractions.Pipelines.Enums.PipelineStageType;
using IStageMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IStageMetrics;
using DotCompute.Abstractions.Validation;
using StageValidationResult = DotCompute.Abstractions.Models.Pipelines.StageValidationResult;
using ValidationIssue = DotCompute.Abstractions.Validation.ValidationIssue;
using IKernelPipeline = DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline;

namespace DotCompute.Core.Pipelines.Stages
{
    /// <summary>
    /// Stage that wraps a pipeline as a stage.
    /// </summary>
    internal sealed class PipelineWrapperStage(string id, string name, IKernelPipeline pipeline) : IPipelineStage
    {
        private readonly IKernelPipeline _pipeline = pipeline;
        private readonly StageMetrics _metrics = new(id);

        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string Name { get; } = name;

        /// <inheritdoc/>
        public PipelineStageType Type => PipelineStageType.Custom;

        /// <inheritdoc/>
        public IReadOnlyList<string> Dependencies { get; init; } = [];

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Metadata => _pipeline.Metadata;

        /// <inheritdoc/>
        public async ValueTask<AbsStageExecutionResult> ExecuteAsync(
            PipelineExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await _pipeline.ExecuteAsync(context, cancellationToken);

                stopwatch.Stop();
                _metrics.RecordExecution(stopwatch.Elapsed, result.Success);

                return new AbsStageExecutionResult
                {
                    StageId = Id,
                    Success = result.Success,
                    ExecutionTime = stopwatch.Elapsed,
                    MemoryUsed = result.Metrics?.PeakMemoryUsage ?? 0L,
                    Error = result.Errors is { Count: > 0 } ? result.Errors[0].Exception : null,
                    OutputData = result.Outputs?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? []
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metrics.RecordExecution(stopwatch.Elapsed, false);

                return new AbsStageExecutionResult
                {
                    StageId = Id,
                    Success = false,
                    ExecutionTime = stopwatch.Elapsed,
                    Error = ex,
                    OutputData = []
                };
            }
        }

        /// <inheritdoc/>
        public StageValidationResult Validate()
        {
            var pipelineValidation = _pipeline.Validate();
            var errors = new List<ValidationIssue>();
            var warnings = new List<string>();

            // Add errors from pipeline validation
            if (pipelineValidation.Errors != null)
            {
                foreach (var error in pipelineValidation.Errors)
                {
                    errors.Add(new ValidationIssue("PIPELINE_001", error.Message, ValidationSeverity.Error));
                }
            }

            // Add warnings from pipeline validation
            if (pipelineValidation.Warnings != null)
            {
                foreach (var warning in pipelineValidation.Warnings)
                {
                    warnings.Add(warning.Message);
                }
            }

            return new StageValidationResult
            {
                IsValid = pipelineValidation.IsValid,
                Issues = errors.Count > 0 ? errors : null,
                Warnings = warnings.Count > 0 ? warnings : null
            };
        }

        /// <inheritdoc/>
        public IStageMetrics GetMetrics() => _metrics;
    }
}