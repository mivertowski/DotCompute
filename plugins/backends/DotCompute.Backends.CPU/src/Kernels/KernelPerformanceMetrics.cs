// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// Performance metrics for a compiled kernel.
/// </summary>
public sealed class KernelPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public required string KernelName { get; init; }
    
    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public required long ExecutionCount { get; init; }
    
    /// <summary>
    /// Gets or sets the total execution time in milliseconds.
    /// </summary>
    public required double TotalExecutionTimeMs { get; init; }
    
    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public required double AverageExecutionTimeMs { get; init; }
    
    /// <summary>
    /// Gets or sets whether vectorization was enabled.
    /// </summary>
    public required bool VectorizationEnabled { get; init; }
    
    /// <summary>
    /// Gets or sets the vector width used.
    /// </summary>
    public required int VectorWidth { get; init; }
    
    /// <summary>
    /// Gets or sets the instruction sets used.
    /// </summary>
    public required IReadOnlySet<string> InstructionSets { get; init; }
    
    /// <summary>
    /// Gets the estimated speedup from vectorization.
    /// </summary>
    public double EstimatedSpeedup => VectorizationEnabled ? VectorWidth / 32.0 : 1.0;
    
    /// <summary>
    /// Gets the throughput in operations per second.
    /// </summary>
    public double ThroughputOpsPerSecond => ExecutionCount > 0 && TotalExecutionTimeMs > 0 
        ? (ExecutionCount * 1000.0) / TotalExecutionTimeMs 
        : 0;
}