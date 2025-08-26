// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Analysis;
using DotCompute.Core.Execution.Types;
using DotCompute.Core.Kernels;

using System;
namespace DotCompute.Core.Execution.Metrics
{
    /// <summary>
    /// Represents a comprehensive analysis of parallel execution performance.
    /// </summary>
    /// <remarks>
    /// This class provides detailed analysis of parallel execution performance,
    /// including bottleneck identification, optimization recommendations, and
    /// strategy suggestions. It combines multiple performance metrics and
    /// analytical insights to provide actionable intelligence for system optimization.
    /// </remarks>
    public class ParallelExecutionAnalysis
    {
        /// <summary>
        /// Gets or sets the overall performance rating (1-10).
        /// </summary>
        /// <value>
        /// A performance rating on a scale of 1 to 10, where 1 indicates very poor performance
        /// and 10 indicates excellent performance. This rating considers execution time,
        /// efficiency, resource utilization, and reliability.
        /// </value>
        public double OverallRating { get; set; }

        /// <summary>
        /// Gets or sets the primary bottlenecks identified.
        /// </summary>
        /// <value>
        /// A list of bottleneck analyses that represent the most significant performance
        /// limitations identified during execution. These bottlenecks are ordered by
        /// severity and impact on overall performance.
        /// </value>
        public List<Analysis.BottleneckAnalysis> Bottlenecks { get; set; } = [];

        /// <summary>
        /// Gets or sets optimization recommendations.
        /// </summary>
        /// <value>
        /// A list of specific, actionable recommendations for improving execution performance.
        /// These recommendations are based on the identified bottlenecks and performance patterns.
        /// </value>
        public List<string> OptimizationRecommendations { get; set; } = [];

        /// <summary>
        /// Gets or sets the recommended execution strategy.
        /// </summary>
        /// <value>
        /// The execution strategy type that is recommended for optimal performance
        /// based on the current workload characteristics and system configuration.
        /// </value>
        public ExecutionStrategyType RecommendedStrategy { get; set; }

        /// <summary>
        /// Gets or sets device utilization analysis.
        /// </summary>
        /// <value>
        /// A dictionary mapping device identifiers to their utilization percentages.
        /// This provides insights into load balancing and resource allocation effectiveness.
        /// </value>
        public Dictionary<string, double> DeviceUtilizationAnalysis { get; set; } = [];

        /// <summary>
        /// Gets the number of critical bottlenecks.
        /// </summary>
        /// <value>
        /// The count of bottlenecks that are considered critical (severity > 0.8).
        /// </value>
        public int CriticalBottleneckCount => Bottlenecks.Count(b => b.IsCritical);

        /// <summary>
        /// Gets the number of bottlenecks requiring attention.
        /// </summary>
        /// <value>
        /// The count of bottlenecks that require attention (severity > 0.6).
        /// </value>
        public int BottlenecksRequiringAttention => Bottlenecks.Count(b => b.RequiresAttention);

        /// <summary>
        /// Gets a value indicating whether the performance is satisfactory.
        /// </summary>
        /// <value>
        /// <c>true</c> if the overall rating is 7 or higher; otherwise, <c>false</c>.
        /// </value>
        public bool IsPerformanceSatisfactory => OverallRating >= 7.0;

        /// <summary>
        /// Gets a value indicating whether load balancing is optimal.
        /// </summary>
        /// <value>
        /// <c>true</c> if device utilization variance is low (indicating good load distribution); otherwise, <c>false</c>.
        /// </value>
        public bool IsLoadBalancingOptimal
        {
            get
            {
                if (DeviceUtilizationAnalysis.Count <= 1)
                {
                    return true;
                }


                var values = DeviceUtilizationAnalysis.Values;
                var mean = values.Average();
                var variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
                var standardDeviation = Math.Sqrt(variance);
                
                // Consider load balancing optimal if standard deviation is less than 15%
                return standardDeviation < 15.0;
            }
        }

        /// <summary>
        /// Gets the most severe bottleneck.
        /// </summary>
        /// <value>
        /// The bottleneck with the highest severity score, or <c>null</c> if no bottlenecks exist.
        /// </value>
        public Analysis.BottleneckAnalysis? MostSevereBottleneck
            => Bottlenecks.Count > 0 ? Bottlenecks.MaxBy(b => b.Severity) : null;

        /// <summary>
        /// Gets the average device utilization.
        /// </summary>
        /// <value>
        /// The mean utilization percentage across all devices.
        /// </value>
        public double AverageDeviceUtilization
            => DeviceUtilizationAnalysis.Count > 0 ? DeviceUtilizationAnalysis.Values.Average() : 0.0;

