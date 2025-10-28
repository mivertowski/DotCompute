// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Results
{
    /// <summary>
    /// Comprehensive metrics for individual kernel steps in a chain execution.
    /// Provides detailed performance, resource usage, and execution information.
    /// </summary>
    public sealed class KernelStepMetrics
    {
        /// <summary>
        /// Gets the kernel name.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets the step index in the chain (zero-based).
        /// </summary>
        public required int StepIndex { get; init; }

        /// <summary>
        /// Gets the step identifier if specified.
        /// </summary>
        public string? StepId { get; init; }

        /// <summary>
        /// Gets the execution time for this step.
        /// </summary>
        public required TimeSpan ExecutionTime { get; init; }

        /// <summary>
        /// Gets whether this step executed successfully.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Gets the backend used for this step (e.g., "CUDA", "CPU", "Metal").
        /// </summary>
        public required string Backend { get; init; }

        /// <summary>
        /// Gets whether this step's result was served from cache.
        /// </summary>
        public required bool WasCached { get; init; }

        /// <summary>
        /// Gets the cache key used for this step (if caching was enabled).
        /// </summary>
        public string? CacheKey { get; init; }

        /// <summary>
        /// Gets memory used by this step in bytes.
        /// </summary>
        public required long MemoryUsed { get; init; }

        /// <summary>
        /// Gets peak memory usage during this step in bytes.
        /// </summary>
        public long? PeakMemoryUsage { get; init; }

        /// <summary>
        /// Gets the number of compute units used (e.g., GPU cores, CPU threads).
        /// </summary>
        public int? ComputeUnitsUsed { get; init; }

        /// <summary>
        /// Gets the timestamp when this step started execution.
        /// </summary>
        public DateTime? StartTime { get; init; }

        /// <summary>
        /// Gets the timestamp when this step completed execution.
        /// </summary>
        public DateTime? EndTime { get; init; }

        /// <summary>
        /// Gets the error that occurred during step execution (if any).
        /// </summary>
        public Exception? Error { get; init; }

        /// <summary>
        /// Gets the number of input parameters for this step.
        /// </summary>
        public int? InputParameterCount { get; init; }

        /// <summary>
        /// Gets the size of input data in bytes.
        /// </summary>
        public long? InputDataSize { get; init; }

        /// <summary>
        /// Gets the size of output data in bytes.
        /// </summary>
        public long? OutputDataSize { get; init; }

        /// <summary>
        /// Gets throughput metrics if applicable (operations per second).
        /// </summary>
        public double? Throughput { get; init; }

        /// <summary>
        /// Gets additional metadata for this step.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }

        /// <summary>
        /// Gets performance optimization hints discovered during execution.
        /// </summary>
        public IReadOnlyList<string>? OptimizationHints { get; init; }

        /// <summary>
        /// Gets the data transfer time (for GPU/accelerator kernels).
        /// </summary>
        public TimeSpan? DataTransferTime { get; init; }

        /// <summary>
        /// Gets the kernel compilation time (if compilation occurred).
        /// </summary>
        public TimeSpan? CompilationTime { get; init; }

        /// <summary>
        /// Creates successful step metrics.
        /// </summary>
        /// <param name="kernelName">Name of the kernel</param>
        /// <param name="stepIndex">Step index in the chain</param>
        /// <param name="executionTime">Execution time</param>
        /// <param name="backend">Backend used</param>
        /// <param name="memoryUsed">Memory used in bytes</param>
        /// <param name="wasCached">Whether result was cached</param>
        /// <param name="stepId">Optional step identifier</param>
        /// <param name="cacheKey">Optional cache key</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>Successful step metrics</returns>
        public static KernelStepMetrics CreateSuccess(
            string kernelName,
            int stepIndex,
            TimeSpan executionTime,
            string backend,
            long memoryUsed,
            bool wasCached = false,
            string? stepId = null,
            string? cacheKey = null,
            IReadOnlyDictionary<string, object>? metadata = null)
            => new()
            {
                KernelName = kernelName,
                StepIndex = stepIndex,
                StepId = stepId,
                ExecutionTime = executionTime,
                Success = true,
                Backend = backend,
                WasCached = wasCached,
                CacheKey = cacheKey,
                MemoryUsed = memoryUsed,
                EndTime = DateTime.UtcNow,
                StartTime = DateTime.UtcNow - executionTime,
                Metadata = metadata
            };

        /// <summary>
        /// Creates failed step metrics.
        /// </summary>
        /// <param name="kernelName">Name of the kernel</param>
        /// <param name="stepIndex">Step index in the chain</param>
        /// <param name="executionTime">Time spent before failure</param>
        /// <param name="backend">Backend used</param>
        /// <param name="error">Error that caused the failure</param>
        /// <param name="memoryUsed">Memory used before failure</param>
        /// <param name="stepId">Optional step identifier</param>
        /// <param name="metadata">Optional metadata</param>
        /// <returns>Failed step metrics</returns>
        public static KernelStepMetrics CreateFailure(
            string kernelName,
            int stepIndex,
            TimeSpan executionTime,
            string backend,
            Exception error,
            long memoryUsed = 0,
            string? stepId = null,
            IReadOnlyDictionary<string, object>? metadata = null)
            => new()
            {
                KernelName = kernelName,
                StepIndex = stepIndex,
                StepId = stepId,
                ExecutionTime = executionTime,
                Success = false,
                Backend = backend,
                WasCached = false,
                MemoryUsed = memoryUsed,
                Error = error,
                EndTime = DateTime.UtcNow,
                StartTime = DateTime.UtcNow - executionTime,
                Metadata = metadata
            };

        /// <summary>
        /// Gets the efficiency ratio (useful operations per memory byte).
        /// </summary>
        public double? GetEfficiencyRatio()
        {
            if (MemoryUsed <= 0 || Throughput is null)
            {
                return null;
            }


            return Throughput.Value / MemoryUsed;
        }

        /// <summary>
        /// Gets the performance score (arbitrary scale based on execution time and memory usage).
        /// </summary>
        public double GetPerformanceScore()
        {
            if (!Success)
            {
                return 0.0;
            }

            // Base score from execution time (faster = higher score)

            var timeScore = Math.Max(0, 10.0 - ExecutionTime.TotalMilliseconds / 100.0);

            // Memory efficiency bonus (less memory = higher score)
            var memoryScore = MemoryUsed > 0 ? Math.Max(0, 5.0 - Math.Log10(MemoryUsed)) : 5.0;

            // Cache hit bonus
            var cacheBonus = WasCached ? 2.0 : 0.0;

            return Math.Max(0, timeScore + memoryScore + cacheBonus);
        }
    }
}
