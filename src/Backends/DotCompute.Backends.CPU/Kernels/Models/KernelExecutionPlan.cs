// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// Execution plan for a compiled kernel.
/// </summary>
public sealed class KernelExecutionPlan
{
    public required KernelAnalysis Analysis { get; init; }
    public required bool UseVectorization { get; init; }
    public bool UseParallelization { get; init; }
    public required int VectorWidth { get; init; }
    public required int VectorizationFactor { get; init; }
    public required int WorkGroupSize { get; init; }
    public int OptimalThreadCount { get; init; }
    public required int MemoryPrefetchDistance { get; init; }
    public required bool EnableLoopUnrolling { get; init; }
    public required IReadOnlySet<string> InstructionSets { get; init; }
    public List<string> MemoryOptimizations { get; init; } = new();
    public List<string> CacheOptimizations { get; init; } = new();
}