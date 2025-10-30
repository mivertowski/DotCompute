// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Results
{
    /// <summary>
    /// Result of kernel chain execution with detailed metrics and information.
    /// This class provides comprehensive execution data including timing, memory usage, and error details.
    /// </summary>
    public sealed class KernelChainExecutionResult
    {
        /// <summary>
        /// Gets whether the execution was successful.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the final result value from the chain execution.
        /// </summary>
        public required object? Result { get; init; }

        /// <summary>
        /// Gets the total execution time for the entire kernel chain.
        /// </summary>
        public required TimeSpan ExecutionTime { get; init; }

        /// <summary>
        /// Gets detailed metrics for each step in the chain.
        /// </summary>
        public required IReadOnlyList<KernelStepMetrics> StepMetrics { get; init; }

        /// <summary>
        /// Gets any errors that occurred during execution.
        /// </summary>
        public IReadOnlyList<Exception>? Errors { get; init; }

        /// <summary>
        /// Gets the backend used for execution.
        /// </summary>
        public required string Backend { get; init; }

        /// <summary>
        /// Gets memory usage statistics for the chain execution.
        /// </summary>
        public KernelChainMemoryMetrics? MemoryMetrics { get; init; }

        /// <summary>
        /// Gets the timestamp when execution started.
        /// </summary>
        public DateTime? StartTime { get; init; }

        /// <summary>
        /// Gets the timestamp when execution completed.
        /// </summary>
        public DateTime? EndTime { get; init; }

        /// <summary>
        /// Gets the chain identifier that was executed.
        /// </summary>
        public string? ChainId { get; init; }

        /// <summary>
        /// Gets additional metadata associated with the execution.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }

        /// <summary>
        /// Gets the number of steps that were successfully executed.
        /// </summary>
        public int SuccessfulSteps => StepMetrics.Count(m => m.Success);

        /// <summary>
        /// Gets the number of steps that failed during execution.
        /// </summary>
        public int FailedSteps => StepMetrics.Count(m => !m.Success);

        /// <summary>
        /// Gets the total number of steps in the chain.
        /// </summary>
        public int TotalSteps => StepMetrics.Count;

        /// <summary>
        /// Creates a successful execution result.
        /// </summary>
        /// <param name="result">The execution result value</param>
        /// <param name="executionTime">Total execution time</param>
        /// <param name="stepMetrics">Metrics for each step</param>
        /// <param name="backend">Backend used for execution</param>
        /// <param name="memoryMetrics">Optional memory metrics</param>
        /// <param name="chainId">Optional chain identifier</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>Successful execution result</returns>
        public static KernelChainExecutionResult CreateSuccess(
            object? result,
            TimeSpan executionTime,
            IReadOnlyList<KernelStepMetrics> stepMetrics,
            string backend,
            KernelChainMemoryMetrics? memoryMetrics = null,
            string? chainId = null,
            IReadOnlyDictionary<string, object>? metadata = null)
            => new()
            {
                Success = true,
                Result = result,
                ExecutionTime = executionTime,
                StepMetrics = stepMetrics,
                Backend = backend,
                MemoryMetrics = memoryMetrics,
                ChainId = chainId,
                Metadata = metadata,
                EndTime = DateTime.UtcNow,
                StartTime = DateTime.UtcNow - executionTime
            };

        /// <summary>
        /// Creates a failed execution result.
        /// </summary>
        /// <param name="errors">Errors that occurred during execution</param>
        /// <param name="executionTime">Time spent before failure</param>
        /// <param name="stepMetrics">Metrics for steps that were executed</param>
        /// <param name="backend">Backend used for execution</param>
        /// <param name="partialResult">Partial result if available</param>
        /// <param name="chainId">Optional chain identifier</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>Failed execution result</returns>
        public static KernelChainExecutionResult CreateFailure(
            IReadOnlyList<Exception> errors,
            TimeSpan executionTime,
            IReadOnlyList<KernelStepMetrics> stepMetrics,
            string backend,
            object? partialResult = null,
            string? chainId = null,
            IReadOnlyDictionary<string, object>? metadata = null)
            => new()
            {
                Success = false,
                Result = partialResult,
                ExecutionTime = executionTime,
                StepMetrics = stepMetrics,
                Errors = errors,
                Backend = backend,
                ChainId = chainId,
                Metadata = metadata,
                EndTime = DateTime.UtcNow,
                StartTime = DateTime.UtcNow - executionTime
            };

        /// <summary>
        /// Creates a failed execution result from a single exception.
        /// </summary>
        /// <param name="exception">Exception that caused the failure</param>
        /// <param name="executionTime">Time spent before failure</param>
        /// <param name="stepMetrics">Metrics for steps that were executed</param>
        /// <param name="backend">Backend used for execution</param>
        /// <param name="partialResult">Partial result if available</param>
        /// <param name="chainId">Optional chain identifier</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>Failed execution result</returns>
        public static KernelChainExecutionResult CreateFailure(
            Exception exception,
            TimeSpan executionTime,
            IReadOnlyList<KernelStepMetrics> stepMetrics,
            string backend,
            object? partialResult = null,
            string? chainId = null,
            IReadOnlyDictionary<string, object>? metadata = null)
            => CreateFailure(
                [exception],
                executionTime,
                stepMetrics,
                backend,
                partialResult,
                chainId,
                metadata);
    }
}
