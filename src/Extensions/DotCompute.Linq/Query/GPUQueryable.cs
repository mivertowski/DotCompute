// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
namespace DotCompute.Linq.Query;
{
/// <summary>
/// GPU-accelerated queryable implementation.
/// </summary>
/// <typeparam name="T">The type of elements in the queryable.</typeparam>
/// <remarks>
/// This class provides a GPU-accelerated implementation of IOrderedQueryable{T}
/// that works with the GPULINQProvider to execute LINQ queries on the GPU.
/// It maintains the expression tree and delegates execution to the provider.
/// </remarks>
public sealed class GPUQueryable<T> : IOrderedQueryable<T>
{
    private readonly GPULINQProvider _provider;
    private readonly Expression _expression;
    /// <summary>
    /// Initializes a new instance of the <see cref="GPUQueryable{T}"/> class.
    /// </summary>
    /// <param name="provider">The GPU LINQ provider.</param>
    /// <param name="expression">The expression tree representing the query.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="provider"/> or <paramref name="expression"/> is null.
    /// </exception>
    public GPUQueryable(GPULINQProvider provider, Expression expression)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
    /// Gets the type of the elements in the queryable.
    /// <value>The type of elements this queryable contains.</value>
    public Type ElementType => typeof(T);
    /// Gets the expression tree representing the query.
    /// <value>The expression tree that will be executed to produce results.</value>
    public Expression Expression => _expression;
    /// Gets the query provider that can execute this queryable.
    /// <value>The GPU LINQ provider instance.</value>
    public IQueryProvider Provider => _provider;
    /// Returns an enumerator that iterates through the query results.
    /// <returns>An enumerator that can be used to iterate through the results.</returns>
    /// <remarks>
    /// This method triggers execution of the query on the GPU and returns
    /// the results as an enumerable sequence.
    /// </remarks>
    public IEnumerator<T> GetEnumerator()
        {
        var result = _provider.Execute<IEnumerable<T>>(_expression);
        return result.GetEnumerator();
    /// Returns a non-generic enumerator that iterates through the query results.
    /// <returns>A non-generic enumerator for the results.</returns>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
