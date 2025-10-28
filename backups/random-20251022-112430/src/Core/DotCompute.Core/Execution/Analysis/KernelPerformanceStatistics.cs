// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Analysis
{
    /// <summary>
    /// Contains detailed performance statistics for a compiled kernel execution.
    /// </summary>
    /// <remarks>
    /// This class provides comprehensive performance metrics for kernel execution including
    /// timing data, execution frequency, and throughput calculations. It is typically used
    /// for performance monitoring and optimization analysis.
    /// </remarks>
    public sealed class KernelPerformanceStatistics
    {
        /// <summary>
        /// Gets or sets the name of the kernel.
        /// </summary>
        /// <value>A string identifying the kernel for which these statistics apply.</value>
        /// <remarks>This is required and must be set during initialization.</remarks>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets or sets the identifier of the device where the kernel was executed.
        /// </summary>
        /// <value>A string uniquely identifying the target device.</value>
        /// <remarks>This is required and must be set during initialization.</remarks>
        public required string DeviceId { get; init; }

        /// <summary>
        /// Gets or sets the total number of times this kernel has been executed.
        /// </summary>
        /// <value>A long value representing the cumulative execution count.</value>
        public long ExecutionCount { get; init; }

        /// <summary>
        /// Gets or sets the total execution time across all invocations in milliseconds.
        /// </summary>
        /// <value>A double representing the sum of all execution times in milliseconds.</value>
        public double TotalExecutionTimeMs { get; init; }

        /// <summary>
        /// Gets or sets the average execution time per invocation in milliseconds.
        /// </summary>
        /// <value>A double representing the mean execution time in milliseconds.</value>
        public double AverageExecutionTimeMs { get; init; }

        /// <summary>
        /// Gets or sets the timestamp when this kernel was compiled.
        /// </summary>
        /// <value>A <see cref="DateTimeOffset"/> representing the compilation time.</value>
        public DateTimeOffset CompilationTime { get; init; }

        /// <summary>
        /// Gets the execution frequency in executions per minute.
        /// </summary>
        /// <value>
        /// A double representing how many times per minute this kernel has been executed
        /// on average since compilation. Returns 0 if no time has elapsed.
        /// </value>
        public double ExecutionFrequency
        {
            get
            {
                var elapsed = DateTimeOffset.UtcNow - CompilationTime;
                return elapsed.TotalMinutes > 0 ? ExecutionCount / elapsed.TotalMinutes : 0;
            }
        }

        /// <summary>
        /// Gets the throughput in executions per second of actual execution time.
        /// </summary>
        /// <value>
        /// A double representing how many executions could be completed per second
        /// based on the average execution time. Returns 0 if no execution time has been recorded.
        /// </value>
        /// <remarks>
        /// This metric reflects the raw computational throughput, excluding any overhead
        /// from scheduling, memory transfers, or synchronization.
        /// </remarks>
        public double Throughput
        {
            get
            {
                return TotalExecutionTimeMs > 0 ? (ExecutionCount * 1000.0) / TotalExecutionTimeMs : 0;
            }
        }

        /// <summary>
        /// Gets the efficiency ratio of this kernel execution.
        /// </summary>
        /// <value>
        /// A double between 0.0 and 1.0 representing execution efficiency,
        /// calculated as the ratio of actual throughput to theoretical maximum.
        /// </value>
        public double EfficiencyRatio
        {
            get
            {
                // Simplified efficiency calculation - could be enhanced with device-specific theoretical maximums - TODO
                var idealTimeMs = ExecutionCount > 0 ? TotalExecutionTimeMs / ExecutionCount * 0.8 : 0; // Assume 80% of current average is ideal
                return idealTimeMs > 0 ? Math.Min(1.0, AverageExecutionTimeMs / idealTimeMs) : 0;
            }
        }

        /// <summary>
        /// Gets the age of this kernel since compilation.
        /// </summary>
        /// <value>A <see cref="TimeSpan"/> representing how long ago the kernel was compiled.</value>
        public TimeSpan Age => DateTimeOffset.UtcNow - CompilationTime;

        /// <summary>
        /// Gets a value indicating whether this kernel is actively used.
        /// </summary>
        /// <value>
        /// <c>true</c> if the kernel has been executed at least once in the last hour; otherwise, <c>false</c>.
        /// </value>
        public bool IsActive
        {
            get
            {
                var hourlyFrequency = ExecutionFrequency / 60.0; // Convert to executions per hour
                return hourlyFrequency >= 1.0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this kernel shows consistent performance.
        /// </summary>
        /// <value>
        /// <c>true</c> if the kernel has been executed multiple times and shows predictable timing; otherwise, <c>false</c>.
        /// </value>
        public bool IsStable => ExecutionCount >= 10 && EfficiencyRatio > 0.7;

        /// <summary>
        /// Creates a new instance with updated execution data.
        /// </summary>
        /// <param name="additionalExecutions">The number of additional executions to add.</param>
        /// <param name="additionalTimeMs">The additional execution time in milliseconds.</param>
        /// <returns>A new <see cref="KernelPerformanceStatistics"/> instance with updated data.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="additionalExecutions"/> is negative or <paramref name="additionalTimeMs"/> is negative.
        /// </exception>
        public KernelPerformanceStatistics WithAdditionalExecution(long additionalExecutions, double additionalTimeMs)
        {
            if (additionalExecutions < 0)
            {

                throw new ArgumentOutOfRangeException(nameof(additionalExecutions), "Additional executions cannot be negative.");
            }


            if (additionalTimeMs < 0)
            {

                throw new ArgumentOutOfRangeException(nameof(additionalTimeMs), "Additional time cannot be negative.");
            }


            var newExecutionCount = ExecutionCount + additionalExecutions;
            var newTotalTimeMs = TotalExecutionTimeMs + additionalTimeMs;
            var newAverageTimeMs = newExecutionCount > 0 ? newTotalTimeMs / newExecutionCount : 0;

            return new KernelPerformanceStatistics
            {
                KernelName = KernelName,
                DeviceId = DeviceId,
                ExecutionCount = newExecutionCount,
                TotalExecutionTimeMs = newTotalTimeMs,
                AverageExecutionTimeMs = newAverageTimeMs,
                CompilationTime = CompilationTime
            };
        }

        /// <summary>
        /// Returns a detailed string representation of the kernel performance statistics.
        /// </summary>
        /// <returns>
        /// A formatted string containing all key performance metrics.
        /// </returns>
        public override string ToString()
        {
            return $"Kernel '{KernelName}' on {DeviceId}: " +
                   $"{ExecutionCount} executions, {AverageExecutionTimeMs:F2}ms avg, " +
                   $"{Throughput:F1} exec/sec, {ExecutionFrequency:F1} exec/min, " +
                   $"Efficiency: {EfficiencyRatio:P1}, Age: {Age.TotalHours:F1}h";
        }

        /// <summary>
        /// Returns a compact string representation suitable for logging.
        /// </summary>
        /// <returns>A concise string with essential performance metrics.</returns>
        public string ToLogString() => $"{KernelName}@{DeviceId}: {ExecutionCount}x, {AverageExecutionTimeMs:F1}ms, {EfficiencyRatio:P0}";
    }
}
