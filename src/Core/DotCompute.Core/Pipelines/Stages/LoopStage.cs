// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Core.Pipelines.Stages
{
    /// <summary>
    /// Stage that executes loops.
    /// </summary>
    internal sealed class LoopStage(
        string id,
        Func<PipelineExecutionContext, int, bool> condition,
        List<IPipelineStage> bodyStages) : IPipelineStage
    {
        private readonly Func<PipelineExecutionContext, int, bool> _condition = condition;
        private readonly List<IPipelineStage> _bodyStages = bodyStages;
        private readonly StageMetrics _metrics = new(id);

        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string Name { get; } = "Loop";

        /// <inheritdoc/>
        public PipelineStageType Type => PipelineStageType.Loop;

        /// <inheritdoc/>
        public IReadOnlyList<string> Dependencies { get; } = new List<string>();

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
                var outputs = new Dictionary<string, object>(context.Inputs);
                var iteration = 0;

                while (_condition(context, iteration) && !cancellationToken.IsCancellationRequested)
                {
                    foreach (var stage in _bodyStages)
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

                    iteration++;
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
                        ["IterationsCompleted"] = iteration,
                        ["AverageIterationTime"] = iteration > 0 ? stopwatch.Elapsed.TotalMilliseconds / iteration : 0
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
                errors.Add("Loop condition is required");
            }

            if (_bodyStages.Count == 0)
            {
                warnings.Add("Loop has no body stages");
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