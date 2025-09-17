// <copyright file="IComplexityMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;

namespace DotCompute.Abstractions.Analysis;

/// <summary>
/// Common interface for all complexity metrics implementations.
/// </summary>
public interface IComplexityMetrics
{
    /// <summary>Gets the computational complexity score.</summary>
    int ComputationalComplexity { get; }

    /// <summary>Gets the memory complexity score.</summary>
    int MemoryComplexity { get; }

    /// <summary>Gets the parallelization complexity score.</summary>
    int ParallelizationComplexity { get; }

    /// <summary>Gets the overall complexity score.</summary>
    int OverallComplexity { get; }

    /// <summary>Gets the estimated operation count.</summary>
    long OperationCount { get; }

    /// <summary>Gets the memory usage in bytes.</summary>
    long MemoryUsage { get; }

    /// <summary>Gets the parallelization potential (0.0 to 1.0).</summary>
    double ParallelizationPotential { get; }

    /// <summary>Gets the cache efficiency estimate (0.0 to 1.0).</summary>
    double CacheEfficiency { get; }

    /// <summary>Gets the complexity factor.</summary>
    double ComplexityFactor { get; }

    /// <summary>Gets the normalized complexity score (0.0 to 10.0).</summary>
    double ComplexityScore { get; }

    /// <summary>Gets whether the complexity is data-dependent.</summary>
    bool IsDataDependent { get; }

    /// <summary>Gets whether the operation is memory-bound.</summary>
    bool MemoryBound { get; }
}

/// <summary>
/// Extended interface for advanced complexity metrics.
/// </summary>
public interface IAdvancedComplexityMetrics : IComplexityMetrics
{
    /// <summary>Gets the computational complexity class (Big O notation).</summary>
    ComplexityClass ComputationalComplexityClass { get; }

    /// <summary>Gets the space complexity.</summary>
    ComplexityClass SpaceComplexity { get; }

    /// <summary>Gets the memory access count.</summary>
    long MemoryAccesses { get; }

    /// <summary>Gets detailed per-operation complexity breakdown.</summary>
    IReadOnlyDictionary<string, double> OperationComplexity { get; }

    /// <summary>Gets memory access patterns that affect complexity.</summary>
    IReadOnlyList<MemoryAccessComplexity> MemoryAccessPatterns { get; }

    /// <summary>Gets the worst-case complexity scenario.</summary>
    string WorstCaseScenario { get; }

    /// <summary>Gets whether the operation can benefit from shared memory optimization.</summary>
    bool CanBenefitFromSharedMemory { get; }

    /// <summary>Gets the compute complexity as a numeric value.</summary>
    double ComputeComplexity { get; }
}