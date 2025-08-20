// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Represents execution result for a pipeline stage.
    /// </summary>
    public sealed class StageExecutionResult
    {
        /// <summary>
        /// Gets the stage identifier.
        /// </summary>
        public required string StageId { get; init; }

        /// <summary>
        /// Gets whether the stage execution was successful.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the stage outputs.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Outputs { get; init; }

        /// <summary>
        /// Gets the execution duration.
        /// </summary>
        public required TimeSpan Duration { get; init; }

        /// <summary>
        /// Gets memory usage statistics.
        /// </summary>
        public MemoryUsageStats? MemoryUsage { get; init; }

        /// <summary>
        /// Gets any error that occurred.
        /// </summary>
        public Exception? Error { get; init; }

        /// <summary>
        /// Gets stage-specific metrics.
        /// </summary>
        public IReadOnlyDictionary<string, double>? Metrics { get; init; }
    }

    /// <summary>
    /// Represents validation result for a pipeline stage.
    /// </summary>
    public sealed class StageValidationResult
    {
        /// <summary>
        /// Gets whether the stage is valid.
        /// </summary>
        public required bool IsValid { get; init; }

        /// <summary>
        /// Gets validation errors.
        /// </summary>
        public IReadOnlyList<string>? Errors { get; init; }

        /// <summary>
        /// Gets validation warnings.
        /// </summary>
        public IReadOnlyList<string>? Warnings { get; init; }
    }

    /// <summary>
    /// Interface for stage performance metrics.
    /// </summary>
    public interface IStageMetrics
    {
        /// <summary>
        /// Gets the total execution count.
        /// </summary>
        public long ExecutionCount { get; }

        /// <summary>
        /// Gets the average execution time.
        /// </summary>
        public TimeSpan AverageExecutionTime { get; }

        /// <summary>
        /// Gets the minimum execution time.
        /// </summary>
        public TimeSpan MinExecutionTime { get; }

        /// <summary>
        /// Gets the maximum execution time.
        /// </summary>
        public TimeSpan MaxExecutionTime { get; }

        /// <summary>
        /// Gets the total execution time.
        /// </summary>
        public TimeSpan TotalExecutionTime { get; }

        /// <summary>
        /// Gets the error count.
        /// </summary>
        public long ErrorCount { get; }

        /// <summary>
        /// Gets the success rate.
        /// </summary>
        public double SuccessRate { get; }

        /// <summary>
        /// Gets average memory usage.
        /// </summary>
        public long AverageMemoryUsage { get; }

        /// <summary>
        /// Gets custom metrics.
        /// </summary>
        public IReadOnlyDictionary<string, double> CustomMetrics { get; }
    }

    /// <summary>
    /// Builder interface for kernel stages.
    /// </summary>
    public interface IKernelStageBuilder
    {
        /// <summary>
        /// Sets the stage name.
        /// </summary>
        public IKernelStageBuilder WithName(string name);

        /// <summary>
        /// Sets the work size for the kernel.
        /// </summary>
        public IKernelStageBuilder WithWorkSize(params long[] globalWorkSize);

        /// <summary>
        /// Sets the local work size for the kernel.
        /// </summary>
        public IKernelStageBuilder WithLocalWorkSize(params long[] localWorkSize);

        /// <summary>
        /// Maps an input from the pipeline context.
        /// </summary>
        public IKernelStageBuilder MapInput(string parameterName, string contextKey);

        /// <summary>
        /// Maps an output to the pipeline context.
        /// </summary>
        public IKernelStageBuilder MapOutput(string parameterName, string contextKey);

        /// <summary>
        /// Sets a constant parameter value.
        /// </summary>
        public IKernelStageBuilder SetParameter<T>(string parameterName, T value);

        /// <summary>
        /// Adds a dependency on another stage.
        /// </summary>
        public IKernelStageBuilder DependsOn(string stageId);

        /// <summary>
        /// Adds metadata to the stage.
        /// </summary>
        public IKernelStageBuilder WithMetadata(string key, object value);

        /// <summary>
        /// Sets memory allocation hints.
        /// </summary>
        public IKernelStageBuilder WithMemoryHint(MemoryHint hint);

        /// <summary>
        /// Sets execution priority.
        /// </summary>
        public IKernelStageBuilder WithPriority(int priority);
    }

    /// <summary>
    /// Builder interface for parallel stages.
    /// </summary>
    public interface IParallelStageBuilder
    {
        /// <summary>
        /// Adds a kernel to execute in parallel.
        /// </summary>
        public IParallelStageBuilder AddKernel(
            string name,
            ICompiledKernel kernel,
            Action<IKernelStageBuilder>? configure = null);

        /// <summary>
        /// Adds a sub-pipeline to execute in parallel.
        /// </summary>
        public IParallelStageBuilder AddPipeline(
            string name,
            Action<IKernelPipelineBuilder> configure);

        /// <summary>
        /// Sets the maximum degree of parallelism.
        /// </summary>
        public IParallelStageBuilder WithMaxDegreeOfParallelism(int maxDegree);

        /// <summary>
        /// Sets the synchronization mode.
        /// </summary>
        public IParallelStageBuilder WithSynchronization(SynchronizationMode mode);

        /// <summary>
        /// Adds a barrier after all parallel operations.
        /// </summary>
        public IParallelStageBuilder WithBarrier();
    }

    /// <summary>
    /// Memory usage statistics.
    /// </summary>
    public sealed class MemoryUsageStats
    {
        /// <summary>
        /// Gets the allocated memory in bytes.
        /// </summary>
        public required long AllocatedBytes { get; init; }

        /// <summary>
        /// Gets the peak memory usage in bytes.
        /// </summary>
        public required long PeakBytes { get; init; }

        /// <summary>
        /// Gets the number of allocations.
        /// </summary>
        public required int AllocationCount { get; init; }

        /// <summary>
        /// Gets the number of deallocations.
        /// </summary>
        public required int DeallocationCount { get; init; }

        /// <summary>
        /// Gets memory usage by type.
        /// </summary>
        public IReadOnlyDictionary<string, long>? UsageByType { get; init; }
    }

    /// <summary>
    /// Memory allocation hints for optimization.
    /// </summary>
    public enum MemoryHint
    {
        /// <summary>
        /// No specific hint.
        /// </summary>
        None,

        /// <summary>
        /// Memory will be read sequentially.
        /// </summary>
        Sequential,

        /// <summary>
        /// Memory will be accessed randomly.
        /// </summary>
        Random,

        /// <summary>
        /// Memory is temporary and can be released soon.
        /// </summary>
        Temporary,

        /// <summary>
        /// Memory will be reused across multiple stages.
        /// </summary>
        Persistent,

        /// <summary>
        /// Memory should be pinned for device access.
        /// </summary>
        Pinned
    }

    /// <summary>
    /// Synchronization modes for parallel execution.
    /// </summary>
    public enum SynchronizationMode
    {
        /// <summary>
        /// Wait for all parallel operations to complete.
        /// </summary>
        WaitAll,

        /// <summary>
        /// Continue when any operation completes.
        /// </summary>
        WaitAny,

        /// <summary>
        /// Fire and forget - don't wait.
        /// </summary>
        FireAndForget,

        /// <summary>
        /// Custom synchronization logic.
        /// </summary>
        Custom
    }
}
