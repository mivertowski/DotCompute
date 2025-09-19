// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Abstractions.Interfaces.Linq;

/// <summary>
/// Interface for LINQ query providers that integrate with the DotCompute runtime.
/// Provides the core abstraction for executing LINQ expressions with GPU acceleration,
/// automatic backend selection, and comprehensive optimization capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This interface represents the primary integration point between LINQ expressions
/// and the DotCompute execution runtime. Implementations handle expression tree analysis,
/// kernel generation, backend selection, and execution coordination.
/// </para>
/// <para>
/// Key Responsibilities:
/// - Convert LINQ expressions to executable compute kernels
/// - Manage accelerator lifecycle and resource allocation
/// - Provide performance analysis and optimization recommendations
/// - Handle error scenarios and automatic fallback strategies
/// </para>
/// <para>
/// Thread Safety: Implementations must be thread-safe and support concurrent
/// query execution across multiple threads.
/// </para>
/// </remarks>
public interface IComputeLinqProvider
{
    /// <summary>
    /// Creates a queryable for the specified data source with GPU acceleration.
    /// This method wraps the source data in a compute-enabled queryable that can
    /// execute LINQ operations using available compute accelerators.
    /// </summary>
    /// <typeparam name="T">The element type. Value types provide better GPU performance.</typeparam>
    /// <param name="source">The data source enumerable to be processed.</param>
    /// <param name="accelerator">Optional specific accelerator to use. If null,
    /// the provider will automatically select the optimal accelerator.</param>
    /// <returns>A GPU-accelerated queryable that supports standard LINQ operations
    /// with automatic kernel compilation and execution.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the element type is not
    /// supported by the available accelerators.</exception>
    /// <remarks>
    /// <para>Performance: Data transfer to GPU memory occurs lazily during query execution.
    /// For repeated queries on the same data, consider using array-based overloads.</para>
    /// <para>Memory: Large enumerables will be materialized into arrays for GPU processing,
    /// which may impact memory usage for very large datasets.</para>
    /// </remarks>
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
    /// Executes a LINQ expression with GPU acceleration and automatic optimization.
    /// This method analyzes the expression tree, generates appropriate compute kernels,
    /// selects the optimal backend, and executes the computation.
    /// </summary>
    /// <typeparam name="T">The result type of the expression execution.</typeparam>
    /// <param name="expression">The LINQ expression tree to execute. Must be a valid
    /// queryable expression that can be converted to compute kernels.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous execution, containing the
    /// computed result of the expression.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the expression contains operations
    /// not supported by any available backend.</exception>
    /// <exception cref="InvalidOperationException">Thrown when kernel compilation fails.</exception>
    /// <exception cref="OutOfMemoryException">Thrown when insufficient GPU memory is available.</exception>
    /// <remarks>
    /// <para>Backend Selection: The method automatically analyzes the expression to determine
    /// the optimal execution backend (CUDA, CPU SIMD, etc.) based on operation types,
    /// data size, and available hardware.</para>
    /// <para>Optimization: Expressions are automatically optimized for the target backend,
    /// including operation reordering, kernel fusion, and memory layout optimization.</para>
    /// </remarks>
    Task<T> ExecuteAsync<T>(Expression expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a LINQ expression with GPU acceleration and optional accelerator preference.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="expression">The expression to execute</param>
    /// <param name="preferredAccelerator">Preferred accelerator for execution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    Task<T> ExecuteAsync<T>(Expression expression, IAccelerator preferredAccelerator, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a LINQ expression and provides optimization suggestions to improve
    /// performance, memory efficiency, and GPU compatibility.
    /// </summary>
    /// <param name="expression">The LINQ expression tree to analyze for optimization opportunities.</param>
    /// <returns>An enumerable collection of optimization suggestions, ordered by
    /// potential performance impact from highest to lowest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is null.</exception>
    /// <remarks>
    /// <para>Analysis Areas:
    /// - Operation ordering for optimal data locality
    /// - Kernel fusion opportunities for reduced memory transfers
    /// - Data type optimization for improved GPU performance
    /// - Memory access pattern analysis
    /// - Backend compatibility assessment
    /// </para>
    /// <para>Performance: Analysis is performed on the expression tree only and does not
    /// require data access or kernel compilation, making it very fast.</para>
    /// <para>Usage: These suggestions can be used to guide manual query optimization
    /// or to inform automatic optimization strategies.</para>
    /// </remarks>
    IEnumerable<OptimizationSuggestion> GetOptimizationSuggestions(Expression expression);

    /// <summary>
    /// Validates if an expression can be executed on GPU.
    /// </summary>
    /// <param name="expression">The expression to validate</param>
    /// <returns>True if GPU-compatible, false otherwise</returns>
    bool IsGpuCompatible(Expression expression);

    /// <summary>
    /// Pre-compiles frequently used expressions for better runtime performance.
    /// This method performs kernel compilation and caching ahead of time to eliminate
    /// compilation overhead during actual query execution.
    /// </summary>
    /// <param name="expressions">Collection of LINQ expressions to pre-compile.
    /// Should include expressions that will be executed multiple times.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that represents the asynchronous pre-compilation operation.
    /// Completion indicates all kernels have been compiled and cached.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expressions"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when one or more expressions
    /// cannot be compiled for any available backend.</exception>
    /// <remarks>
    /// <para>Performance Benefits: Pre-compilation can reduce first-execution latency
    /// by 50-90% for complex expressions, as kernel compilation occurs during
    /// application startup rather than during query execution.</para>
    /// <para>Caching: Compiled kernels are automatically cached and reused for identical
    /// expressions, providing performance benefits for repeated query patterns.</para>
    /// <para>Resource Usage: Pre-compilation consumes CPU and memory resources upfront
    /// but reduces resource usage during query execution.</para>
    /// </remarks>
    Task PrecompileExpressionsAsync(IEnumerable<Expression> expressions, CancellationToken cancellationToken = default);
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