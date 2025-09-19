// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Memory optimization benchmark result.
/// </summary>
public class MemoryOptimizationResult
{
    public TimeSpan ExecutionTime { get; set; }
    public long PeakMemoryUsage { get; set; }
    public long FinalMemoryUsage { get; set; }
    public int ResultSize { get; set; }
    public double MemoryEfficiency { get; set; }
}