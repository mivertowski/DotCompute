// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Interfaces.Pipelines
{
    /// <summary>
    /// Fluent interface for building and executing kernel chains with method chaining.
    /// This provides an intuitive API that leverages the existing sophisticated pipeline infrastructure.
    /// </summary>
    public interface IKernelChainBuilder : IAsyncDisposable
    {
        /// <summary>
        /// Adds a kernel to the execution chain.
        /// </summary>
        /// <param name="kernelName">The name of the kernel to execute</param>
        /// <param name="args">The arguments to pass to the kernel</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder Kernel(string kernelName, params object[] args);

        /// <summary>
        /// Adds another kernel to the execution chain (alias for Kernel for readability).
        /// </summary>
        /// <param name="kernelName">The name of the kernel to execute</param>
        /// <param name="args">The arguments to pass to the kernel</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder Then(string kernelName, params object[] args);

        /// <summary>
        /// Adds multiple kernels to execute in parallel.
        /// </summary>
        /// <param name="kernels">Array of (kernel name, arguments) tuples to execute in parallel</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder Parallel(params (string kernelName, object[] args)[] kernels);

        /// <summary>
        /// Adds conditional branching to the chain based on the result of the previous operation.
        /// </summary>
        /// <typeparam name="T">The type of the condition input</typeparam>
        /// <param name="condition">Function to evaluate for branching decision</param>
        /// <param name="truePath">Action to configure the true branch</param>
        /// <param name="falsePath">Action to configure the false branch (optional)</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder Branch<T>(Func<T, bool> condition,
            Func<IKernelChainBuilder, IKernelChainBuilder> truePath,
            Func<IKernelChainBuilder, IKernelChainBuilder>? falsePath = null);

        /// <summary>
        /// Adds caching for the current chain step with optional TTL.
        /// </summary>
        /// <param name="key">Cache key for storing/retrieving results</param>
        /// <param name="ttl">Time-to-live for cached results (optional)</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder Cache(string key, TimeSpan? ttl = null);

        /// <summary>
        /// Forces execution on a specific backend.
        /// </summary>
        /// <param name="backendName">Name of the backend to use (e.g., "CUDA", "CPU")</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder OnBackend(string backendName);

        /// <summary>
        /// Forces execution on a specific accelerator instance.
        /// </summary>
        /// <param name="accelerator">The specific accelerator to use</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder OnAccelerator(IAccelerator accelerator);

        /// <summary>
        /// Adds profiling and performance monitoring to the chain.
        /// </summary>
        /// <param name="profileName">Optional profile name for identification</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder WithProfiling(string? profileName = null);

        /// <summary>
        /// Sets execution timeout for the entire chain.
        /// </summary>
        /// <param name="timeout">Maximum execution time</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder WithTimeout(TimeSpan timeout);

        /// <summary>
        /// Adds error handling to the chain.
        /// </summary>
        /// <param name="errorHandler">Function to handle errors during execution</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder OnError(Func<Exception, ErrorHandlingStrategy> errorHandler);

        /// <summary>
        /// Adds validation to ensure chain integrity before execution.
        /// </summary>
        /// <param name="validateInputs">Whether to validate input parameters</param>
        /// <returns>The chain builder for method chaining</returns>
        public IKernelChainBuilder WithValidation(bool validateInputs = true);

        /// <summary>
        /// Executes the kernel chain and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected return type</typeparam>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task containing the execution result</returns>
        public Task<T> ExecuteAsync<T>(CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the kernel chain and returns detailed execution information.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task containing detailed execution results with metrics</returns>
        public Task<KernelChainExecutionResult> ExecuteWithMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the current chain configuration without executing.
        /// </summary>
        /// <returns>Validation result indicating any issues</returns>
        public Task<KernelChainValidationResult> ValidateAsync();
    }


    /// <summary>
    /// Result of kernel chain execution with metrics and detailed information.
    /// </summary>
    public sealed class KernelChainExecutionResult
    {
        /// <summary>
        /// Gets whether the execution was successful.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the final result value.
        /// </summary>
        public required object? Result { get; init; }

        /// <summary>
        /// Gets the execution time for the entire chain.
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
        /// Gets memory usage statistics.
        /// </summary>
        public KernelChainMemoryMetrics? MemoryMetrics { get; init; }
    }

    /// <summary>
    /// Metrics for individual kernel steps in a chain.
    /// </summary>
    public sealed class KernelStepMetrics
    {
        /// <summary>
        /// Gets the kernel name.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets the step name or identifier.
        /// </summary>
        public required string StepName { get; init; }

        /// <summary>
        /// Gets the step index in the chain.
        /// </summary>
        public required int StepIndex { get; init; }

        /// <summary>
        /// Gets the execution time for this step.
        /// </summary>
        public required TimeSpan ExecutionTime { get; init; }

        /// <summary>
        /// Gets whether this step executed successfully.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the error message if the step failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Gets the backend used for this step.
        /// </summary>
        public required string Backend { get; init; }

        /// <summary>
        /// Gets whether this step was cached.
        /// </summary>
        public required bool WasCached { get; init; }

        /// <summary>
        /// Gets memory used by this step.
        /// </summary>
        public required long MemoryUsed { get; init; }

        /// <summary>
        /// Gets throughput in operations per second.
        /// </summary>
        public double ThroughputOpsPerSecond { get; init; }
    }

    /// <summary>
    /// Memory usage metrics for kernel chain execution.
    /// </summary>
    public sealed class KernelChainMemoryMetrics
    {
        /// <summary>
        /// Gets the peak memory usage during execution.
        /// </summary>
        public required long PeakMemoryUsage { get; init; }

        /// <summary>
        /// Gets the total memory allocated.
        /// </summary>
        public required long TotalMemoryAllocated { get; init; }

        /// <summary>
        /// Gets the number of garbage collections triggered.
        /// </summary>
        public required int GarbageCollections { get; init; }

        /// <summary>
        /// Gets whether memory pooling was used.
        /// </summary>
        public required bool MemoryPoolingUsed { get; init; }
    }

    /// <summary>
    /// Result of kernel chain validation.
    /// </summary>
    public sealed class KernelChainValidationResult
    {
        /// <summary>
        /// Gets whether the chain is valid.
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

        /// <summary>
        /// Gets estimated execution time.
        /// </summary>
        public TimeSpan? EstimatedExecutionTime { get; init; }

        /// <summary>
        /// Gets estimated memory usage.
        /// </summary>
        public long? EstimatedMemoryUsage { get; init; }
    }
}
