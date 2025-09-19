// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections;
using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Linq.Compilation.Context;
using DotCompute.Linq.Compilation.Plans;
using DotCompute.Linq.Expressions;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
using DotCompute.Linq.Compilation;

namespace DotCompute.Linq.Providers;

/// <summary>
/// LINQ query provider that is fully integrated with the runtime orchestrator.
/// This is the primary provider for production use cases.
/// </summary>
public class IntegratedComputeQueryProvider : IQueryProvider
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly IExpressionOptimizer _optimizer;
    private readonly ILogger<IntegratedComputeQueryProvider> _logger;
    private readonly LinqToKernelTranslator _translator;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegratedComputeQueryProvider"/> class.
    /// </summary>
    /// <param name="orchestrator">The compute orchestrator for kernel execution.</param>
    /// <param name="optimizer">The expression optimizer.</param>
    /// <param name="logger">The logger instance.</param>
    public IntegratedComputeQueryProvider(
        IComputeOrchestrator orchestrator,
        IExpressionOptimizer optimizer,
        ILogger<IntegratedComputeQueryProvider> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _translator = new LinqToKernelTranslator(logger);
    }

    /// <summary>
    /// Gets the orchestrator associated with this provider.
    /// </summary>
    public IComputeOrchestrator Orchestrator => _orchestrator;

    /// <inheritdoc />
    public IQueryable CreateQuery(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var elementType = GetElementType(expression.Type);
        var queryableType = typeof(IntegratedComputeQueryable<>).MakeGenericType(elementType);


        try
        {
            return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to create query for expression of type {expression.Type}");
            throw new InvalidOperationException($"Failed to create query for expression of type {expression.Type}", ex);
        }
    }

    /// <inheritdoc />
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new IntegratedComputeQueryable<TElement>(this, expression);
    }

    /// <inheritdoc />
    public object? Execute(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);


        _logger.LogDebugMessage("Executing query expression via orchestrator");

        // Use async-over-sync pattern for synchronous interface
        return ExecuteAsync<object>(expression, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public TResult Execute<TResult>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ExecuteAsync<TResult>(expression, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes the query asynchronously using the runtime orchestrator.
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="expression">The expression to execute</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Optimize the expression

            var optimizedExpression = OptimizeExpression(expression);

            // Translate LINQ expression to kernel operations

            var kernelOperations = _translator.TranslateToKernelOperations(optimizedExpression);

            // Execute kernel operations sequentially

            object? result = null;
            foreach (var operation in kernelOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await ExecuteKernelOperationAsync(operation, null, cancellationToken);
            }

            // Convert result to expected type

            return await ConvertResultAsync<TResult>(result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to execute query expression via orchestrator");
            throw;
        }
    }


    /// <summary>
    /// Executes the query with a specific accelerator preference.
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="expression">The expression to execute</param>
    /// <param name="preferredBackend">The preferred backend name</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<TResult> ExecuteAsync<TResult>(Expression expression, string preferredBackend, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(preferredBackend);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Optimize the expression

            var optimizedExpression = OptimizeExpression(expression);

            // Translate LINQ expression to kernel operations

            var kernelOperations = _translator.TranslateToKernelOperations(optimizedExpression);

            // Execute kernel operations with preferred backend

            object? result = null;
            foreach (var operation in kernelOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await ExecuteKernelOperationAsync(operation, preferredBackend, cancellationToken);
            }


            return await ConvertResultAsync<TResult>(result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to execute query expression with preferred backend");
            throw;
        }
    }

    private Expression OptimizeExpression(Expression expression)
    {
        var options = new CompilationOptions
        {
            EnableOptimizations = true,
            UseSharedMemory = true,
            EnableCaching = true
        };


        return _optimizer.Optimize(expression, options);
    }

    private async Task<object?> ExecuteKernelOperationAsync(KernelOperation operation, string? preferredBackend = null, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();


            if (preferredBackend != null)
            {
                return await _orchestrator.ExecuteAsync<object>(operation.KernelName, preferredBackend, operation.Arguments, cancellationToken);
            }


            return await _orchestrator.ExecuteAsync<object>(operation.KernelName, operation.Arguments, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarningMessage("Kernel operation failed, attempting CPU fallback");

            // Attempt CPU fallback

            if (preferredBackend != "CPU")
            {
                try
                {
                    return await _orchestrator.ExecuteAsync<object>(operation.KernelName, "CPU", operation.Arguments, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Fallback failed, rethrow original exception
                }
            }


            throw new InvalidOperationException($"Failed to execute kernel operation: {operation.KernelName}", ex);
        }
    }

    private static async Task<TResult> ConvertResultAsync<TResult>(object? result, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();


        if (result is TResult directResult)
        {
            return directResult;
        }

        if (result == null && default(TResult) == null)
        {
            return default!;
        }

        // Handle array result conversions
        if (typeof(TResult).IsAssignableFrom(typeof(IEnumerable<>)) && result is Array array)
        {
            var elementType = typeof(TResult).GetGenericArguments().FirstOrDefault();
            if (elementType != null)
            {
                var convertedArray = Array.CreateInstance(elementType, array.Length);
                Array.Copy(array, convertedArray, array.Length);
                return (TResult)(object)convertedArray;
            }
        }

        throw new InvalidCastException($"Cannot convert query result of type {result?.GetType()} to {typeof(TResult)}");
    }

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
/// Represents a queryable sequence that uses the integrated compute orchestrator.
/// </summary>
/// <typeparam name="T">The type of the elements in the sequence.</typeparam>
public class IntegratedComputeQueryable<T> : IOrderedQueryable<T>
{
    private readonly IntegratedComputeQueryProvider _provider;
    private readonly Expression _expression;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegratedComputeQueryable{T}"/> class.
    /// </summary>
    /// <param name="provider">The query provider.</param>
    /// <param name="expression">The expression tree.</param>
    public IntegratedComputeQueryable(IntegratedComputeQueryProvider provider, Expression expression)
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
    /// Initializes a new instance for a data source.
    /// </summary>
    /// <param name="provider">The query provider.</param>
    /// <param name="source">The data source.</param>
    public IntegratedComputeQueryable(IntegratedComputeQueryProvider provider, IEnumerable<T> source)
        : this(provider, Expression.Constant(source.AsQueryable(), typeof(IQueryable<T>)))
    {
    }

    /// <inheritdoc />
    public Type ElementType => typeof(T);

    /// <inheritdoc />
    public Expression Expression => _expression;

    /// <inheritdoc />
    public IQueryProvider Provider => _provider;

    /// <summary>
    /// Gets the orchestrator associated with this queryable.
    /// </summary>
    internal IComputeOrchestrator Orchestrator => _provider.Orchestrator;

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        var result = _provider.Execute<IEnumerable<T>>(_expression);
        return result.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Executes the query asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The query result</returns>
    public async Task<IEnumerable<T>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _provider.ExecuteAsync<IEnumerable<T>>(_expression, cancellationToken);
    }

    /// <summary>
    /// Executes the query asynchronously with a preferred backend.
    /// </summary>
    /// <param name="preferredBackend">The preferred backend</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The query result</returns>
    public async Task<IEnumerable<T>> ExecuteAsync(string preferredBackend, CancellationToken cancellationToken = default)
    {
        return await _provider.ExecuteAsync<IEnumerable<T>>(_expression, preferredBackend, cancellationToken);
    }
}

