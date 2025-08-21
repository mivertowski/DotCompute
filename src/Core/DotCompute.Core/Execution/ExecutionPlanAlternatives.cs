// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution
{
    /// <summary>
    /// Represents multiple execution plan alternatives for comparison and selection.
    /// Provides a recommended plan along with all available alternatives and selection criteria.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
    public sealed class ExecutionPlanAlternatives<T> where T : unmanaged
    {
        /// <summary>Gets or initializes the recommended execution plan based on selection criteria.</summary>
        /// <value>The best execution plan according to the selection algorithm, or null if no suitable plan exists</value>
        public ExecutionPlan<T>? RecommendedPlan { get; init; }

        /// <summary>Gets or initializes all available execution plan alternatives.</summary>
        /// <value>Array of all generated execution plans, ordered by performance</value>
        public ExecutionPlan<T>[] AllAlternatives { get; init; } = [];

        /// <summary>Gets or initializes the criteria used for plan selection and ranking.</summary>
        /// <value>Description of how plans were ranked and the recommended plan was selected</value>
        public string SelectionCriteria { get; init; } = string.Empty;

        /// <summary>
        /// Gets the number of available alternatives.
        /// </summary>
        /// <value>The count of execution plan alternatives</value>
        public int AlternativeCount => AllAlternatives.Length;

        /// <summary>
        /// Gets whether there are multiple alternatives available.
        /// </summary>
        /// <value>True if more than one alternative exists, false otherwise</value>
        public bool HasMultipleAlternatives => AllAlternatives.Length > 1;

        /// <summary>
        /// Gets the execution plan with the shortest estimated execution time.
        /// </summary>
        /// <value>The fastest execution plan, or null if no plans are available</value>
        public ExecutionPlan<T>? FastestPlan => AllAlternatives
            .OrderBy(p => p.EstimatedExecutionTimeMs)
            .FirstOrDefault();

        /// <summary>
        /// Gets the execution plan that uses the fewest devices.
        /// </summary>
        /// <value>The plan with minimum device usage, or null if no plans are available</value>
        public ExecutionPlan<T>? MinimalDevicePlan => AllAlternatives
            .OrderBy(p => p.Devices.Length)
            .FirstOrDefault();

        /// <summary>
        /// Gets execution plans filtered by the specified strategy type.
        /// </summary>
        /// <param name="strategyType">The execution strategy to filter by</param>
        /// <returns>Array of plans using the specified strategy</returns>
        public ExecutionPlan<T>[] GetPlansByStrategy(ExecutionStrategyType strategyType)
            => AllAlternatives.Where(p => p.StrategyType == strategyType).ToArray();

        /// <summary>
        /// Gets execution plans that use a specific number of devices.
        /// </summary>
        /// <param name="deviceCount">The number of devices to filter by</param>
        /// <returns>Array of plans using the specified number of devices</returns>
        public ExecutionPlan<T>[] GetPlansByDeviceCount(int deviceCount)
            => AllAlternatives.Where(p => p.Devices.Length == deviceCount).ToArray();

        /// <summary>
        /// Gets execution plans within the specified execution time range.
        /// </summary>
        /// <param name="minTimeMs">Minimum execution time in milliseconds</param>
        /// <param name="maxTimeMs">Maximum execution time in milliseconds</param>
        /// <returns>Array of plans within the specified time range</returns>
        public ExecutionPlan<T>[] GetPlansByExecutionTime(double minTimeMs, double maxTimeMs)
            => AllAlternatives.Where(p => p.EstimatedExecutionTimeMs >= minTimeMs && 
                                      p.EstimatedExecutionTimeMs <= maxTimeMs).ToArray();
    }
}