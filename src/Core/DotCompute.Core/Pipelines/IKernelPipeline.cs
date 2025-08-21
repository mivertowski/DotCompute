// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using DotCompute.Core.Device.Interfaces;

namespace DotCompute.Core.Pipelines
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
        public IPipelineMetrics GetMetrics();

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
        public StageValidationResult Validate();

        /// <summary>
        /// Gets performance metrics for this stage.
        /// </summary>
        public IStageMetrics GetMetrics();
    }

    /// <summary>
    /// Pipeline stage types.
    /// </summary>
    public enum PipelineStageType
    {
        /// <summary>
        /// Single kernel execution.
        /// </summary>
        Kernel,

        /// <summary>
        /// Parallel execution of multiple stages.
        /// </summary>
        Parallel,

        /// <summary>
        /// Sequential execution of multiple stages.
        /// </summary>
        Sequential,

        /// <summary>
        /// Conditional branching.
        /// </summary>
        Branch,

        /// <summary>
        /// Loop execution.
        /// </summary>
        Loop,

        /// <summary>
        /// Custom stage.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Execution context for the pipeline.
    /// </summary>
    public sealed class PipelineExecutionContext
    {
        /// <summary>
        /// Gets or sets the input data.
        /// </summary>
        public required IReadOnlyDictionary<string, object> Inputs { get; init; }

        /// <summary>
        /// Gets the shared memory manager.
        /// </summary>
        public required IPipelineMemoryManager MemoryManager { get; init; }

        /// <summary>
        /// Gets the compute device.
        /// </summary>
        public required IComputeDevice Device { get; init; }

        /// <summary>
        /// Gets or sets execution options.
        /// </summary>
        public PipelineExecutionOptions Options { get; init; } = PipelineExecutionOptions.Default;

        /// <summary>
        /// Gets the execution state that can be shared between stages.
        /// </summary>
        public IDictionary<string, object> State { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the performance profiler.
        /// </summary>
        public IPipelineProfiler? Profiler { get; init; }
    }

    /// <summary>
    /// Pipeline execution options.
    /// </summary>
    public sealed class PipelineExecutionOptions
    {
        /// <summary>
        /// Gets or sets whether to enable profiling.
        /// </summary>
        public bool EnableProfiling { get; set; }

        /// <summary>
        /// Gets or sets whether to enable detailed logging.
        /// </summary>
        public bool EnableDetailedLogging { get; set; }

        /// <summary>
        /// Gets or sets the maximum parallel stages.
        /// </summary>
        public int MaxParallelStages { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets whether to continue on error.
        /// </summary>
        public bool ContinueOnError { get; set; }

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public static PipelineExecutionOptions Default { get; } = new();
    }

    /// <summary>
    /// Result of pipeline execution.
    /// </summary>
    public sealed class PipelineExecutionResult
    {
        /// <summary>
        /// Gets whether the execution was successful.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the output data.
        /// </summary>
        public required IReadOnlyDictionary<string, object> Outputs { get; init; }

        /// <summary>
        /// Gets execution metrics.
        /// </summary>
        public required PipelineExecutionMetrics Metrics { get; init; }

        /// <summary>
        /// Gets any errors that occurred.
        /// </summary>
        public IReadOnlyList<PipelineError>? Errors { get; init; }

        /// <summary>
        /// Gets stage results.
        /// </summary>
        public required IReadOnlyList<StageExecutionResult> StageResults { get; init; }
    }

    /// <summary>
    /// Pipeline validation result.
    /// </summary>
    public sealed class PipelineValidationResult
    {
        /// <summary>
        /// Gets whether the pipeline is valid.
        /// </summary>
        public required bool IsValid { get; init; }

        /// <summary>
        /// Gets validation errors.
        /// </summary>
        public IReadOnlyList<ValidationError>? Errors { get; init; }

        /// <summary>
        /// Gets validation warnings.
        /// </summary>
        public IReadOnlyList<ValidationWarning>? Warnings { get; init; }
    }

    /// <summary>
    /// Pipeline optimization settings.
    /// </summary>
    public sealed class PipelineOptimizationSettings
    {
        /// <summary>
        /// Gets or sets whether to enable kernel fusion.
        /// </summary>
        public bool EnableKernelFusion { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable stage reordering.
        /// </summary>
        public bool EnableStageReordering { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable memory optimization.
        /// </summary>
        public bool EnableMemoryOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable buffer reuse.
        /// </summary>
        public bool EnableBufferReuse { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable streaming support.
        /// </summary>
        public bool EnableStreaming { get; set; }


        /// <summary>
        /// Gets or sets whether to enable parallel stage merging.
        /// </summary>
        public bool EnableParallelMerging { get; set; } = true;

        /// <summary>
        /// Gets or sets the optimization level.
        /// </summary>
        public PipelineOptimizationLevel Level { get; set; } = PipelineOptimizationLevel.Balanced;
    }

    /// <summary>
    /// Pipeline optimization levels.
    /// </summary>
    public enum PipelineOptimizationLevel
    {
        /// <summary>
        /// No optimization.
        /// </summary>
        None,

        /// <summary>
        /// Conservative optimization.
        /// </summary>
        Conservative,

        /// <summary>
        /// Balanced optimization.
        /// </summary>
        Balanced,

        /// <summary>
        /// Aggressive optimization.
        /// </summary>
        Aggressive
    }

    /// <summary>
    /// Error handling result options.
    /// </summary>
    public enum ErrorHandlingResult
    {
        /// <summary>
        /// Continue execution, ignoring the error.
        /// </summary>
        Continue,

        /// <summary>
        /// Retry the failed operation.
        /// </summary>
        Retry,

        /// <summary>
        /// Skip the failed stage and continue.
        /// </summary>
        Skip,

        /// <summary>
        /// Abort the entire pipeline execution.
        /// </summary>
        Abort
    }

    /// <summary>
    /// Interface for pipeline optimization.
    /// </summary>
    public interface IPipelineOptimizer
    {
        /// <summary>
        /// Optimizes a pipeline.
        /// </summary>
        public ValueTask<OptimizedPipeline> OptimizeAsync(
            IKernelPipeline pipeline,
            PipelineOptimizationSettings settings,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of pipeline optimization.
    /// </summary>
    public sealed class OptimizedPipeline
    {
        /// <summary>
        /// Gets the optimized pipeline.
        /// </summary>
        public required IKernelPipeline Pipeline { get; init; }

        /// <summary>
        /// Gets the optimizations applied.
        /// </summary>
        public required IReadOnlyList<AppliedOptimization> AppliedOptimizations { get; init; }

        /// <summary>
        /// Gets the estimated performance improvement.
        /// </summary>
        public required double EstimatedSpeedup { get; init; }

        /// <summary>
        /// Gets the estimated memory savings.
        /// </summary>
        public required long EstimatedMemorySavings { get; init; }
    }

    /// <summary>
    /// Represents an applied optimization.
    /// </summary>
    public sealed class AppliedOptimization
    {
        /// <summary>
        /// Gets the optimization type.
        /// </summary>
        public required OptimizationType Type { get; init; }

        /// <summary>
        /// Gets the optimization description.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Gets the affected stages.
        /// </summary>
        public required IReadOnlyList<string> AffectedStages { get; init; }

        /// <summary>
        /// Gets the estimated impact.
        /// </summary>
        public required double EstimatedImpact { get; init; }
    }

    /// <summary>
    /// Types of pipeline optimizations.
    /// </summary>
    public enum OptimizationType
    {
        /// <summary>
        /// Kernel fusion optimization.
        /// </summary>
        KernelFusion,

        /// <summary>
        /// Stage reordering.
        /// </summary>
        StageReordering,

        /// <summary>
        /// Memory layout optimization.
        /// </summary>
        MemoryOptimization,

        /// <summary>
        /// Parallel stage merging.
        /// </summary>
        ParallelMerging,

        /// <summary>
        /// Dead code elimination.
        /// </summary>
        DeadCodeElimination,

        /// <summary>
        /// Loop unrolling.
        /// </summary>
        LoopUnrolling,

        /// <summary>
        /// Constant propagation.
        /// </summary>
        ConstantPropagation
    }

    /// <summary>
    /// Pipeline event for monitoring.
    /// </summary>
    public sealed class PipelineEvent
    {
        /// <summary>
        /// Gets the event type.
        /// </summary>
        public required PipelineEventType Type { get; init; }

        /// <summary>
        /// Gets the event timestamp.
        /// </summary>
        public required DateTime Timestamp { get; init; }

        /// <summary>
        /// Gets the stage ID if applicable.
        /// </summary>
        public string? StageId { get; init; }

        /// <summary>
        /// Gets the event message.
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Gets additional event data.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Data { get; init; }
    }

    /// <summary>
    /// Pipeline event types.
    /// </summary>
    public enum PipelineEventType
    {
        /// <summary>
        /// Pipeline started.
        /// </summary>
        Started,

        /// <summary>
        /// Pipeline completed.
        /// </summary>
        Completed,

        /// <summary>
        /// Stage started.
        /// </summary>
        StageStarted,

        /// <summary>
        /// Stage completed.
        /// </summary>
        StageCompleted,

        /// <summary>
        /// Error occurred.
        /// </summary>
        Error,

        /// <summary>
        /// Warning raised.
        /// </summary>
        Warning,

        /// <summary>
        /// Optimization applied.
        /// </summary>
        OptimizationApplied,

        /// <summary>
        /// Memory allocated.
        /// </summary>
        MemoryAllocated,

        /// <summary>
        /// Memory released.
        /// </summary>
        MemoryReleased
    }
}
