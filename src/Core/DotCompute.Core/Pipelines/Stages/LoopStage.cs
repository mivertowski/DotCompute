// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Linq;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Core.Telemetry;
using DotCompute.Core.Pipelines.Models;

// Type aliases to resolve ambiguous references
using PipelineExecutionContext = DotCompute.Core.Pipelines.Models.PipelineExecutionContext;
using AbsStageExecutionResult = DotCompute.Abstractions.Models.Pipelines.StageExecutionResult;
using CoreStageExecutionResult = DotCompute.Core.Pipelines.Models.StageExecutionResult;
using PipelineStageType = DotCompute.Abstractions.Pipelines.Enums.PipelineStageType;
using IStageMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IStageMetrics;
using StageValidationResult = DotCompute.Abstractions.Models.Pipelines.StageValidationResult;
using ValidationIssue = DotCompute.Abstractions.ValidationIssue;

namespace DotCompute.Core.Pipelines.Stages
{
    /// <summary>
    /// Stage that executes loops.
    /// </summary>
    internal sealed class LoopStage(
        string id,
        Func<DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext, int, bool> condition,
        List<IPipelineStage> bodyStages) : IPipelineStage
    {
        private readonly Func<DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext, int, bool> _condition = condition;
        private readonly List<IPipelineStage> _bodyStages = bodyStages;
        private readonly StageMetrics _metrics = new(id);

        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string Name { get; } = "Loop";

        /// <inheritdoc/>
        public PipelineStageType Type => PipelineStageType.Loop;

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
                var outputs = new Dictionary<string, object>(context.Inputs);
                var iteration = 0;

                while (_condition(context, iteration) && !cancellationToken.IsCancellationRequested)
                {
                    foreach (var stage in _bodyStages)
                    {
                        var stageContext = new PipelineExecutionContext();
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

                    iteration++;
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
                        ["IterationsCompleted"] = iteration,
                        ["AverageIterationTime"] = iteration > 0 ? stopwatch.Elapsed.TotalMilliseconds / iteration : 0
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
                errors.Add(ValidationIssue.Error("LOOP_001", "Loop condition is required"));
            }

            var warnings = new List<string>();
            if (_bodyStages.Count == 0)
            {
                warnings.Add("Loop has no body stages");
            }

            return new StageValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = [.. errors.Select(e => e.Message)],
                Warnings = warnings.Count > 0 ? warnings : null
            };
        }


        /// <inheritdoc/>
        public IStageMetrics GetMetrics() => _metrics;
    }
}