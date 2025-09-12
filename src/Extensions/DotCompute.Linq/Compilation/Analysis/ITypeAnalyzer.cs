// <copyright file="ITypeAnalyzer.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotCompute.Linq.Compilation.Analysis;

/// <summary>
/// Interface for analyzing types in expression trees.
/// </summary>
public interface ITypeAnalyzer
{
    /// <summary>Gets the type this analyzer handles.</summary>
    Type TargetType { get; }

    /// <summary>
    /// Analyzes type usage within an expression.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>Type usage information.</returns>
    TypeUsageInfo AnalyzeUsage(Expression expression, AnalysisContext context);

    /// <summary>
    /// Determines if the type supports vectorization.
    /// </summary>
    /// <returns>True if the type can be vectorized; otherwise, false.</returns>
    bool SupportsVectorization();

    /// <summary>
    /// Gets the optimal memory alignment for this type.
    /// </summary>
    /// <returns>The alignment in bytes.</returns>
    int GetOptimalAlignment();

    /// <summary>
    /// Estimates the computational complexity of operations on this type.
    /// </summary>
    /// <param name="operation">The operation being performed.</param>
    /// <returns>Complexity factor (relative to int32 operations).</returns>
    double EstimateOperationComplexity(ExpressionType operation);

    /// <summary>
    /// Gets optimization hints specific to this type.
    /// </summary>
    /// <param name="expression">The expression context.</param>
    /// <returns>Type-specific optimization hints.</returns>
    IEnumerable<OptimizationHint> GetOptimizationHints(Expression expression);

    /// <summary>
    /// Analyzes a type expression with context.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="context">Optional analysis context.</param>
    /// <returns>Type usage information.</returns>
    TypeUsageInfo Analyze(Expression expression, object? context = null);
}

/// <summary>
/// Interface for analyzing operators in expression trees.
/// </summary>
public interface IOperatorAnalyzer
{
    /// <summary>Gets the expression type this analyzer handles.</summary>
    ExpressionType TargetOperator { get; }

    /// <summary>
    /// Analyzes operator usage within an expression.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>Operator analysis information.</returns>
    OperatorInfo AnalyzeOperator(Expression expression, AnalysisContext context);

    /// <summary>
    /// Analyzes an expression and returns pipeline operator information for the compilation pipeline.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>Pipeline operator information.</returns>
    DotCompute.Linq.Pipelines.Analysis.OperatorInfo Analyze(Expression expression, AnalysisContext context);

    /// <summary>
    /// Determines parallelization opportunities for this operator.
    /// </summary>
    /// <param name="expression">The expression context.</param>
    /// <returns>Parallelization opportunity information.</returns>
    ParallelizationOpportunity AnalyzeParallelization(Expression expression);

    /// <summary>
    /// Estimates the execution cost of this operator.
    /// </summary>
    /// <param name="expression">The expression context.</param>
    /// <returns>Relative execution cost.</returns>
    double EstimateExecutionCost(Expression expression);
}

// TypeUsageInfo is defined in PipelineAnalysisTypes.cs to avoid duplication

/// <summary>
/// Contains information about operator analysis.
/// </summary>
public record OperatorInfo
{
    /// <summary>Gets the operator type.</summary>
    public ExpressionType Operator { get; init; }

    /// <summary>Gets the computational complexity.</summary>
    public double Complexity { get; init; }

    /// <summary>Gets whether the operator is commutative.</summary>
    public bool IsCommutative { get; init; }

    /// <summary>Gets whether the operator is associative.</summary>
    public bool IsAssociative { get; init; }

    /// <summary>Gets the memory access pattern.</summary>
    public MemoryAccessPattern AccessPattern { get; init; } = MemoryAccessPattern.Sequential;

    /// <summary>Gets parallelization opportunities.</summary>
    public ParallelizationOpportunity ParallelizationOpportunity { get; init; } = new();
}

/// <summary>
/// Contains information about parallelization opportunities.
/// </summary>
public record ParallelizationOpportunity
{
    /// <summary>Gets whether the operation is suitable for vectorization.</summary>
    public bool VectorizationSuitable { get; init; }

    /// <summary>Gets whether the operation supports parallel execution.</summary>
    public bool SupportsParallelExecution { get; init; }

    /// <summary>Gets the recommended parallelization degree.</summary>
    public int RecommendedParallelism { get; init; } = 1;

    /// <summary>Gets data dependencies that affect parallelization.</summary>
    public List<string> DataDependencies { get; init; } = new();

    /// <summary>Gets the estimated speedup from parallelization.</summary>
    public double EstimatedSpeedup { get; init; } = 1.0;

    /// <summary>Gets the parallelization potential score (0-1).</summary>
    public double ParallelizationPotential { get; init; }

    /// <summary>Gets whether this operation is suitable for parallelization.</summary>
    public bool IsSuitable => VectorizationSuitable || SupportsParallelExecution;
}

// DependencyInfo is defined in PipelineAnalysisTypes.cs to avoid duplication

/// <summary>
/// Defines memory usage patterns for types.
/// </summary>
public enum MemoryUsagePattern
{
    /// <summary>Sequential memory access.</summary>
    Sequential,

    /// <summary>Random memory access.</summary>
    Random,

    /// <summary>Strided memory access.</summary>
    Strided,

    /// <summary>Scattered memory access.</summary>
    Scattered,

    /// <summary>Read-only access.</summary>
    ReadOnly,

    /// <summary>Write-only access.</summary>
    WriteOnly,

    /// <summary>Read-write access.</summary>
    ReadWrite
}

/// <summary>
/// Defines memory access patterns for operations.
/// </summary>
public enum MemoryAccessPattern
{
    /// <summary>Sequential access pattern.</summary>
    Sequential,

    /// <summary>Random access pattern.</summary>
    Random,

    /// <summary>Strided access pattern.</summary>
    Strided,

    /// <summary>Broadcast access pattern.</summary>
    Broadcast,

    /// <summary>Gather access pattern.</summary>
    Gather,

    /// <summary>Scatter access pattern.</summary>
    Scatter,

    /// <summary>Cache-friendly access pattern.</summary>
    CacheFriendly,

    /// <summary>Cache-unfriendly access pattern.</summary>
    CacheUnfriendly
}