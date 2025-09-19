// <copyright file="AnalysisContext.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotCompute.Linq.Compilation.Analysis;

/// <summary>
/// Context for expression analysis operations.
/// </summary>
public class AnalysisContext
{
    /// <summary>Gets or sets the expression tree depth.</summary>
    public int Depth { get; set; }

    /// <summary>Gets or sets the current expression being analyzed.</summary>
    public Expression? CurrentExpression { get; set; }

    /// <summary>Gets or sets the parent expression.</summary>
    public Expression? ParentExpression { get; set; }

    /// <summary>Gets the operator chain in the expression tree.</summary>
    public List<ExpressionType> OperatorChain { get; } = [];

    /// <summary>Gets type usage information.</summary>
    public Dictionary<Type, TypeUsageInfo> TypeUsage { get; } = [];

    /// <summary>Gets parallelization opportunities.</summary>
    public Dictionary<Expression, ParallelizationOpportunity> ParallelizationOpportunities { get; } = [];

    /// <summary>Gets data flow bottlenecks.</summary>
    public List<string> DataFlowBottlenecks { get; } = [];

    /// <summary>Gets the global memory access pattern.</summary>
    public GlobalMemoryPattern GlobalMemoryPattern { get; set; } = new();

    /// <summary>Gets variable dependencies.</summary>
    public Dictionary<string, DependencyInfo> VariableDependencies { get; } = [];

    /// <summary>Gets method call information.</summary>
    public Dictionary<string, MethodCallInfo> MethodCalls { get; } = [];

    /// <summary>Gets the estimated computational complexity.</summary>
    public double ComputationalComplexity { get; set; }

    /// <summary>Gets or sets whether the expression tree is suitable for GPU execution.</summary>
    public bool IsGpuSuitable { get; set; } = true;

    /// <summary>Gets collected optimization hints.</summary>
    public List<OptimizationHint> OptimizationHints { get; } = [];

    /// <summary>
    /// Adds an optimization hint to the context.
    /// </summary>
    /// <param name="hint">The optimization hint to add.</param>
    public void AddOptimizationHint(OptimizationHint hint)
    {
        OptimizationHints.Add(hint);
    }

    /// <summary>
    /// Records a parallelization opportunity.
    /// </summary>
    /// <param name="expression">The expression with the opportunity.</param>
    /// <param name="opportunity">The parallelization opportunity details.</param>
    public void RecordParallelizationOpportunity(Expression expression, ParallelizationOpportunity opportunity)
    {
        ParallelizationOpportunities[expression] = opportunity;
    }

    /// <summary>
    /// Records type usage information.
    /// </summary>
    /// <param name="type">The type being used.</param>
    /// <param name="usage">The usage information.</param>
    public void RecordTypeUsage(Type type, TypeUsageInfo usage)
    {
        TypeUsage[type] = usage;
    }

    /// <summary>
    /// Creates a child analysis context for nested analysis.
    /// </summary>
    /// <param name="expression">The expression for the child context.</param>
    /// <returns>A new child analysis context.</returns>
    public AnalysisContext CreateChildContext(Expression expression)
    {
        return new AnalysisContext
        {
            Depth = Depth + 1,
            CurrentExpression = expression,
            ParentExpression = CurrentExpression,
            ComputationalComplexity = ComputationalComplexity,
            IsGpuSuitable = IsGpuSuitable
        };
    }
}

/// <summary>
/// Contains global memory access pattern information.
/// </summary>
public class GlobalMemoryPattern
{
    /// <summary>Gets or sets whether there are memory coalescing opportunities.</summary>
    public bool HasCoalescingOpportunities { get; set; }

    /// <summary>Gets or sets the predominant access pattern.</summary>
    public MemoryAccessPattern PredominantPattern { get; set; } = MemoryAccessPattern.Sequential;

    /// <summary>Gets or sets the estimated memory bandwidth utilization (0.0 to 1.0).</summary>
    public double BandwidthUtilization { get; set; } = 1.0;

    /// <summary>Gets memory access regions.</summary>
    public List<MemoryRegion> AccessRegions { get; } = [];

    /// <summary>Gets or sets whether memory prefetching would be beneficial.</summary>
    public bool BenefitsFromPrefetching { get; set; }

    /// <summary>Gets or sets the cache hit ratio estimate (0.0 to 1.0).</summary>
    public double EstimatedCacheHitRatio { get; set; } = 0.8;
}

/// <summary>
/// Represents a memory access region.
/// </summary>
public record MemoryRegion
{
    /// <summary>Gets the starting offset of the region.</summary>
    public long StartOffset { get; init; }

    /// <summary>Gets the size of the region in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Gets the access pattern for this region.</summary>
    public MemoryAccessPattern AccessPattern { get; init; }

    /// <summary>Gets the access frequency.</summary>
    public int AccessFrequency { get; init; }

    /// <summary>Gets whether this region is read-only.</summary>
    public bool IsReadOnly { get; init; }
}

/// <summary>
/// Contains information about method calls in expressions.
/// </summary>
public record MethodCallInfo
{
    /// <summary>Gets the method name.</summary>
    public string MethodName { get; init; } = string.Empty;

    /// <summary>Gets the declaring type of the method.</summary>
    public Type? DeclaringType { get; init; }

    /// <summary>Gets the parameter types.</summary>
    public List<Type> ParameterTypes { get; init; } = [];

    /// <summary>Gets the return type.</summary>
    public Type? ReturnType { get; init; }

    /// <summary>Gets whether the method has side effects.</summary>
    public bool HasSideEffects { get; init; }

    /// <summary>Gets whether the method is GPU-compatible.</summary>
    public bool IsGpuCompatible { get; init; } = true;

    /// <summary>Gets the estimated execution cost.</summary>
    public double ExecutionCost { get; init; } = 1.0;

    /// <summary>Gets whether the method supports vectorization.</summary>
    public bool SupportsVectorization { get; init; }
}