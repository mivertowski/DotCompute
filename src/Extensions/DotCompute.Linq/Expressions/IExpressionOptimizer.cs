// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
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
    public Expression Optimize(Expression expression, DotCompute.Linq.Compilation.CompilationOptions options);
    /// Analyzes an expression tree and returns optimization suggestions.
    /// <param name="expression">The expression to analyze.</param>
    /// <returns>A collection of optimization suggestions.</returns>
    public IEnumerable<OptimizationSuggestion> Analyze(Expression expression);
}
/// Represents an optimization suggestion.
public class OptimizationSuggestion
    /// Initializes a new instance of the <see cref="OptimizationSuggestion"/> class.
    /// <param name="type">The type of optimization.</param>
    /// <param name="description">The description of the optimization.</param>
    /// <param name="impact">The expected performance impact.</param>
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
    /// Gets the type of optimization.
    public OptimizationType Type { get; }
    /// Gets the description of the optimization.
    public string Description { get; }
    /// Gets the expected performance impact.
    public PerformanceImpact Impact { get; }
    /// Gets the expression to optimize.
    public Expression? Expression { get; }
/// Types of optimizations that can be applied.
public enum OptimizationType
    /// Combine multiple operations into a single kernel.
    OperatorFusion,
    /// Optimize memory access patterns.
    MemoryCoalescing,
    /// Remove redundant operations.
    RedundancyElimination,
    /// Reorder operations for better performance.
    OperationReordering,
    /// Replace operations with more efficient alternatives.
    OperationSubstitution,
    /// Optimize data layout for GPU access.
    DataLayoutOptimization,
    /// Vectorize scalar operations.
    Vectorization,
    /// Optimize loop structures.
    LoopOptimization
/// Expected performance impact of an optimization.
public enum PerformanceImpact
    /// Minor performance improvement (less than 10%).
    Low,
    /// Moderate performance improvement (10-50%).
    Medium,
    /// Significant performance improvement (greater than 50%).
    High,
    /// Critical for performance.
    Critical
