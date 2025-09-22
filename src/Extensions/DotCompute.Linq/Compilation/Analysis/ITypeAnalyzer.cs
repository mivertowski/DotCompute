// <copyright file="ITypeAnalyzer.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
namespace DotCompute.Linq.Compilation.Analysis;
{
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
    /// Determines if the type supports vectorization.
    /// <returns>True if the type can be vectorized; otherwise, false.</returns>
    bool SupportsVectorization();
    /// Gets the optimal memory alignment for this type.
    /// <returns>The alignment in bytes.</returns>
    int GetOptimalAlignment();
    /// Estimates the computational complexity of operations on this type.
    /// <param name="operation">The operation being performed.</param>
    /// <returns>Complexity factor (relative to int32 operations).</returns>
    double EstimateOperationComplexity(ExpressionType operation);
    /// Gets optimization hints specific to this type.
    /// <param name="expression">The expression context.</param>
    /// <returns>Type-specific optimization hints.</returns>
    IEnumerable<OptimizationHint> GetOptimizationHints(Expression expression);
    /// Analyzes a type expression with context.
    /// <param name="context">Optional analysis context.</param>
    TypeUsageInfo Analyze(Expression expression, object? context = null);
}
/// Interface for analyzing operators in expression trees.
public interface IOperatorAnalyzer
{
    /// <summary>Gets the expression type this analyzer handles.</summary>
    ExpressionType TargetOperator { get; }
    /// Analyzes operator usage within an expression.
    /// <returns>Operator analysis information.</returns>
    OperatorInfo AnalyzeOperator(Expression expression, AnalysisContext context);
    /// Analyzes an expression and returns pipeline operator information for the compilation pipeline.
    /// <returns>Pipeline operator information.</returns>
    DotCompute.Linq.Pipelines.Analysis.OperatorInfo Analyze(Expression expression, AnalysisContext context);
    /// Determines parallelization opportunities for this operator.
    /// <returns>Parallelization opportunity information.</returns>
    ParallelizationOpportunity AnalyzeParallelization(Expression expression);
    /// Estimates the execution cost of this operator.
    /// <returns>Relative execution cost.</returns>
    double EstimateExecutionCost(Expression expression);
}
// TypeUsageInfo is defined in PipelineAnalysisTypes.cs to avoid duplication
/// Contains information about operator analysis.
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
/// Contains information about parallelization opportunities.
public record ParallelizationOpportunity
{
    /// <summary>Gets whether the operation is suitable for vectorization.</summary>
    public bool VectorizationSuitable { get; init; }
    /// <summary>Gets whether the operation supports parallel execution.</summary>
    public bool SupportsParallelExecution { get; init; }
    /// <summary>Gets the recommended parallelization degree.</summary>
    public int RecommendedParallelism { get; init; } = 1;
    /// <summary>Gets data dependencies that affect parallelization.</summary>
    public List<string> DataDependencies { get; init; } = [];
    /// <summary>Gets the estimated speedup from parallelization.</summary>
    public double EstimatedSpeedup { get; init; } = 1.0;
    /// <summary>Gets the parallelization potential score (0-1).</summary>
    public double ParallelizationPotential { get; init; }
    /// <summary>Gets whether this operation is suitable for parallelization.</summary>
    public bool IsSuitable => VectorizationSuitable || SupportsParallelExecution;
// DependencyInfo is defined in PipelineAnalysisTypes.cs to avoid duplication
/// Defines memory usage patterns for types.
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
/// Defines memory access patterns for operations.
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
    /// <summary>Coalesced access pattern (GPU optimized).</summary>
    Coalesced,
    /// <summary>Cache-friendly access pattern.</summary>
    CacheFriendly,
    /// <summary>Cache-unfriendly access pattern.</summary>
    CacheUnfriendly
}
