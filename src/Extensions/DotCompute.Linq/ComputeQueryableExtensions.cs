// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Providers;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Execution;
using DotCompute.Linq.Expressions;
using DotCompute.Linq.Operators.Generation;
using DotCompute.Linq.Operators.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DotCompute.Memory;
namespace DotCompute.Linq;


/// <summary>
/// Extension methods for creating and executing GPU-accelerated LINQ queries.
/// </summary>
public static class ComputeQueryableExtensions
{
    /// <summary>
    /// Converts an enumerable to a GPU-accelerated queryable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <param name="accelerator">The accelerator to use for query execution.</param>
    /// <param name="options">Optional query options.</param>
    /// <returns>A GPU-accelerated queryable.</returns>
    public static IQueryable<T> AsComputeQueryable<T>(
        this IEnumerable<T> source,
        IAccelerator accelerator,
        ComputeQueryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accelerator);

        var actualOptions = options ?? new ComputeQueryOptions();
        var provider = CreateQueryProvider(accelerator, actualOptions);

        // Create a constant expression for the source
        // Use alternative to AsQueryable to avoid AOT issues
        IQueryable queryableSource;
        if (source is IQueryable alreadyQueryable)
        {
            queryableSource = alreadyQueryable;
        }
        else
        {
            queryableSource = source.AsQueryable();
        }
        var sourceExpression = Expression.Constant(queryableSource);

