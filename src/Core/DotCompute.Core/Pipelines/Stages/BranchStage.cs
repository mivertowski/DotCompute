// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Interfaces.Pipelines;

// Type aliases to resolve ambiguous references
using AbsStageExecutionResult = DotCompute.Abstractions.Models.Pipelines.StageExecutionResult;
using PipelineStageType = DotCompute.Abstractions.Pipelines.Enums.PipelineStageType;
using IStageMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IStageMetrics;
using StageValidationResult = DotCompute.Abstractions.Models.Pipelines.StageValidationResult;
using ValidationIssue = DotCompute.Abstractions.Validation.ValidationIssue;

namespace DotCompute.Core.Pipelines.Stages
{
    /// <summary>
    /// Stage that executes conditional branches.
    /// </summary>
    internal sealed class BranchStage(
        string id,
        Func<DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext, bool> condition,
        List<IPipelineStage> trueStages,
        List<IPipelineStage>? falseStages) : IPipelineStage
    {
        private readonly Func<DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext, bool> _condition = condition;
        private readonly List<IPipelineStage> _trueStages = trueStages;
        private readonly List<IPipelineStage>? _falseStages = falseStages;
        private readonly StageMetrics _metrics = new(id);

        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string Name { get; } = "Branch";

        /// <inheritdoc/>
        public PipelineStageType Type => PipelineStageType.Branch;

        /// <inheritdoc/>
        public IReadOnlyList<string> Dependencies { get; } = [];

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

        /// <inheritdoc/>
        public async ValueTask<DotCompute.Abstractions.Models.Pipelines.StageExecutionResult> ExecuteAsync(
            DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var conditionResult = _condition(context);
                var stagesToExecute = conditionResult ? _trueStages : _falseStages;

                var outputs = new Dictionary<string, object>(context.Inputs);

                if (stagesToExecute != null)
                {
                    foreach (var stage in stagesToExecute)
                    {
                        var stageContext = new DotCompute.Core.Pipelines.Models.PipelineExecutionContext();
                        foreach (var kvp in outputs)
                        {
                            stageContext.Inputs[kvp.Key] = kvp.Value;
                        }
                        stageContext.SetMemoryManager(context.MemoryManager);
                        stageContext.SetDevice(context.Device);
                        stageContext.Options = (context as DotCompute.Core.Pipelines.Models.PipelineExecutionContext)?.Options;

                        var result = await stage.ExecuteAsync(stageContext, cancellationToken);

                        if (!result.Success)
                        {
                            return new AbsStageExecutionResult
                            {
                                StageId = Id,
                                Success = false,
                                ExecutionTime = stopwatch.Elapsed,
                                OutputData = [],
                                Error = result.Error
                            };
                        }

                        if (result.OutputData != null)
                        {
                            foreach (var (key, value) in result.OutputData)
                            {
                                outputs[key] = value;
                            }
                        }
                    }
                }

                stopwatch.Stop();
                _metrics.RecordExecution(stopwatch.Elapsed, true);

                return new AbsStageExecutionResult
                {
                    StageId = Id,
                    Success = true,
                    ExecutionTime = stopwatch.Elapsed,
                    OutputData = outputs.Count > context.Inputs.Count ? outputs : [],
                    Metadata = new Dictionary<string, object>
                    {
                        ["BranchTaken"] = conditionResult ? 1 : 0,
                        ["StagesExecuted"] = stagesToExecute?.Count ?? 0
                    }
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
                    OutputData = [],
                    Error = ex
                };
            }
        }

        /// <inheritdoc/>
        public StageValidationResult Validate()
        {
            var errors = new List<ValidationIssue>();

            if (_condition == null)
            {
                errors.Add(new ValidationIssue("BRANCH_001", "Branch condition is required", AbstractionsMemory.Validation.ValidationSeverity.Error));
            }

            var warnings = new List<string>();
            if (_trueStages.Count == 0 && (_falseStages == null || _falseStages.Count == 0))
            {
                warnings.Add("Branch has no stages to execute");
            }

            return new StageValidationResult
            {
                IsValid = errors.Count == 0,
                Issues = errors.Count > 0 ? errors : null,
                Warnings = warnings.Count > 0 ? warnings : null
            };
        }


        /// <inheritdoc/>
        public IStageMetrics GetMetrics() => _metrics;
    }
}