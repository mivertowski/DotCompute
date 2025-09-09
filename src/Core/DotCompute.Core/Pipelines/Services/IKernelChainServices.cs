// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Pipelines.Models;

namespace DotCompute.Core.Pipelines.Services
{
    /// <summary>
    /// Interface for resolving kernel names to compiled kernel instances.
    /// This service bridges the gap between string-based kernel names and actual ICompiledKernel instances.
    /// </summary>
    public interface IKernelResolver
    {
        /// <summary>
        /// Resolves a kernel name to a compiled kernel instance.
        /// </summary>
        /// <param name="kernelName">The name of the kernel to resolve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The compiled kernel instance or null if not found</returns>
        Task<ICompiledKernel?> ResolveKernelAsync(string kernelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available kernel names.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of available kernel names</returns>
        Task<IReadOnlyCollection<string>> GetAvailableKernelNamesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a kernel with the specified name exists.
        /// </summary>
        /// <param name="kernelName">The kernel name to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the kernel exists, false otherwise</returns>
        Task<bool> KernelExistsAsync(string kernelName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for profiling kernel chain execution performance.
    /// </summary>
    public interface IKernelChainProfiler
    {
        /// <summary>
        /// Starts profiling with the specified profile name.
        /// </summary>
        /// <param name="profileName">Name for the profiling session</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        Task StartProfilingAsync(string profileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the current profiling session.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the stop operation</returns>
        Task StopProfilingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the profiling results for the specified profile.
        /// </summary>
        /// <param name="profileName">Name of the profiling session</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Profiling results or null if not found</returns>
        Task<KernelChainProfilingResult?> GetProfilingResultAsync(string profileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a kernel execution event for profiling.
        /// </summary>
        /// <param name="kernelName">Name of the executed kernel</param>
        /// <param name="executionTime">Time taken to execute</param>
        /// <param name="memoryUsed">Memory used during execution</param>
        /// <param name="backend">Backend used for execution</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the record operation</returns>
        Task RecordKernelExecutionAsync(string kernelName, TimeSpan executionTime, long memoryUsed, string backend, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for validating kernel chains before execution.
    /// </summary>
    public interface IKernelChainValidator
    {
        /// <summary>
        /// Validates a kernel chain configuration.
        /// </summary>
        /// <param name="steps">The steps to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result with any errors or warnings</returns>
        Task<KernelChainValidationResult> ValidateChainAsync(IEnumerable<KernelChainStep> steps, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates kernel arguments for a specific kernel.
        /// </summary>
        /// <param name="kernelName">Name of the kernel</param>
        /// <param name="arguments">Arguments to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if arguments are valid, false otherwise</returns>
        Task<bool> ValidateKernelArgumentsAsync(string kernelName, object[] arguments, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets validation recommendations for optimizing a kernel chain.
        /// </summary>
        /// <param name="steps">The steps to analyze</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of optimization recommendations</returns>
        Task<IReadOnlyCollection<KernelChainOptimizationRecommendation>> GetOptimizationRecommendationsAsync(IEnumerable<KernelChainStep> steps, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for caching kernel chain execution results.
    /// </summary>
    public interface IKernelChainCacheService
    {
        /// <summary>
        /// Gets a cached value by key.
        /// </summary>
        /// <typeparam name="T">Type of the cached value</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached value or null if not found</returns>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Sets a value in the cache with optional TTL.
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="ttl">Time-to-live for the cached value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the set operation</returns>
        Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        /// <param name="key">Cache key to remove</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the remove operation</returns>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cached values.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the clear operation</returns>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cache statistics</returns>
        Task<KernelChainCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Profiling result for kernel chain execution.
    /// </summary>
    public sealed class KernelChainProfilingResult
    {
        /// <summary>
        /// Gets or sets the profile name.
        /// </summary>
        public required string ProfileName { get; init; }

        /// <summary>
        /// Gets or sets the start time of profiling.
        /// </summary>
        public required DateTime StartTime { get; init; }

        /// <summary>
        /// Gets or sets the end time of profiling.
        /// </summary>
        public required DateTime EndTime { get; init; }

        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        public required TimeSpan TotalExecutionTime { get; init; }

        /// <summary>
        /// Gets or sets the individual kernel execution records.
        /// </summary>
        public required IReadOnlyList<KernelExecutionRecord> KernelExecutions { get; init; }

        /// <summary>
        /// Gets or sets the peak memory usage during profiling.
        /// </summary>
        public required long PeakMemoryUsage { get; init; }

        /// <summary>
        /// Gets or sets the total number of kernel executions.
        /// </summary>
        public required int TotalKernelExecutions { get; init; }
    }

    /// <summary>
    /// Record of individual kernel execution during profiling.
    /// </summary>
    public sealed class KernelExecutionRecord
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets or sets the execution timestamp.
        /// </summary>
        public required DateTime Timestamp { get; init; }

        /// <summary>
        /// Gets or sets the execution time.
        /// </summary>
        public required TimeSpan ExecutionTime { get; init; }

        /// <summary>
        /// Gets or sets the memory used.
        /// </summary>
        public required long MemoryUsed { get; init; }

        /// <summary>
        /// Gets or sets the backend used for execution.
        /// </summary>
        public required string Backend { get; init; }
    }

    /// <summary>
    /// Optimization recommendation for kernel chains.
    /// </summary>
    public sealed class KernelChainOptimizationRecommendation
    {
        /// <summary>
        /// Gets or sets the recommendation type.
        /// </summary>
        public required KernelChainOptimizationType Type { get; init; }

        /// <summary>
        /// Gets or sets the recommendation description.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Gets or sets the estimated performance impact.
        /// </summary>
        public required double EstimatedImpact { get; init; }

        /// <summary>
        /// Gets or sets the affected step IDs.
        /// </summary>
        public required IReadOnlyList<string> AffectedStepIds { get; init; }

        /// <summary>
        /// Gets or sets the priority level of this recommendation.
        /// </summary>
        public required KernelChainOptimizationPriority Priority { get; init; }
    }

    /// <summary>
    /// Types of kernel chain optimizations.
    /// </summary>
    public enum KernelChainOptimizationType
    {
        /// <summary>
        /// Parallel execution optimization.
        /// </summary>
        ParallelExecution,

        /// <summary>
        /// Memory usage optimization.
        /// </summary>
        MemoryOptimization,

        /// <summary>
        /// Backend selection optimization.
        /// </summary>
        BackendOptimization,

        /// <summary>
        /// Caching optimization.
        /// </summary>
        CachingOptimization,

        /// <summary>
        /// Step reordering optimization.
        /// </summary>
        StepReordering,

        /// <summary>
        /// Kernel fusion optimization.
        /// </summary>
        KernelFusion
    }

    /// <summary>
    /// Priority levels for optimization recommendations.
    /// </summary>
    public enum KernelChainOptimizationPriority
    {
        /// <summary>
        /// Low priority - minor performance improvement.
        /// </summary>
        Low,

        /// <summary>
        /// Medium priority - moderate performance improvement.
        /// </summary>
        Medium,

        /// <summary>
        /// High priority - significant performance improvement.
        /// </summary>
        High,

        /// <summary>
        /// Critical priority - major performance bottleneck.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Statistics for kernel chain caching.
    /// </summary>
    public sealed class KernelChainCacheStatistics
    {
        /// <summary>
        /// Gets or sets the total number of cache hits.
        /// </summary>
        public required long TotalHits { get; init; }

        /// <summary>
        /// Gets or sets the total number of cache misses.
        /// </summary>
        public required long TotalMisses { get; init; }

        /// <summary>
        /// Gets or sets the cache hit ratio.
        /// </summary>
        public double HitRatio => TotalHits + TotalMisses > 0 ? (double)TotalHits / (TotalHits + TotalMisses) : 0.0;

        /// <summary>
        /// Gets or sets the total memory used by the cache.
        /// </summary>
        public required long TotalMemoryUsed { get; init; }

        /// <summary>
        /// Gets or sets the number of cached entries.
        /// </summary>
        public required int EntryCount { get; init; }

        /// <summary>
        /// Gets or sets the number of expired entries removed.
        /// </summary>
        public required long ExpiredEntriesRemoved { get; init; }
    }
}
