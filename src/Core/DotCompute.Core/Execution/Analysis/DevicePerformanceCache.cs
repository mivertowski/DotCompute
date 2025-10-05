// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Analysis
{
    /// <summary>
    /// Caches performance metrics for a specific device to improve performance estimation accuracy.
    /// </summary>
    /// <remarks>
    /// This cache stores historical performance data including success rates, efficiency metrics,
    /// and execution statistics that are used by the <see cref="DevicePerformanceEstimator"/>
    /// to make more accurate performance predictions.
    /// </remarks>
    public sealed class DevicePerformanceCache
    {
        /// <summary>
        /// Gets or sets the success rate for executions on this device.
        /// </summary>
        /// <value>
        /// A double value between 0.0 and 1.0, where 1.0 indicates all executions succeeded
        /// and 0.0 indicates all executions failed. Default is 1.0.
        /// </value>
        public double SuccessRate { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the average efficiency percentage for this device.
        /// </summary>
        /// <value>
        /// A double value representing the average efficiency as a percentage,
        /// where 100.0 indicates perfect efficiency. Default is 80.0%.
        /// </value>
        /// <remarks>
        /// Efficiency is calculated based on how closely actual execution times
        /// match estimated execution times. Higher values indicate more predictable performance.
        /// </remarks>
        public double AverageEfficiency { get; set; } = 80.0;

        /// <summary>
        /// Gets or sets the timestamp when this cache was last updated.
        /// </summary>
        /// <value>
        /// A <see cref="DateTime"/> representing when the performance data was last modified.
        /// Default is the current UTC time when the instance is created.
        /// </value>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the total number of executions recorded for this device.
        /// </summary>
        /// <value>
        /// A long value representing the total number of executions that have contributed
        /// to the performance statistics. Default is 0.
        /// </value>
        public long TotalExecutions { get; set; }


        /// <summary>
        /// Gets or sets the average execution time in milliseconds.
        /// </summary>
        /// <value>
        /// A double value representing the mean execution time across all recorded executions.
        /// Default is 0.0.
        /// </value>
        public double AverageExecutionTime { get; set; }


        /// <summary>
        /// Gets or sets the total execution time in milliseconds across all recorded executions.
        /// </summary>
        /// <value>
        /// A double value representing the cumulative execution time. Default is 0.0.
        /// </value>
        public double TotalExecutionTime { get; set; }


        /// <summary>
        /// Gets a value indicating whether this cache contains recent performance data.
        /// </summary>
        /// <value>
        /// <c>true</c> if the cache was updated within the last 24 hours; otherwise, <c>false</c>.
        /// </value>
        public bool IsRecent => DateTime.UtcNow - LastUpdated < TimeSpan.FromHours(24);

        /// <summary>
        /// Gets a value indicating whether this cache has sufficient data for reliable estimates.
        /// </summary>
        /// <value>
        /// <c>true</c> if the cache has at least 5 recorded executions; otherwise, <c>false</c>.
        /// </value>
        public bool HasSufficientData => TotalExecutions >= 5;

        /// <summary>
        /// Gets the performance confidence score based on data quality and recency.
        /// </summary>
        /// <value>
        /// A double value between 0.0 and 1.0, where higher values indicate more reliable
        /// performance data for estimation purposes.
        /// </value>
        public double ConfidenceScore
        {
            get
            {
                var recencyFactor = IsRecent ? 1.0 : 0.7;
                var dataFactor = Math.Min(1.0, TotalExecutions / 20.0); // Full confidence at 20+ executions
                var successFactor = SuccessRate;


                return recencyFactor * dataFactor * successFactor;
            }
        }

        /// <summary>
        /// Updates the cache with new execution data.
        /// </summary>
        /// <param name="executionTime">The execution time in milliseconds for the new execution.</param>
        /// <param name="succeeded">Whether the execution completed successfully.</param>
        /// <remarks>
        /// This method recalculates the running averages and updates all relevant statistics.
        /// </remarks>
        public void UpdateWithExecution(double executionTime, bool succeeded)
        {
            var newTotal = TotalExecutions + 1;

            // Update success rate

            SuccessRate = (SuccessRate * TotalExecutions + (succeeded ? 1.0 : 0.0)) / newTotal;

            // Update execution time statistics (only for successful executions)

            if (succeeded && executionTime >= 0)
            {
                TotalExecutionTime += executionTime;
                AverageTimeMs = TotalExecutionTime / newTotal;
            }


            TotalExecutions = newTotal;
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Resets all cached performance data to initial values.
        /// </summary>
        public void Reset()
        {
            SuccessRate = 1.0;
            AverageEfficiency = 80.0;
            TotalExecutions = 0;
            AverageTimeMs = 0.0;
            TotalExecutionTime = 0.0;
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the performance cache data.
        /// </summary>
        /// <returns>
        /// A string containing the key performance metrics and statistics.
        /// </returns>
        public override string ToString()
        {
            return $"PerformanceCache: Success={SuccessRate:P2}, Efficiency={AverageEfficiency:F1}%, " +
                   $"Executions={TotalExecutions}, AvgTime={AverageExecutionTime:F2}ms, " +
                   $"LastUpdated={LastUpdated:yyyy-MM-dd HH:mm:ss}, Confidence={ConfidenceScore:F2}";
        }
    }
}