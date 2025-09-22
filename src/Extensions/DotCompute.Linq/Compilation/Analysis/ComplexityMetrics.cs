// <copyright file="ComplexityMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
namespace DotCompute.Linq.Compilation.Analysis;
{
/// <summary>
/// Contains computational complexity metrics for expressions.
/// </summary>
public record ComplexityMetrics
{
    /// <summary>Gets the computational complexity (Big O notation).</summary>
    public ComplexityClass ComputationalComplexity { get; init; } = ComplexityClass.Constant;
    /// <summary>Gets the space complexity.</summary>
    public ComplexityClass SpaceComplexity { get; init; } = ComplexityClass.Constant;
    /// <summary>Gets the estimated operation count.</summary>
    public long OperationCount { get; init; }
    /// <summary>Gets the memory access count.</summary>
    public long MemoryAccesses { get; init; }
    /// <summary>Gets the algorithmic complexity factor.</summary>
    public double ComplexityFactor { get; init; } = 1.0;
    /// <summary>Gets the parallelization potential (0.0 to 1.0).</summary>
    public double ParallelizationPotential { get; init; } = 1.0;
    /// <summary>Gets the cache efficiency estimate (0.0 to 1.0).</summary>
    public double CacheEfficiency { get; init; } = 0.8;
    /// <summary>Gets detailed per-operation complexity breakdown.</summary>
    public Dictionary<string, double> OperationComplexity { get; init; } = [];
    /// <summary>Gets memory access patterns that affect complexity.</summary>
    public List<MemoryAccessComplexity> MemoryAccessPatterns { get; init; } = [];
    /// <summary>Gets whether the complexity is data-dependent.</summary>
    public bool IsDataDependent { get; init; }
    /// <summary>Gets the worst-case complexity scenario.</summary>
    public string WorstCaseScenario { get; init; } = string.Empty;
    /// <summary>Gets whether the operation is memory-bound.</summary>
    public bool MemoryBound => MemoryAccesses > OperationCount * 2;
    /// <summary>Gets whether the operation can benefit from shared memory optimization.</summary>
    public bool CanBenefitFromSharedMemory => MemoryBound && ParallelizationPotential > 0.5;
    /// <summary>
    /// Gets the overall compute complexity as a numeric value.
    /// </summary>
    public double ComputeComplexity => ComplexityFactor * (1.0 + OperationCount / 1000000.0);
    /// Gets the overall complexity as an integer score.
    public int OverallComplexity => (int)Math.Round(ComplexityScore);
    /// Gets the memory complexity score.
    public double MemoryComplexity => Math.Min(10.0, MemoryAccesses / 1000000.0 * (1.0 / CacheEfficiency));
    /// Gets the memory usage in bytes.
    public long MemoryUsage => MemoryAccesses * 8; // Assume 8 bytes per access as average
    /// Gets the normalized complexity score (0.0 to 10.0).
    public double ComplexityScore => Math.Min(10.0, ComputationalComplexity switch
    {
        ComplexityClass.Constant => 1.0,
        ComplexityClass.Logarithmic => 2.0,
        ComplexityClass.Linear => 3.0,
        ComplexityClass.Linearithmic => 4.0,
        ComplexityClass.Quadratic => 6.0,
        ComplexityClass.Cubic => 8.0,
        ComplexityClass.Exponential => 10.0,
        ComplexityClass.Factorial => 10.0,
        _ => 3.0
    } * ComplexityFactor);
}
// GlobalMemoryAccessPattern is defined in PipelineAnalysisTypes.cs to avoid duplication
/// <summary>
/// Represents memory access complexity for specific patterns.
/// </summary>
public record MemoryAccessComplexity
{
    /// <summary>Gets the access pattern type.</summary>
    public MemoryAccessPattern Pattern { get; init; }

    /// <summary>Gets the frequency of this pattern.</summary>
    public int Frequency { get; init; }

    /// <summary>Gets the complexity impact factor.</summary>
    public double ComplexityImpact { get; init; }

    /// <summary>Gets the memory region size.</summary>
    public long RegionSize { get; init; }

    /// <summary>Gets the stride size for strided access.</summary>
    public int StrideSize { get; init; } = 1;
}
/// <summary>
/// Represents a memory access hotspot.
/// </summary>
public record MemoryHotspot
{
    /// <summary>Gets the location of the hotspot.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>Gets the access frequency.</summary>
    public int AccessFrequency { get; init; }

    /// <summary>Gets the memory region accessed.</summary>
    public MemoryRegion Region { get; init; } = new();

    /// <summary>Gets the hotspot intensity (0.0 to 1.0).</summary>
    public double Intensity { get; init; }

    /// <summary>Gets optimization recommendations.</summary>
    public List<string> OptimizationRecommendations { get; init; } = [];
}
/// <summary>
/// Represents a memory access conflict.
/// </summary>
public record MemoryConflict
{
    /// <summary>Gets the conflicting access locations.</summary>
    public List<string> ConflictingLocations { get; init; } = [];

    /// <summary>Gets the conflict type.</summary>
    public ConflictType Type { get; init; }

    /// <summary>Gets the severity of the conflict (0.0 to 1.0).</summary>
    public double Severity { get; init; }

    /// <summary>Gets the performance impact estimate.</summary>
    public double PerformanceImpact { get; init; }

    /// <summary>Gets suggested resolutions.</summary>
    public List<string> Resolutions { get; init; } = [];
}
/// <summary>
/// Defines computational complexity classes.
/// </summary>
public enum ComplexityClass
{
    /// <summary>O(1) - Constant time.</summary>
    Constant,

    /// <summary>O(log n) - Logarithmic time.</summary>
    Logarithmic,

    /// <summary>O(n) - Linear time.</summary>
    Linear,

    /// <summary>O(n log n) - Linearithmic time.</summary>
    Linearithmic,

    /// <summary>O(n²) - Quadratic time.</summary>
    Quadratic,

    /// <summary>O(n³) - Cubic time.</summary>
    Cubic,

    /// <summary>O(2^n) - Exponential time.</summary>
    Exponential,

    /// <summary>O(n!) - Factorial time.</summary>
    Factorial
}
/// <summary>
/// Defines types of memory conflicts.
/// </summary>
public enum ConflictType
{
    /// <summary>Bank conflict in shared memory.</summary>
    BankConflict,

    /// <summary>Cache line conflict.</summary>
    CacheLineConflict,

    /// <summary>False sharing between threads.</summary>
    FalseSharing,

    /// <summary>Memory coalescing conflict.</summary>
    CoalescingConflict,

    /// <summary>Write-after-read dependency.</summary>
    WriteAfterRead,

    /// <summary>Read-after-write dependency.</summary>
    ReadAfterWrite
}
