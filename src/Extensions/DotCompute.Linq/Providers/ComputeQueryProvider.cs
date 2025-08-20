// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections;
using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Execution;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Providers;


/// <summary>
/// LINQ query provider that enables GPU-accelerated query execution.
/// </summary>
public class ComputeQueryProvider : IQueryProvider
{
private readonly IAccelerator _accelerator;
private readonly IQueryCompiler _compiler;
private readonly IQueryExecutor _executor;
private readonly IQueryCache _cache;
private readonly ILogger<ComputeQueryProvider> _logger;

/// <summary>
/// Initializes a new instance of the <see cref="ComputeQueryProvider"/> class.
/// </summary>
/// <param name="accelerator">The accelerator to use for query execution.</param>
/// <param name="compiler">The query expression compiler.</param>
/// <param name="executor">The query executor.</param>
/// <param name="cache">The query cache.</param>
/// <param name="logger">The logger instance.</param>
public ComputeQueryProvider(
    IAccelerator accelerator,
    IQueryCompiler compiler,
    IQueryExecutor executor,
    IQueryCache cache,
    ILogger<ComputeQueryProvider> logger)
{
    _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}

/// <summary>
/// Creates a new query with the specified expression.
/// </summary>
/// <param name="expression">The expression tree representing the query.</param>
/// <returns>An IQueryable representing the query.</returns>
public IQueryable CreateQuery(Expression expression)
{
    ArgumentNullException.ThrowIfNull(expression);

    var elementType = GetElementType(expression.Type);
    
    try
    {
        var queryableType = typeof(ComputeQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create query for expression of type {ExpressionType}", expression.Type);
        throw new InvalidOperationException($"Failed to create query for expression of type {expression.Type}", ex);
    }
}

/// <summary>
/// Creates a new strongly-typed query with the specified expression.
/// </summary>
/// <typeparam name="TElement">The type of the elements in the query.</typeparam>
/// <param name="expression">The expression tree representing the query.</param>
/// <returns>An IQueryable&lt;TElement&gt; representing the query.</returns>
public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
{
    ArgumentNullException.ThrowIfNull(expression);
    return new ComputeQueryable<TElement>(this, expression);
}

/// <summary>
/// Executes the query represented by the expression tree.
/// </summary>
/// <param name="expression">The expression tree to execute.</param>
/// <returns>The result of the query execution.</returns>
public object? Execute(Expression expression)
{
    ArgumentNullException.ThrowIfNull(expression);

    _logger.LogDebug("Executing query expression of type {ExpressionType}", expression.Type);

    // Check cache first
    var cacheKey = _cache.GenerateKey(expression);
    if (_cache.TryGet(cacheKey, out var cachedResult))
    {
        _logger.LogDebug("Query result found in cache");
        return cachedResult;
    }

    try
    {
        // Compile the expression to a compute plan
        var compilationContext = new CompilationContext(_accelerator, expression);
        var computePlan = _compiler.Compile(compilationContext);

        // Execute the compute plan
        var executionContext = new Execution.ExecutionContext(_accelerator, computePlan);
        var result = _executor.Execute(executionContext);

        // Cache the result
        _cache.Set(cacheKey, result);

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to execute query expression");
        throw new InvalidOperationException("Failed to execute query expression", ex);
    }
}

/// <summary>
/// Executes the strongly-typed query represented by the expression tree.
/// </summary>
/// <typeparam name="TResult">The type of the query result.</typeparam>
/// <param name="expression">The expression tree to execute.</param>
/// <returns>The result of the query execution.</returns>
public TResult Execute<TResult>(Expression expression)
{
    var result = Execute(expression);
    
    if (result is TResult typedResult)
    {
        return typedResult;
    }

    if (result == null && default(TResult) == null)
    {
        return default!;
    }

    throw new InvalidCastException($"Cannot cast query result of type {result?.GetType()} to {typeof(TResult)}");
}

/// <summary>
/// Gets the element type from a queryable type.
/// </summary>
private static Type GetElementType(Type type)
{
    // Check if it's already IQueryable<T>
    var queryableType = type.GetInterfaces()
        .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>));
    
    if (queryableType != null)
    {
        return queryableType.GetGenericArguments()[0];
    }

    // Check if it's IEnumerable<T>
    var enumerableType = type.GetInterfaces()
        .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    
    if (enumerableType != null)
    {
        return enumerableType.GetGenericArguments()[0];
    }

    // If type implements IEnumerable, return object
    if (typeof(IEnumerable).IsAssignableFrom(type))
    {
        return typeof(object);
    }

    throw new ArgumentException($"Cannot determine element type for {type}");
}
}

/// <summary>
/// Represents a queryable sequence that can be executed on a compute accelerator.
/// </summary>
/// <typeparam name="T">The type of the elements in the sequence.</typeparam>
public class ComputeQueryable<T> : IOrderedQueryable<T>
{
private readonly ComputeQueryProvider _provider;
private readonly Expression _expression;

/// <summary>
/// Initializes a new instance of the <see cref="ComputeQueryable{T}"/> class.
/// </summary>
/// <param name="provider">The query provider.</param>
/// <param name="expression">The expression tree.</param>
public ComputeQueryable(ComputeQueryProvider provider, Expression expression)
{
    _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    _expression = expression ?? throw new ArgumentNullException(nameof(expression));

    // Validate that expression type is compatible
    if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
    {
        throw new ArgumentException($"Expression type {expression.Type} is not assignable to IQueryable<{typeof(T)}>");
    }
}

/// <summary>
/// Gets the type of the elements in the sequence.
/// </summary>
public Type ElementType => typeof(T);

/// <summary>
/// Gets the expression tree associated with this instance.
/// </summary>
public Expression Expression => _expression;

/// <summary>
/// Gets the query provider associated with this data source.
/// </summary>
public IQueryProvider Provider => _provider;

/// <summary>
/// Returns an enumerator that iterates through the collection.
/// </summary>
/// <returns>An enumerator for the collection.</returns>
public IEnumerator<T> GetEnumerator()
{
    var result = _provider.Execute<IEnumerable<T>>(_expression);
    return result.GetEnumerator();
}

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An IEnumerator object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