/// <summary>
/// Represents a kernel operation translated from a LINQ expression.
/// </summary>
public class KernelOperation
{
    /// <summary>
    /// Gets or sets the kernel name to execute.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments for the kernel.
    /// </summary>
    public object[] Arguments { get; set; } = Array.Empty<object>();

    /// <summary>
    /// Gets or sets the expected result type.
    /// </summary>
    public Type ResultType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets metadata about the operation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Translates LINQ expressions to kernel operations.
/// </summary>
public class LinqToKernelTranslator
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinqToKernelTranslator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public LinqToKernelTranslator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Translates a LINQ expression to a sequence of kernel operations.
    /// </summary>
    /// <param name="expression">The expression to translate</param>
    /// <returns>A sequence of kernel operations</returns>
    public IEnumerable<KernelOperation> TranslateToKernelOperations(Expression expression)
    {
        var translator = new ExpressionToKernelVisitor(_logger);
        return translator.Translate(expression);
    }

    /// <summary>
    /// Visitor that converts LINQ expressions to kernel operations.
    /// </summary>
    private class ExpressionToKernelVisitor : ExpressionVisitor
    {
        private readonly ILogger _logger;
        private readonly List<KernelOperation> _operations = [];
        private int _operationCounter = 0;

        public ExpressionToKernelVisitor(ILogger logger)
        {
            _logger = logger;
        }

