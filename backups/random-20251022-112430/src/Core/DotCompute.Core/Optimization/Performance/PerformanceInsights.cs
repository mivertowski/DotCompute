// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Optimization.Models;

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Comprehensive performance insights for monitoring and debugging.
/// </summary>
public class PerformanceInsights
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the total workload signatures.
    /// </summary>
    /// <value>The total workload signatures.</value>
    public int TotalWorkloadSignatures { get; set; }
    /// <summary>
    /// Gets or sets the total backends.
    /// </summary>
    /// <value>The total backends.</value>
    public int TotalBackends { get; set; }
    /// <summary>
    /// Gets or sets the backend states.
    /// </summary>
    /// <value>The backend states.</value>
    public Dictionary<string, BackendPerformanceStateSummary> BackendStates { get; init; } = [];
    /// <summary>
    /// Gets or sets the top performing pairs.
    /// </summary>
    /// <value>The top performing pairs.</value>
    public IList<(WorkloadSignature Workload, string Backend, double PerformanceScore)> TopPerformingPairs { get; init; } = [];
    /// <summary>
    /// Gets or sets the learning statistics.
    /// </summary>
    /// <value>The learning statistics.</value>
    public LearningStatistics LearningStatistics { get; set; } = new();
}