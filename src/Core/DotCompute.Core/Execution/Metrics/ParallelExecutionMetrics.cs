// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.


using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Metrics
{
    /// <summary>
    /// Represents performance metrics for parallel execution operations.
    /// </summary>
    /// <remarks>
    /// This class aggregates execution metrics across multiple devices and execution strategies,
    /// providing comprehensive performance analysis for parallel compute operations.
    /// It tracks execution statistics, efficiency metrics, and performance trends
    /// to enable optimization and monitoring of parallel workloads.
    /// </remarks>
    public class ParallelExecutionMetrics
    {
        /// <summary>
        /// Gets or sets the total number of executions.
        /// </summary>
        /// <value>
        /// The total count of parallel execution operations that have been tracked.
        /// This includes both successful and failed executions across all strategies and devices.
        /// </value>
        public int TotalExecutions { get; set; }

        /// <summary>
        /// Gets or sets the average execution time in milliseconds.
        /// </summary>
        /// <value>
        /// The mean execution time across all tracked operations, measured in milliseconds.
        /// This metric provides insight into overall system performance and helps identify trends.
        /// </value>
        public double AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average parallel efficiency percentage.
        /// </summary>
        /// <value>
        /// The mean parallel efficiency as a percentage (0-100), indicating how effectively
        /// the parallel execution utilizes available compute resources compared to ideal scaling.
        /// Higher values indicate better parallelization efficiency.
        /// </value>
        public double AverageEfficiencyPercentage { get; set; }

        /// <summary>
        /// Gets or sets the total GFLOPS-hours processed.
        /// </summary>
        /// <value>
        /// The cumulative computational work performed, measured in gigaFLOPS-hours.
        /// This metric represents the total amount of floating-point computation
        /// performed across all tracked executions and provides insight into system utilization.
        /// </value>
        public double TotalGFLOPSHours { get; set; }

        /// <summary>
        /// Gets or sets metrics by execution strategy.
        /// </summary>
        /// <value>
        /// A dictionary mapping execution strategy types to their corresponding performance metrics.
        /// This allows for strategy-specific performance analysis and comparison.
        /// </value>
        public Dictionary<ExecutionStrategyType, StrategyMetrics> MetricsByStrategy { get; set; } = [];

        /// <summary>
        /// Gets or sets metrics by device.
        /// </summary>
        /// <value>
        /// A dictionary mapping device identifiers to their corresponding performance metrics.
        /// This enables device-specific performance tracking and load balancing analysis.
        /// </value>
        public Dictionary<string, DeviceMetrics> MetricsByDevice { get; set; } = [];

        /// <summary>
        /// Gets the overall success rate percentage.
        /// </summary>
        /// <value>
        /// The percentage of successful executions out of total executions (0-100).
        /// This metric indicates system reliability and stability.
        /// </value>
        public double SuccessRatePercentage
        {
            get
            {
                if (TotalExecutions == 0)
                {
                    return 0.0;
                }


                var totalSuccesses = MetricsByStrategy.Values.Sum(m => m.UsageCount * m.SuccessRatePercentage / 100.0);
                return (totalSuccesses / TotalExecutions) * 100.0;
            }
        }

        /// <summary>
        /// Gets the average device utilization percentage.
        /// </summary>
        /// <value>
        /// The mean utilization across all tracked devices (0-100).
        /// This indicates how effectively the available compute resources are being used.
        /// </value>
        public double AverageDeviceUtilization
        {
            get
            {
                if (MetricsByDevice.Count == 0)
                {
                    return 0.0;
                }


                return MetricsByDevice.Values.Average(d => d.UtilizationPercentage);
            }
        }

        /// <summary>
        /// Gets the most efficient execution strategy.
        /// </summary>
        /// <value>
        /// The execution strategy type that achieved the highest average efficiency,
        /// or <c>null</c> if no strategies have been tracked.
        /// </value>
        public ExecutionStrategyType? MostEfficientStrategy
        {
            get
            {
                if (MetricsByStrategy.Count == 0)
                {
                    return null;
                }


                return MetricsByStrategy.MaxBy(kvp => kvp.Value.AverageEfficiencyPercentage).Key;
            }
        }

        /// <summary>
        /// Gets the fastest execution strategy.
        /// </summary>
        /// <value>
        /// The execution strategy type that achieved the lowest average execution time,
        /// or <c>null</c> if no strategies have been tracked.
        /// </value>
        public ExecutionStrategyType? FastestStrategy
        {
            get
            {
                if (MetricsByStrategy.Count == 0)
                {
                    return null;
                }


                return MetricsByStrategy.MinBy(kvp => kvp.Value.AverageExecutionTimeMs).Key;
            }
        }

        /// <summary>
        /// Adds metrics from a completed execution.
        /// </summary>
        /// <param name="result">The parallel execution result to incorporate into the metrics.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="result"/> is <c>null</c>.</exception>
        public void AddExecution(ParallelExecutionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            TotalExecutions++;
            
            // Update average execution time
            var totalTime = (AverageExecutionTimeMs * (TotalExecutions - 1)) + result.TotalExecutionTimeMs;
            AverageExecutionTimeMs = totalTime / TotalExecutions;
            
            // Update average efficiency
            var totalEfficiency = (AverageEfficiencyPercentage * (TotalExecutions - 1)) + result.EfficiencyPercentage;
            AverageEfficiencyPercentage = totalEfficiency / TotalExecutions;
            
            // Update GFLOPS-hours
            TotalGFLOPSHours += result.ThroughputGFLOPS * (result.TotalExecutionTimeMs / (1000.0 * 3600.0));
            
            // Update strategy metrics
            if (!MetricsByStrategy.TryGetValue(result.Strategy, out var strategyMetrics))
            {
                strategyMetrics = new StrategyMetrics();
                MetricsByStrategy[result.Strategy] = strategyMetrics;
            }
            strategyMetrics.AddExecution(result);
            
            // Update device metrics
            foreach (var deviceResult in result.DeviceResults)
            {
                if (!MetricsByDevice.TryGetValue(deviceResult.DeviceId, out var deviceMetrics))
                {
                    deviceMetrics = new DeviceMetrics { DeviceId = deviceResult.DeviceId };
                    MetricsByDevice[deviceResult.DeviceId] = deviceMetrics;
                }
                deviceMetrics.AddExecution(deviceResult);
            }
        }

        /// <summary>
        /// Resets all metrics to their initial state.
        /// </summary>
        public void Reset()
        {
            TotalExecutions = 0;
            AverageExecutionTimeMs = 0.0;
            AverageEfficiencyPercentage = 0.0;
            TotalGFLOPSHours = 0.0;
            MetricsByStrategy.Clear();
            MetricsByDevice.Clear();
        }

        /// <summary>
        /// Returns a string representation of the parallel execution metrics.
        /// </summary>
        /// <returns>
        /// A string containing a summary of the key performance metrics.
        /// </returns>
        public override string ToString()
        {
            return $"Executions: {TotalExecutions}, Avg Time: {AverageExecutionTimeMs:F2}ms, " +
                   $"Avg Efficiency: {AverageEfficiencyPercentage:F1}%, Success Rate: {SuccessRatePercentage:F1}%";
        }
    }
}