// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Linq;

/// <summary>
/// Extension methods for creating GPU-accelerated LINQ queries using the simple provider.
/// </summary>
public static class SimpleQueryableExtensions
{
    /// <summary>
    /// Converts an enumerable to a GPU-accelerated queryable.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <returns>A GPU-accelerated queryable.</returns>
    public static IQueryable<T> AsGpuQueryable<T>(this IEnumerable<T> source, IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accelerator);

        var logger = NullLogger<SimpleLINQProvider>.Instance;
        var provider = new SimpleLINQProvider(accelerator, logger);
        
        var expression = System.Linq.Expressions.Expression.Constant(source.AsQueryable());
        return new SimpleQueryable<T>(provider, expression);
    }

    /// <summary>
    /// Executes a GPU query and returns the results as an array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The queryable source.</param>
    /// <returns>The results as an array.</returns>
    public static T[] ToGpuArray<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return [.. source];
    }

    /// <summary>
    /// Computes the sum of integers on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The sum of the elements.</returns>
    public static int GpuSum(this IQueryable<int> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Sum();
    }

    /// <summary>
    /// Computes the sum of floats on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The sum of the elements.</returns>
    public static float GpuSum(this IQueryable<float> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Sum();
    }

    /// <summary>
    /// Computes the sum of doubles on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The sum of the elements.</returns>
    public static double GpuSum(this IQueryable<double> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Sum();
    }

    /// <summary>
    /// Computes the average on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The average of the elements.</returns>
    public static double GpuAverage(this IQueryable<int> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Average();
    }

    /// <summary>
    /// Computes the average on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The average of the elements.</returns>
    public static float GpuAverage(this IQueryable<float> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Average();
    }

    /// <summary>
    /// Computes the average on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The average of the elements.</returns>
    public static double GpuAverage(this IQueryable<double> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Average();
    }
}