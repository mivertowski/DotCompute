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
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    /// <value>The definition.</value>
    public required KernelDefinition Definition { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether vectorize.
    /// </summary>
    /// <value>The can vectorize.</value>
    public required bool CanVectorize { get; init; }
    /// <summary>
    /// Gets or sets the vectorization factor.
    /// </summary>
    /// <value>The vectorization factor.</value>
    public required int VectorizationFactor { get; init; }
    /// <summary>
    /// Gets or sets the memory access pattern.
    /// </summary>
    /// <value>The memory access pattern.</value>
    public required MemoryAccessPattern MemoryAccessPattern { get; init; }
    /// <summary>
    /// Gets or sets the compute intensity.
    /// </summary>
    /// <value>The compute intensity.</value>
    public required ComputeIntensity ComputeIntensity { get; init; }
    /// <summary>
    /// Gets or sets the preferred work group size.
    /// </summary>
    /// <value>The preferred work group size.</value>
    public required int PreferredWorkGroupSize { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether branching.
    /// </summary>
    /// <value>The has branching.</value>
    public bool HasBranching { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether loops.
    /// </summary>
    /// <value>The has loops.</value>
    public bool HasLoops { get; set; }
    /// <summary>
    /// Gets or sets the estimated complexity.
    /// </summary>
    /// <value>The estimated complexity.</value>
    public int EstimatedComplexity { get; set; }
    /// <summary>
    /// Gets or sets the work dimensions.
    /// </summary>
    /// <value>The work dimensions.</value>

    // Additional properties required by CpuKernelOptimizer
    public WorkDimensions WorkDimensions { get; set; }
    /// <summary>
    /// Gets or sets the total work items.
    /// </summary>
    /// <value>The total work items.</value>
    public long TotalWorkItems { get; set; }
    /// <summary>
    /// Gets or sets the optimal vector width.
    /// </summary>
    /// <value>The optimal vector width.</value>
    public int OptimalVectorWidth { get; set; }
    /// <summary>
    /// Gets or sets the threading overhead.
    /// </summary>
    /// <value>The threading overhead.</value>
    public double ThreadingOverhead { get; set; }
}