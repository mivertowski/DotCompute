// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Enums;
using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// Analysis results for a kernel.
/// </summary>
public sealed class KernelAnalysis
{
    public required KernelDefinition Definition { get; init; }
    public required bool CanVectorize { get; init; }
    public required int VectorizationFactor { get; init; }
    public required MemoryAccessPattern MemoryAccessPattern { get; init; }
    public required ComputeIntensity ComputeIntensity { get; init; }
    public required int PreferredWorkGroupSize { get; init; }
    public bool HasBranching { get; set; }
    public bool HasLoops { get; set; }
    public int EstimatedComplexity { get; set; }

    // Additional properties required by CpuKernelOptimizer
    public WorkDimensions WorkDimensions { get; set; }
    public long TotalWorkItems { get; set; }
    public int OptimalVectorWidth { get; set; }
    public double ThreadingOverhead { get; set; }
}