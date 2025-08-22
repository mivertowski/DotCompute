// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Providers;

namespace DotCompute.Linq.Queryables;

/// <summary>
/// Simple queryable implementation.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public class SimpleQueryable<T> : IOrderedQueryable<T>
{
    private readonly SimpleLINQProvider _provider;
    private readonly Expression _expression;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleQueryable{T}"/> class.
    /// </summary>
    /// <param name="provider">The query provider.</param>
    /// <param name="expression">The expression.</param>
    public SimpleQueryable(SimpleLINQProvider provider, Expression expression)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    /// <inheritdoc/>
    public Type ElementType => typeof(T);

    /// <inheritdoc/>
    public Expression Expression => _expression;

    /// <inheritdoc/>
    public IQueryProvider Provider => _provider;

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        var result = _provider.Execute<IEnumerable<T>>(_expression);
        return result.GetEnumerator();
    }

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}