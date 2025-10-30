// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Metrics
{
    /// <summary>
    /// Represents performance metrics for a specific execution strategy.
    /// </summary>
    /// <remarks>
    /// This class tracks performance characteristics and usage statistics
    /// for individual execution strategies (e.g., data parallel, model parallel, pipeline parallel).
    /// It provides insights into the effectiveness and reliability of different
    /// parallelization approaches under various workload conditions.
    /// </remarks>
    public class StrategyMetrics
    {
        /// <summary>
        /// Gets or sets the number of times this strategy was used.
        /// </summary>
        /// <value>
        /// The total count of executions that used this strategy.
        /// This metric indicates the frequency of strategy selection and user preference.
        /// </value>
        public int UsageCount { get; set; }

        /// <summary>
        /// Gets or sets the average execution time for this strategy.
        /// </summary>
        /// <value>
        /// The mean execution time across all uses of this strategy, measured in milliseconds.
        /// This metric helps compare the performance characteristics of different strategies.
        /// </value>
        public double AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average efficiency for this strategy.
        /// </summary>
        /// <value>
        /// The mean parallel efficiency as a percentage (0-100) for this strategy.
        /// Higher values indicate that this strategy achieves better parallelization
        /// and resource utilization across its executions.
        /// </value>
        public double AverageEfficiencyPercentage { get; set; }

        /// <summary>
        /// Gets or sets the success rate for this strategy.
        /// </summary>
        /// <value>
        /// The percentage of successful executions for this strategy (0-100).
        /// This metric indicates the reliability and stability of the strategy
        /// under various execution conditions.
        /// </value>
        public double SuccessRatePercentage { get; set; }

        private int _successfulExecutions;

        /// <summary>
        /// Gets the number of successful executions.
        /// </summary>
        /// <value>
        /// The count of executions that completed successfully using this strategy.
        /// </value>
        public int SuccessfulExecutions => _successfulExecutions;

        /// <summary>
        /// Gets the number of failed executions.
        /// </summary>
        /// <value>
        /// The count of executions that failed when using this strategy.
        /// </value>
        public int FailedExecutions => UsageCount - _successfulExecutions;

        /// <summary>
        /// Gets a value indicating whether this strategy is reliable.
        /// </summary>
        /// <value>
        /// <c>true</c> if the success rate is above 95%; otherwise, <c>false</c>.
        /// </value>
        public bool IsReliable => SuccessRatePercentage > 95.0;

        /// <summary>
        /// Gets a value indicating whether this strategy is highly efficient.
        /// </summary>
        /// <value>
        /// <c>true</c> if the average efficiency is above 80%; otherwise, <c>false</c>.
        /// </value>
        public bool IsHighlyEfficient => AverageEfficiencyPercentage > 80.0;

        /// <summary>
        /// Gets a value indicating whether this strategy is fast.
        /// </summary>
        /// <value>
        /// <c>true</c> if the average execution time is below 100ms; otherwise, <c>false</c>.
        /// </value>
        public bool IsFast => AverageExecutionTimeMs < 100.0;

        /// <summary>
        /// Adds the results of a completed execution to these metrics.
        /// </summary>
        /// <param name="result">The parallel execution result to incorporate.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is <c>null</c>.</exception>
        public void AddExecution(ParallelExecutionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var previousCount = UsageCount;
            UsageCount++;

            // Update average execution time
            var totalTime = (AverageExecutionTimeMs * previousCount) + result.TotalExecutionTimeMs;
            AverageExecutionTimeMs = totalTime / UsageCount;

            // Update average efficiency
            var totalEfficiency = (AverageEfficiencyPercentage * previousCount) + result.EfficiencyPercentage;
            AverageEfficiencyPercentage = totalEfficiency / UsageCount;

            // Update success count and rate
            if (result.Success)
            {
                _successfulExecutions++;
            }
            SuccessRatePercentage = ((double)_successfulExecutions / UsageCount) * 100.0;
        }

        /// <summary>
        /// Resets all metrics to their initial state.
        /// </summary>
        public void Reset()
        {
            UsageCount = 0;
            AverageExecutionTimeMs = 0.0;
            AverageEfficiencyPercentage = 0.0;
            SuccessRatePercentage = 0.0;
            _successfulExecutions = 0;
        }

        /// <summary>
        /// Compares this strategy's performance with another strategy.
        /// </summary>
        /// <param name="other">The strategy metrics to compare against.</param>
        /// <returns>
        /// A positive value if this strategy performs better, negative if worse, or zero if equivalent.
        /// The comparison considers efficiency, reliability, and speed.
        /// </returns>
        public double CompareTo(StrategyMetrics other)
        {
            ArgumentNullException.ThrowIfNull(other);

            // Weighted comparison: efficiency (50%), reliability (30%), speed (20%)
            var efficiencyScore = (AverageEfficiencyPercentage - other.AverageEfficiencyPercentage) * 0.5;
            var reliabilityScore = (SuccessRatePercentage - other.SuccessRatePercentage) * 0.3;
            var speedScore = (other.AverageExecutionTimeMs - AverageExecutionTimeMs) / Math.Max(AverageExecutionTimeMs, other.AverageExecutionTimeMs) * 20.0;

            return efficiencyScore + reliabilityScore + speedScore;
        }

        /// <summary>
        /// Returns a string representation of the strategy metrics.
        /// </summary>
        /// <returns>
        /// A string containing usage count, average execution time, efficiency, and success rate.
        /// </returns>
        public override string ToString()
        {
            return $"Usage: {UsageCount}, Avg Time: {AverageExecutionTimeMs:F2}ms, " +
                   $"Efficiency: {AverageEfficiencyPercentage:F1}%, Success: {SuccessRatePercentage:F1}%";
        }
    }
}