        /// <summary>
        /// Gets the minimum device utilization.
        /// </summary>
        /// <value>
        /// The lowest utilization percentage among all devices.
        /// </value>
        public double MinDeviceUtilization
            => DeviceUtilizationAnalysis.Count > 0 ? DeviceUtilizationAnalysis.Values.Min() : 0.0;

        /// <summary>
        /// Gets the maximum device utilization.
        /// </summary>
        /// <value>
        /// The highest utilization percentage among all devices.
        /// </value>
        public double MaxDeviceUtilization
            => DeviceUtilizationAnalysis.Count > 0 ? DeviceUtilizationAnalysis.Values.Max() : 0.0;

        /// <summary>
        /// Adds a bottleneck to the analysis.
        /// </summary>
        /// <param name="bottleneck">The bottleneck analysis to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bottleneck"/> is <c>null</c>.</exception>
        public void AddBottleneck(Analysis.BottleneckAnalysis bottleneck)
        {
            ArgumentNullException.ThrowIfNull(bottleneck);
            Bottlenecks.Add(bottleneck);
            
            // Sort bottlenecks by severity (highest first)
            Bottlenecks.Sort((a, b) => b.Severity.CompareTo(a.Severity));
        }

        /// <summary>
        /// Adds an optimization recommendation.
        /// </summary>
        /// <param name="recommendation">The optimization recommendation to add.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="recommendation"/> is null or whitespace.</exception>
        public void AddRecommendation(string recommendation)
        {
            if (string.IsNullOrWhiteSpace(recommendation))
            {
                throw new ArgumentException("Recommendation cannot be null or whitespace.", nameof(recommendation));
            }
            
            OptimizationRecommendations.Add(recommendation);
        }

        /// <summary>
        /// Updates device utilization for a specific device.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="utilization">The utilization percentage (0-100).</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="utilization"/> is not between 0 and 100.</exception>
        public void UpdateDeviceUtilization(string deviceId, double utilization)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device ID cannot be null or whitespace.", nameof(deviceId));
            }
            
            if (utilization is < 0.0 or > 100.0)
            {
                throw new ArgumentOutOfRangeException(nameof(utilization), 
                    "Utilization must be between 0 and 100.");
            }
            
            DeviceUtilizationAnalysis[deviceId] = utilization;
        }

        /// <summary>
        /// Generates automatic recommendations based on the current analysis.
        /// </summary>
        public void GenerateRecommendations()
        {
            OptimizationRecommendations.Clear();
            
            // Recommendations based on overall rating
            if (OverallRating < 5.0)
            {
                AddRecommendation("Consider switching to a different execution strategy for better performance.");
            }
            
            // Recommendations based on bottlenecks
            if (CriticalBottleneckCount > 0)
            {
                AddRecommendation($"Address {CriticalBottleneckCount} critical bottleneck(s) immediately.");
            }
            
            // Recommendations based on load balancing
            if (!IsLoadBalancingOptimal)
            {
                AddRecommendation("Improve load balancing across devices to maximize resource utilization.");
            }
            
            // Recommendations based on device utilization
            if (AverageDeviceUtilization < 50.0)
            {
                AddRecommendation("Device utilization is low. Consider increasing workload size or using fewer devices.");
            }
            else if (AverageDeviceUtilization > 90.0)
            {
                AddRecommendation("Device utilization is very high. Consider adding more devices or optimizing kernels.");
            }
            
            // Recommendations based on specific bottleneck types
            var memoryBottlenecks = Bottlenecks.Where(b => b.Type == BottleneckType.MemoryBandwidth).Count();
            if (memoryBottlenecks > 0)
            {
                AddRecommendation("Optimize memory access patterns and consider memory pooling strategies.");
            }
            
            var communicationBottlenecks = Bottlenecks.Where(b => b.Type == BottleneckType.Communication).Count();
            if (communicationBottlenecks > 0)
            {
                AddRecommendation("Reduce communication overhead by overlapping computation with communication.");
            }
        }

        /// <summary>
        /// Returns a string representation of the execution analysis.
        /// </summary>
        /// <returns>
        /// A string containing the overall rating, bottleneck summary, and key recommendations.
        /// </returns>
        public override string ToString()
        {
            var criticalCount = CriticalBottleneckCount;
            var attentionCount = BottlenecksRequiringAttention;
            
            return $"Performance Rating: {OverallRating:F1}/10, " +
                   $"Bottlenecks: {criticalCount} critical, {attentionCount} requiring attention, " +
                   $"Strategy: {RecommendedStrategy}, " +
                   $"Avg Utilization: {AverageDeviceUtilization:F1}%";
        }
    }
}