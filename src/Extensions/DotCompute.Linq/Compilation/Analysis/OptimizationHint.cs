// <copyright file="OptimizationHint.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
namespace DotCompute.Linq.Compilation.Analysis;
/// <summary>
/// Represents an optimization hint generated during expression analysis.
/// </summary>
public record OptimizationHint
{
    /// <summary>Gets the type of optimization hint.</summary>
    public OptimizationHintType Type { get; init; }
    /// <summary>Gets the human-readable description of the hint.</summary>
    public string Description { get; init; }
    /// <summary>Gets the estimated impact of applying this optimization.</summary>
    public OptimizationImpact Impact { get; init; }
    /// <summary>Gets the confidence level in this hint (0.0 to 1.0).</summary>
    public double Confidence { get; init; } = 1.0;
    /// <summary>Gets additional metadata associated with this hint.</summary>
    public object? Metadata { get; init; }
    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizationHint"/> record.
    /// </summary>
    /// <param name="type">The optimization hint type.</param>
    /// <param name="description">The description of the hint.</param>
    /// <param name="impact">The expected impact of applying this optimization.</param>
    public OptimizationHint(OptimizationHintType type, string description, OptimizationImpact impact)
    {
        Type = type;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Impact = impact;
    }
}
/// Defines the types of optimization hints that can be generated.
public enum OptimizationHintType
    /// <summary>Vectorization/SIMD optimization opportunity.</summary>
    Vectorization,
    /// <summary>Memory access coalescing optimization.</summary>
    MemoryCoalescing,
    /// <summary>Kernel fusion optimization.</summary>
    KernelFusion,
    /// <summary>Type specialization optimization.</summary>
    TypeSpecialization,
    /// <summary>Loop unrolling optimization.</summary>
    LoopUnrolling,
    /// <summary>Constant folding optimization.</summary>
    ConstantFolding,
    /// <summary>Memory prefetching optimization.</summary>
    MemoryPrefetching,
    /// <summary>Parallel execution optimization.</summary>
    Parallelization,
    /// <summary>Cache optimization.</summary>
    CacheOptimization,
    /// <summary>Backend-specific optimization.</summary>
    BackendSpecific,
    /// <summary>General performance optimization.</summary>
    Performance
/// Defines the expected impact levels for optimization hints.
public enum OptimizationImpact
    /// <summary>Low impact - minor performance improvement expected.</summary>
    Low,
    /// <summary>Medium impact - moderate performance improvement expected.</summary>
    Medium,
    /// <summary>High impact - significant performance improvement expected.</summary>
    High,
    /// <summary>Critical impact - major performance improvement expected.</summary>
    Critical
