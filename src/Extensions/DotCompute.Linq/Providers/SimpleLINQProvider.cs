// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Linq.Queryables;
using Microsoft.Extensions.Logging;
namespace DotCompute.Linq.Providers;
/// <summary>
/// Simplified LINQ provider implementation for initial testing.
/// </summary>
public class SimpleLINQProvider : IQueryProvider
{
    private readonly IAccelerator _accelerator;
    private readonly ILogger<SimpleLINQProvider> _logger;
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleLINQProvider"/> class.
    /// </summary>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <param name="logger">The logger.</param>
    public SimpleLINQProvider(IAccelerator accelerator, ILogger<SimpleLINQProvider> logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    /// <inheritdoc/>
    public IQueryable CreateQuery(Expression expression)
        ArgumentNullException.ThrowIfNull(expression);
        var elementType = GetElementType(expression.Type);
        var queryableType = typeof(SimpleQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        return new SimpleQueryable<TElement>(this, expression);
    public object? Execute(Expression expression)
        // Skip logging to avoid CA1848/CA2254
        // For now, compile and execute on CPU
        var lambda = Expression.Lambda(expression);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    public TResult Execute<TResult>(Expression expression)
        var result = Execute(expression);
        return (TResult)result!;
    private static Type GetElementType(Type type)
        var queryableType = type.GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>));
        if (queryableType != null)
        {
            return queryableType.GetGenericArguments()[0];
        }
        if (typeof(IQueryable).IsAssignableFrom(type))
            return typeof(object);
        throw new ArgumentException($"Cannot determine element type for {type}");
}
