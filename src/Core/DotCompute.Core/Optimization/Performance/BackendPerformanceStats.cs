// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Historical performance statistics for a backend on a specific workload.
/// </summary>
public class BackendPerformanceStats
{
    public int SampleCount { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double ExecutionTimeStdDev { get; set; }
    public double MinExecutionTimeMs { get; set; }
    public double MaxExecutionTimeMs { get; set; }
    public double AverageThroughput { get; set; }
    public double AverageMemoryUsage { get; set; }
    public float ReliabilityScore { get; set; } // 0-1 based on success rate
    public DateTimeOffset LastUpdated { get; set; }
}