        public List<KernelOperation> Translate(Expression expression)
        {
            Visit(expression);
            return _operations;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsLinqMethod(node))
            {
                _logger.LogDebugMessage($"Translating LINQ method: {node.Method.Name}");

                switch (node.Method.Name)
                {
                    case "Select":
                        TranslateSelect(node);
                        break;
                    case "Where":
                        TranslateWhere(node);
                        break;
                    case "Sum":
                    case "Average":
                    case "Min":
                    case "Max":
                    case "Count":
                        TranslateAggregate(node);
                        break;
                    case "OrderBy":
                    case "OrderByDescending":
                        TranslateOrderBy(node);
                        break;
                    default:
                        _logger.LogWarningMessage($"Unsupported LINQ method: {node.Method.Name}");
                        break;
                }
            }

            return base.VisitMethodCall(node);
        }

        private void TranslateSelect(MethodCallExpression node)
        {
            var operation = new KernelOperation
            {
                KernelName = $"System.Linq.Select_{_operationCounter++}",
                Arguments = ExtractArguments(node),
                ResultType = node.Type,
                Metadata = new Dictionary<string, object>
                {
                    ["Operation"] = "Select",
                    ["ElementType"] = node.Type.GetGenericArguments().FirstOrDefault() ?? typeof(object)
                }
            };

            _operations.Add(operation);
        }

        private void TranslateWhere(MethodCallExpression node)
        {
            var operation = new KernelOperation
            {
                KernelName = $"System.Linq.Where_{_operationCounter++}",
                Arguments = ExtractArguments(node),
                ResultType = node.Type,
                Metadata = new Dictionary<string, object>
                {
                    ["Operation"] = "Where",
                    ["ElementType"] = node.Type.GetGenericArguments().FirstOrDefault() ?? typeof(object)
                }
            };

            _operations.Add(operation);
        }

        private void TranslateAggregate(MethodCallExpression node)
        {
            var operation = new KernelOperation
            {
                KernelName = $"System.Linq.{node.Method.Name}_{_operationCounter++}",
                Arguments = ExtractArguments(node),
                ResultType = node.Type,
                Metadata = new Dictionary<string, object>
                {
                    ["Operation"] = node.Method.Name,
                    ["AggregationType"] = node.Method.Name
                }
            };

            _operations.Add(operation);
        }

        private void TranslateOrderBy(MethodCallExpression node)
        {
            var operation = new KernelOperation
            {
                KernelName = $"System.Linq.{node.Method.Name}_{_operationCounter++}",
                Arguments = ExtractArguments(node),
                ResultType = node.Type,
                Metadata = new Dictionary<string, object>
                {
                    ["Operation"] = node.Method.Name,
                    ["SortDirection"] = node.Method.Name.Contains("Descending") ? "Descending" : "Ascending"
                }
            };

            _operations.Add(operation);
        }

        private static object[] ExtractArguments(MethodCallExpression node)
        {
            var args = new List<object>();

            foreach (var argument in node.Arguments)
            {
                if (argument is ConstantExpression constant)
                {
                    args.Add(constant.Value!);
                }
                else if (argument is UnaryExpression unary && unary.Operand is LambdaExpression lambda)
                {
                    args.Add(lambda);
                }
                else if (argument is LambdaExpression directLambda)
                {
                    args.Add(directLambda);
                }
                else
                {
                    // For more complex expressions, we might need to evaluate or convert them
                    args.Add(argument);
                }
            }

            return args.ToArray();
        }

        private static bool IsLinqMethod(MethodCallExpression node)
        {
            return node.Method.DeclaringType == typeof(Queryable) ||
                   node.Method.DeclaringType == typeof(Enumerable);
        }
    }
}