// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Optimization.Performance;

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Comprehensive performance report for kernel execution.
/// </summary>
public class PerformanceReport
{
    public required string KernelName { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public int ExecutionCount { get; set; }
    public Dictionary<string, BackendPerformanceStats> Backends { get; set; } = new();
    public OverallPerformanceStats? OverallStats { get; set; }
    public string Summary { get; set; } = string.Empty;
}