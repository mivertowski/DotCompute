// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;

namespace DotCompute.Linq.Interfaces;

/// <summary>
/// Interface for LINQ query providers that integrate with the DotCompute runtime.
/// </summary>
public interface IComputeLinqProvider
{
    /// <summary>
    /// Creates a queryable for the specified data source with GPU acceleration.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="source">The data source</param>
    /// <param name="accelerator">Optional specific accelerator to use</param>
    /// <returns>A GPU-accelerated queryable</returns>
    IQueryable<T> CreateQueryable<T>(IEnumerable<T> source, IAccelerator? accelerator = null);

    /// <summary>
    /// Creates a queryable for the specified array with GPU acceleration.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="source">The array source</param>
    /// <param name="accelerator">Optional specific accelerator to use</param>
    /// <returns>A GPU-accelerated queryable</returns>
    IQueryable<T> CreateQueryable<T>(T[] source, IAccelerator? accelerator = null);

    /// <summary>
    /// Executes a LINQ expression with GPU acceleration.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="expression">The expression to execute</param>
    /// <returns>The execution result</returns>
    Task<T> ExecuteAsync<T>(Expression expression);

    /// <summary>
    /// Executes a LINQ expression with GPU acceleration and optional accelerator preference.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="expression">The expression to execute</param>
    /// <param name="preferredAccelerator">Preferred accelerator for execution</param>
    /// <returns>The execution result</returns>
    Task<T> ExecuteAsync<T>(Expression expression, IAccelerator preferredAccelerator);

    /// <summary>
    /// Gets optimization suggestions for a LINQ expression.
    /// </summary>
    /// <param name="expression">The expression to analyze</param>
    /// <returns>Optimization suggestions</returns>
    IEnumerable<OptimizationSuggestion> GetOptimizationSuggestions(Expression expression);

    /// <summary>
    /// Validates if an expression can be executed on GPU.
    /// </summary>
    /// <param name="expression">The expression to validate</param>
    /// <returns>True if GPU-compatible, false otherwise</returns>
    bool IsGpuCompatible(Expression expression);

    /// <summary>
    /// Pre-compiles frequently used expressions for better performance.
    /// </summary>
    /// <param name="expressions">Expressions to pre-compile</param>
    /// <returns>A task representing the pre-compilation operation</returns>
    Task PrecompileExpressionsAsync(IEnumerable<Expression> expressions);
}

/// <summary>
/// Represents an optimization suggestion for LINQ queries.
/// </summary>
public class OptimizationSuggestion
{
    /// <summary>
    /// Gets or sets the suggestion category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the suggestion message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the severity level.
    /// </summary>
    public SuggestionSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the estimated performance impact.
    /// </summary>
    public double EstimatedImpact { get; set; }
}

/// <summary>
/// Severity levels for optimization suggestions.
/// </summary>
public enum SuggestionSeverity
{
    /// <summary>
    /// Information level suggestion.
    /// </summary>
    Info,

    /// <summary>
    /// Warning level suggestion.
    /// </summary>
    Warning,

    /// <summary>
    /// High priority suggestion.
    /// </summary>
    High,

    /// <summary>
    /// Critical optimization needed.
    /// </summary>
    Critical
}