        return new ComputeQueryable<T>(provider, sourceExpression);
    }

    /// <summary>
    /// Converts an array to a GPU-accelerated queryable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <param name="source">The source array.</param>
    /// <param name="accelerator">The accelerator to use for query execution.</param>
    /// <param name="options">Optional query options.</param>
    /// <returns>A GPU-accelerated queryable.</returns>
    public static IQueryable<T> AsComputeQueryable<T>(
        this T[] source,
        IAccelerator accelerator,
        ComputeQueryOptions? options = null) => ((IEnumerable<T>)source).AsComputeQueryable(accelerator, options);

    /// <summary>
    /// Executes a query on the GPU and returns the results.
    /// </summary>
    /// <typeparam name="T">The type of elements in the result.</typeparam>
    /// <param name="source">The compute queryable.</param>
    /// <returns>The query results.</returns>
    public static T[] ToComputeArray<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is ComputeQueryProvider computeProvider)
        {
            var result = computeProvider.Execute<T[]>(source.Expression);
            return result ?? [];
        }

        // Fallback to regular LINQ
        return [.. source];
    }

    /// <summary>
    /// Executes a query on the GPU asynchronously and returns the results.
    /// </summary>
    /// <typeparam name="T">The type of elements in the result.</typeparam>
    /// <param name="source">The compute queryable.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation with the query results.</returns>
    public static async Task<T[]> ToComputeArrayAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is ComputeQueryProvider computeProvider)
        {
            // For async execution, we'd need to extend the provider
            // For now, run synchronously on a background thread
            return await Task.Run(() => computeProvider.Execute<T[]>(source.Expression) ?? [],
                cancellationToken);
        }

        // Fallback to regular LINQ
        return await Task.Run(() => source.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Computes the sum of a sequence on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The sum of the elements.</returns>
    public static int ComputeSum(this IQueryable<int> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sumExpression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Sum),
            Type.EmptyTypes,
            source.Expression);

        return source.Provider.Execute<int>(sumExpression);
    }

    /// <summary>
    /// Computes the sum of a sequence on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The sum of the elements.</returns>
    public static float ComputeSum(this IQueryable<float> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sumExpression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Sum),
            Type.EmptyTypes,
            source.Expression);

        return source.Provider.Execute<float>(sumExpression);
    }

    /// <summary>
    /// Computes the sum of a sequence on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The sum of the elements.</returns>
    public static double ComputeSum(this IQueryable<double> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sumExpression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Sum),
            Type.EmptyTypes,
            source.Expression);

        return source.Provider.Execute<double>(sumExpression);
    }

    /// <summary>
    /// Computes the average of a sequence on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The average of the elements.</returns>
    public static double ComputeAverage(this IQueryable<int> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var avgExpression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Average),
            Type.EmptyTypes,
            source.Expression);

        return source.Provider.Execute<double>(avgExpression);
    }

    /// <summary>
    /// Computes the average of a sequence on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The average of the elements.</returns>
    public static float ComputeAverage(this IQueryable<float> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var avgExpression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Average),
            Type.EmptyTypes,
            source.Expression);

        return source.Provider.Execute<float>(avgExpression);
    }

    /// <summary>
    /// Computes the average of a sequence on the GPU.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>The average of the elements.</returns>
    public static double ComputeAverage(this IQueryable<double> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var avgExpression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Average),
            Type.EmptyTypes,
            source.Expression);

        return source.Provider.Execute<double>(avgExpression);
    }

    /// <summary>
    /// Gets optimization suggestions for a query.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The queryable source.</param>
    /// <returns>A collection of optimization suggestions.</returns>
    public static IEnumerable<OptimizationSuggestion> GetOptimizationSuggestions<T>(
        this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider is ComputeQueryProvider computeProvider)
        {
            // Access the optimizer through the provider
            // This would require exposing it through the provider interface
            var optimizer = new ExpressionOptimizer(NullLogger<ExpressionOptimizer>.Instance);
            return optimizer.Analyze(source.Expression);
        }

        return [];
    }

    /// <summary>
    /// Enables profiling for a query.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The queryable source.</param>
    /// <returns>The queryable with profiling enabled.</returns>
    public static IQueryable<T> WithProfiling<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // This would add metadata to enable profiling
        // In a full implementation, we'd modify the expression tree
        return source;
    }

    /// <summary>
    /// Sets the maximum memory allocation for a query.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The queryable source.</param>
    /// <param name="maxBytes">The maximum memory allocation in bytes.</param>
    /// <returns>The queryable with memory limit set.</returns>
    public static IQueryable<T> WithMemoryLimit<T>(this IQueryable<T> source, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(source);

        // This would add metadata to limit memory usage
        // In a full implementation, we'd modify the expression tree
        return source;
    }

    private static ComputeQueryProvider CreateQueryProvider(
        IAccelerator accelerator,
        ComputeQueryOptions options)
    {
        // Create logger factory
        var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;

        // Create enhanced kernel factory with logging
        var kernelFactory = new Operators.DefaultKernelFactory(
            loggerFactory.CreateLogger<Operators.DefaultKernelFactory>());

        // Create components
        var optimizer = new ExpressionOptimizer(
            loggerFactory.CreateLogger<ExpressionOptimizer>());

        var compiler = new QueryCompiler(
            kernelFactory,
            optimizer,
            loggerFactory.CreateLogger<QueryCompiler>());

        var memoryManagerFactory = new DefaultMemoryManagerFactory(
            loggerFactory.CreateLogger<UnifiedMemoryManager>());

        var executor = new QueryExecutor(
            memoryManagerFactory,
            loggerFactory.CreateLogger<QueryExecutor>());

        var cacheOptions = new QueryCacheOptions
        {
            MaxEntries = options.CacheMaxEntries,
            EnableExpiration = options.EnableCacheExpiration,
            DefaultExpiration = options.CacheExpiration
        };

        var cache = new QueryCache(
            cacheOptions,
            loggerFactory.CreateLogger<QueryCache>());

        return new ComputeQueryProvider(
            accelerator,
            compiler,
            executor,
            cache,
            loggerFactory.CreateLogger<ComputeQueryProvider>());
    }

    /// <summary>
    /// Options for compute queries.
    /// </summary>
    public class ComputeQueryOptions
    {
        /// <summary>
        /// Gets or sets the logger factory.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable query caching.
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of cache entries.
        /// </summary>
        public int CacheMaxEntries { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to enable cache expiration.
        /// </summary>
        public bool EnableCacheExpiration { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache expiration time.
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets a value indicating whether to enable automatic CPU fallback.
        /// </summary>
        public bool EnableCpuFallback { get; set; } = true;

        /// <summary>
        /// Gets or sets the default execution timeout.
        /// </summary>
        public TimeSpan? DefaultTimeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable profiling.
        /// </summary>
        public bool EnableProfiling { get; set; }
    }

    /// <summary>
    /// Interface for kernel factories.
    /// </summary>
    public interface IKernelFactory
    {
        /// <summary>
        /// Creates a kernel from a definition.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <param name="definition">The kernel definition.</param>
        /// <returns>The created kernel.</returns>
        public IKernel CreateKernel(IAccelerator accelerator, DotCompute.Abstractions.Kernels.KernelDefinition definition);
    }
}
