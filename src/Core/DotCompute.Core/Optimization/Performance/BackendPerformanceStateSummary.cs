// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Summary of backend performance state for external consumption.
/// </summary>
public class BackendPerformanceStateSummary
{
    public string BackendId { get; set; } = string.Empty;
    public double CurrentUtilization { get; set; }
    public double RecentAverageExecutionTimeMs { get; set; }
    public int RecentExecutionCount { get; set; }
    public DateTimeOffset LastExecutionTime { get; set; }
}