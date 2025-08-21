// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Compilation;

namespace DotCompute.Linq.Expressions;


/// <summary>
/// Defines the interface for optimizing LINQ expression trees for GPU execution.
/// </summary>
public interface IExpressionOptimizer
{
    /// <summary>
    /// Optimizes an expression tree for GPU execution.
    /// </summary>
    /// <param name="expression">The expression to optimize.</param>
    /// <param name="options">The compilation options.</param>
    /// <returns>The optimized expression.</returns>
    public Expression Optimize(Expression expression, CompilationOptions options);

    /// <summary>
    /// Analyzes an expression tree and returns optimization suggestions.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <returns>A collection of optimization suggestions.</returns>
    public IEnumerable<OptimizationSuggestion> Analyze(Expression expression);
}

/// <summary>
/// Represents an optimization suggestion.
/// </summary>
public class OptimizationSuggestion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizationSuggestion"/> class.
    /// </summary>
    /// <param name="type">The type of optimization.</param>
    /// <param name="description">The description of the optimization.</param>
    /// <param name="impact">The expected performance impact.</param>
    /// <param name="expression">The expression to optimize.</param>
    public OptimizationSuggestion(
        OptimizationType type,
        string description,
        PerformanceImpact impact,
        Expression? expression = null)
    {
        Type = type;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Impact = impact;
        Expression = expression;
    }

    /// <summary>
    /// Gets the type of optimization.
    /// </summary>
    public OptimizationType Type { get; }

    /// <summary>
    /// Gets the description of the optimization.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the expected performance impact.
    /// </summary>
    public PerformanceImpact Impact { get; }

    /// <summary>
    /// Gets the expression to optimize.
    /// </summary>
    public Expression? Expression { get; }
}

/// <summary>
/// Types of optimizations that can be applied.
/// </summary>
public enum OptimizationType
{
    /// <summary>
    /// Combine multiple operations into a single kernel.
    /// </summary>
    OperatorFusion,

    /// <summary>
    /// Optimize memory access patterns.
    /// </summary>
    MemoryCoalescing,

    /// <summary>
    /// Remove redundant operations.
    /// </summary>
    RedundancyElimination,

    /// <summary>
    /// Reorder operations for better performance.
    /// </summary>
    OperationReordering,

    /// <summary>
    /// Replace operations with more efficient alternatives.
    /// </summary>
    OperationSubstitution,

    /// <summary>
    /// Optimize data layout for GPU access.
    /// </summary>
    DataLayoutOptimization,

    /// <summary>
    /// Vectorize scalar operations.
    /// </summary>
    Vectorization,

    /// <summary>
    /// Optimize loop structures.
    /// </summary>
    LoopOptimization
}

/// <summary>
/// Expected performance impact of an optimization.
/// </summary>
public enum PerformanceImpact
{
    /// <summary>
    /// Minor performance improvement (less than 10%).
    /// </summary>
    Low,

    /// <summary>
    /// Moderate performance improvement (10-50%).
    /// </summary>
    Medium,

    /// <summary>
    /// Significant performance improvement (greater than 50%).
    /// </summary>
    High,

    /// <summary>
    /// Critical for performance.
    /// </summary>
    Critical
}
