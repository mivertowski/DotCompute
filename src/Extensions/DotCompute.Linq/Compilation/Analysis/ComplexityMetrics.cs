// <copyright file="ComplexityMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;

namespace DotCompute.Linq.Compilation.Analysis;

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
    public Dictionary<string, double> OperationComplexity { get; init; } = new();

    /// <summary>Gets memory access patterns that affect complexity.</summary>
    public List<MemoryAccessComplexity> MemoryAccessPatterns { get; init; } = new();

    /// <summary>Gets whether the complexity is data-dependent.</summary>
    public bool IsDataDependent { get; init; }

    /// <summary>Gets the worst-case complexity scenario.</summary>
    public string WorstCaseScenario { get; init; } = string.Empty;
}

/// <summary>
/// Represents global memory access patterns across an expression tree.
/// </summary>
public record GlobalMemoryAccessPattern
{
    /// <summary>Gets the predominant access pattern.</summary>
    public MemoryAccessPattern PredominantPattern { get; init; } = MemoryAccessPattern.Sequential;

    /// <summary>Gets the total memory footprint in bytes.</summary>
    public long TotalMemoryFootprint { get; init; }

    /// <summary>Gets the working set size in bytes.</summary>
    public long WorkingSetSize { get; init; }

    /// <summary>Gets the memory bandwidth utilization (0.0 to 1.0).</summary>
    public double BandwidthUtilization { get; init; } = 1.0;

    /// <summary>Gets whether coalescing opportunities exist.</summary>
    public bool HasCoalescingOpportunities { get; init; }

    /// <summary>Gets whether prefetching would be beneficial.</summary>
    public bool BenefitsFromPrefetching { get; init; }

    /// <summary>Gets the estimated cache hit ratio (0.0 to 1.0).</summary>
    public double EstimatedCacheHitRatio { get; init; } = 0.8;

    /// <summary>Gets memory access hotspots.</summary>
    public List<MemoryHotspot> Hotspots { get; init; } = new();

    /// <summary>Gets memory access conflicts.</summary>
    public List<MemoryConflict> Conflicts { get; init; } = new();

    /// <summary>Gets the memory access locality factor (0.0 to 1.0).</summary>
    public double LocalityFactor { get; init; } = 0.8;
}

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
    public List<string> OptimizationRecommendations { get; init; } = new();
}

/// <summary>
/// Represents a memory access conflict.
/// </summary>
public record MemoryConflict
{
    /// <summary>Gets the conflicting access locations.</summary>
    public List<string> ConflictingLocations { get; init; } = new();

    /// <summary>Gets the conflict type.</summary>
    public ConflictType Type { get; init; }

    /// <summary>Gets the severity of the conflict (0.0 to 1.0).</summary>
    public double Severity { get; init; }

    /// <summary>Gets the performance impact estimate.</summary>
    public double PerformanceImpact { get; init; }

    /// <summary>Gets suggested resolutions.</summary>
    public List<string> Resolutions { get; init; } = new();
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