// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Pipelines.Profiling;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Abstractions.Pipelines.Results;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Interfaces.Pipelines.Interfaces;

// Using aliases to resolve interface conflicts
using IPipelineMetricsInterface = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IPipelineMetrics;
using IPipelineMetricsModel = DotCompute.Abstractions.Models.Pipelines.IPipelineMetrics;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Abstractions.Interfaces.Pipelines
{
    /// <summary>
    /// Represents a pipeline of kernel operations that can be executed in sequence or parallel.
    /// </summary>
    public interface IKernelPipeline : IAsyncDisposable
    {
        /// <summary>
        /// Gets the pipeline identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the pipeline name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the pipeline stages.
        /// </summary>
        public IReadOnlyList<IPipelineStage> Stages { get; }

        /// <summary>
        /// Gets the pipeline optimization settings.
        /// </summary>
        public PipelineOptimizationSettings OptimizationSettings { get; }

        /// <summary>
        /// Gets the pipeline metadata.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata { get; }

        /// <summary>
        /// Executes the pipeline asynchronously.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The execution result.</returns>
        public ValueTask<PipelineExecutionResult> ExecuteAsync(
            PipelineExecutionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the pipeline configuration.
        /// </summary>
        /// <returns>Validation result with any errors or warnings.</returns>
        public PipelineValidationResult Validate();

        /// <summary>
        /// Gets performance metrics for the pipeline.
        /// </summary>
        /// <returns>Performance metrics.</returns>
        public IPipelineMetricsInterface GetMetrics();

        /// <summary>
        /// Optimizes the pipeline by applying transformations like kernel fusion.
        /// </summary>
        /// <param name="optimizer">The pipeline optimizer.</param>
        /// <returns>An optimized pipeline.</returns>
        public ValueTask<IKernelPipeline> OptimizeAsync(IPipelineOptimizer optimizer);
    }

    /// <summary>
    /// Represents a builder for creating kernel pipelines with a fluent API.
    /// </summary>
    public interface IKernelPipelineBuilder
    {
        /// <summary>
        /// Sets the pipeline name.
        /// </summary>
        public IKernelPipelineBuilder WithName(string name);

        /// <summary>
        /// Adds a kernel stage to the pipeline.
        /// </summary>
        public IKernelPipelineBuilder AddKernel(
            string name,
            ICompiledKernel kernel,
            Action<IKernelStageBuilder>? configure = null);

        /// <summary>
        /// Adds a parallel stage that executes multiple kernels concurrently.
        /// </summary>
        public IKernelPipelineBuilder AddParallel(Action<IParallelStageBuilder> configure);

        /// <summary>
        /// Adds a branching stage that conditionally executes different paths.
        /// </summary>
        public IKernelPipelineBuilder AddBranch(
            Func<PipelineExecutionContext, bool> condition,
            Action<IKernelPipelineBuilder> trueBranch,
            Action<IKernelPipelineBuilder>? falseBranch = null);

        /// <summary>
        /// Adds a loop stage that repeats execution.
        /// </summary>
        public IKernelPipelineBuilder AddLoop(
            Func<PipelineExecutionContext, int, bool> condition,
            Action<IKernelPipelineBuilder> body);

        /// <summary>
        /// Adds a custom stage.
        /// </summary>
        public IKernelPipelineBuilder AddStage(IPipelineStage stage);

        /// <summary>
        /// Adds metadata to the pipeline.
        /// </summary>
        public IKernelPipelineBuilder WithMetadata(string key, object value);

        /// <summary>
        /// Configures optimization settings.
        /// </summary>
        public IKernelPipelineBuilder WithOptimization(Action<PipelineOptimizationSettings> configure);

        /// <summary>
        /// Adds error handling to the pipeline.
        /// </summary>
        public IKernelPipelineBuilder WithErrorHandler(Func<Exception, PipelineExecutionContext, ErrorHandlingResult> handler);

        /// <summary>
        /// Adds a callback for pipeline events.
        /// </summary>
        public IKernelPipelineBuilder WithEventHandler(Action<PipelineEvent> handler);

        /// <summary>
        /// Builds the pipeline.
        /// </summary>
        public IKernelPipeline Build();
    }

    /// <summary>
    /// Represents a stage in the kernel pipeline.
    /// </summary>
    public interface IPipelineStage
    {
        /// <summary>
        /// Gets the stage identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the stage name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the stage type.
        /// </summary>
        public PipelineStageType Type { get; }

        /// <summary>
        /// Gets the stage dependencies.
        /// </summary>
        public IReadOnlyList<string> Dependencies { get; }

        /// <summary>
        /// Gets the stage metadata.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata { get; }

        /// <summary>
        /// Executes the stage.
        /// </summary>
        public ValueTask<StageExecutionResult> ExecuteAsync(
            PipelineExecutionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the stage configuration.
        /// </summary>
        public Models.Pipelines.StageValidationResult Validate();

        /// <summary>
        /// Gets performance metrics for this stage.
        /// </summary>
        public IStageMetrics GetMetrics();
    }
}
