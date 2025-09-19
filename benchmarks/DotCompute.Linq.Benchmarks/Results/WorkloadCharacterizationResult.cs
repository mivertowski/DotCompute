// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Optimization;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Workload characterization result.
/// </summary>
public class WorkloadCharacterizationResult
{
    public WorkloadCharacteristics Characteristics { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
    public int ResultSize { get; set; }
    public double ThroughputOpsPerSec { get; set; }
    public double OptimizationEffectiveness { get; set; }
}