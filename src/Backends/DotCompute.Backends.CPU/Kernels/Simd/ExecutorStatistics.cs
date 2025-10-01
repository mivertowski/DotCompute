// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// Performance statistics for SIMD executor operations
/// </summary>
public sealed class ExecutorStatistics
{
    public long TotalExecutions { get; init; }
    public long TotalElements { get; init; }
    public long VectorizedElements { get; init; }
    public long ScalarElements { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public double VectorizationRatio { get; init; }
    public double PerformanceGain { get; init; }
}