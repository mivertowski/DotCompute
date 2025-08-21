// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

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
        public IReadOnlyList<string> Dependencies { get; } = new List<string>();

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Metadata => _pipeline.Metadata;

        /// <inheritdoc/>
        public async ValueTask<StageExecutionResult> ExecuteAsync(
            PipelineExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await _pipeline.ExecuteAsync(context, cancellationToken);

                stopwatch.Stop();
                _metrics.RecordExecution(stopwatch.Elapsed, result.Success);

                return new StageExecutionResult
                {
                    StageId = Id,
                    Success = result.Success,
                    Duration = stopwatch.Elapsed,
                    Outputs = result.Outputs,
                    MemoryUsage = result.Metrics.MemoryUsage,
                    Error = result.Errors?.FirstOrDefault()?.Exception
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
            var pipelineValidation = _pipeline.Validate();

            return new StageValidationResult
            {
                IsValid = pipelineValidation.IsValid,
                Errors = pipelineValidation.Errors?.Select(e => e.Message).ToList(),
                Warnings = pipelineValidation.Warnings?.Select(w => w.Message).ToList()
            };
        }

        /// <inheritdoc/>
        public IStageMetrics GetMetrics() => _metrics;
    }
}