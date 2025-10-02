// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Optimization.Models;

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Comprehensive performance insights for monitoring and debugging.
/// </summary>
public class PerformanceInsights
{
    public DateTimeOffset Timestamp { get; set; }
    public int TotalWorkloadSignatures { get; set; }
    public int TotalBackends { get; set; }
    public Dictionary<string, BackendPerformanceStateSummary> BackendStates { get; set; } = [];
    public List<(WorkloadSignature Workload, string Backend, double PerformanceScore)> TopPerformingPairs { get; set; } = [];
    public LearningStatistics LearningStatistics { get; set; } = new();
}