using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotCompute.Linq;

/// <summary>
/// Provides LINQ extensions for compute operations
/// </summary>
public static class ComputeQueryableExtensions
{
    /// <summary>
    /// Converts an IQueryable to a compute-enabled queryable
    /// </summary>
    public static IQueryable<T> AsComputeQueryable<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ComputeQueryable<T>(source.Expression, new ComputeQueryProvider());
    }

    /// <summary>
    /// Executes a compute operation on GPU if available, otherwise falls back to CPU
    /// </summary>
    public static T[] ToComputeArray<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // For now, just execute on CPU - this is a minimal implementation
        return source.ToArray();
    }

    /// <summary>
    /// Maps elements using compute acceleration
    /// </summary>
    public static IQueryable<TResult> ComputeSelect<TSource, TResult>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        return source.Select(selector);
    }

    /// <summary>
    /// Filters elements using compute acceleration
    /// </summary>
    public static IQueryable<T> ComputeWhere<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return source.Where(predicate);
    }
}

/// <summary>
/// Minimal compute queryable implementation
/// </summary>
internal class ComputeQueryable<T> : IQueryable<T>
{
    public ComputeQueryable(Expression expression, IQueryProvider provider)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public Type ElementType => typeof(T);
    public Expression Expression { get; }
    public IQueryProvider Provider { get; }

    public IEnumerator<T> GetEnumerator() => Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Minimal compute query provider implementation
/// </summary>
internal sealed class ComputeQueryProvider : IQueryProvider
{
    [UnconditionalSuppressMessage("Trimming", "IL3051", Justification = "IQueryProvider interface from framework cannot be annotated")]
    [RequiresDynamicCode("Creating generic types at runtime requires dynamic code generation.")]
    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? expression.Type;
        var queryableType = typeof(ComputeQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, expression, this)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new ComputeQueryable<TElement>(expression, this);
    }

    [UnconditionalSuppressMessage("Trimming", "IL3051", Justification = "IQueryProvider interface from framework cannot be annotated")]
    [RequiresDynamicCode("Expression compilation requires dynamic code generation.")]
    public object? Execute(Expression expression)
    {
        // For minimal implementation, just compile and execute the expression
        var lambda = Expression.Lambda(expression);
        return lambda.Compile().DynamicInvoke();
    }

    [UnconditionalSuppressMessage("Trimming", "IL3051", Justification = "IQueryProvider interface from framework cannot be annotated")]
    [RequiresDynamicCode("Expression compilation requires dynamic code generation.")]
    public TResult Execute<TResult>(Expression expression)
    {
        var result = Execute(expression);
        return result is TResult typed ? typed : (TResult)result!;
    }
}