// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Core.Pipelines.Stages
{
    /// <summary>
    /// Stage that executes conditional branches.
    /// </summary>
    internal sealed class BranchStage(
        string id,
        Func<PipelineExecutionContext, bool> condition,
        List<IPipelineStage> trueStages,
        List<IPipelineStage>? falseStages) : IPipelineStage
    {
        private readonly Func<PipelineExecutionContext, bool> _condition = condition;
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
        public async ValueTask<StageExecutionResult> ExecuteAsync(
            PipelineExecutionContext context,
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
                        var stageContext = new PipelineExecutionContext
                        {
                            Inputs = outputs,
                            MemoryManager = context.MemoryManager,
                            Device = context.Device,
                            Options = context.Options,
                            Profiler = context.Profiler
                        };

                        var result = await stage.ExecuteAsync(stageContext, cancellationToken);

                        if (!result.Success)
                        {
                            return new StageExecutionResult
                            {
                                StageId = Id,
                                Success = false,
                                Duration = stopwatch.Elapsed,
                                Error = result.Error
                            };
                        }

                        if (result.Outputs != null)
                        {
                            foreach (var (key, value) in result.Outputs)
                            {
                                outputs[key] = value;
                            }
                        }
                    }
                }

                stopwatch.Stop();
                _metrics.RecordExecution(stopwatch.Elapsed, true);

                return new StageExecutionResult
                {
                    StageId = Id,
                    Success = true,
                    Duration = stopwatch.Elapsed,
                    Outputs = outputs.Count > context.Inputs.Count ? outputs : null,
                    Metrics = new Dictionary<string, double>
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

                return new StageExecutionResult
                {
                    StageId = Id,
                    Success = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex
                };
            }
        }

        /// <inheritdoc/>
        public StageValidationResult Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (_condition == null)
            {
                errors.Add("Branch condition is required");
            }

            if (_trueStages.Count == 0 && (_falseStages == null || _falseStages.Count == 0))
            {
                warnings.Add("Branch has no stages to execute");
            }

            return new StageValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.Count > 0 ? errors : null,
                Warnings = warnings.Count > 0 ? warnings : null
            };
        }

        /// <inheritdoc/>
        public IStageMetrics GetMetrics() => _metrics;
    }
}