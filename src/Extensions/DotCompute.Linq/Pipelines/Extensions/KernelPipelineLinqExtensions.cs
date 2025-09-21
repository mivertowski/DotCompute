// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Pipelines.Interfaces;
namespace DotCompute.Linq.Pipelines.Extensions
{
    /// <summary>
    /// LINQ extension methods for IKernelPipeline that provide synchronous access to async enumerable operations.
    /// </summary>
    public static class KernelPipelineLinqExtensions
    {
        /// <summary>
        /// Determines whether the pipeline contains any elements.
        /// </summary>
        /// <param name="pipeline">The pipeline to check</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the pipeline contains any elements; otherwise, false</returns>
        public static bool Any(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            
            return AnyAsync(pipeline, cancellationToken).GetAwaiter().GetResult();
        }
        /// Determines whether the pipeline contains any elements that satisfy a condition.
        /// <param name="predicate">A function to test each element for a condition</param>
        /// <returns>True if any element satisfies the condition; otherwise, false</returns>
        public static bool Any(this IKernelPipeline pipeline, Func<object, bool> predicate, CancellationToken cancellationToken = default)
            ArgumentNullException.ThrowIfNull(predicate);
            return AnyAsync(pipeline, predicate, cancellationToken).GetAwaiter().GetResult();
        /// Asynchronously determines whether the pipeline contains any elements.
        /// <returns>Task that represents the asynchronous operation. The task result is true if the pipeline contains any elements; otherwise, false</returns>
        public static async Task<bool> AnyAsync(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
            await using var enumerator = pipeline.GetAsyncEnumerator(cancellationToken);
            return await enumerator.MoveNextAsync();
        /// Asynchronously determines whether the pipeline contains any elements that satisfy a condition.
        /// <returns>Task that represents the asynchronous operation. The task result is true if any element satisfies the condition; otherwise, false</returns>
        public static async Task<bool> AnyAsync(this IKernelPipeline pipeline, Func<object, bool> predicate, CancellationToken cancellationToken = default)
            while (await enumerator.MoveNextAsync())
            {
                if (predicate(enumerator.Current))
                {
                    return true;
                }
            }
            return false;
        /// Returns the number of elements in the pipeline.
        /// <param name="pipeline">The pipeline to count</param>
        /// <returns>The number of elements in the pipeline</returns>
        public static int Count(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
            return CountAsync(pipeline, cancellationToken).GetAwaiter().GetResult();
        /// Asynchronously returns the number of elements in the pipeline.
        /// <returns>Task that represents the asynchronous operation. The task result is the number of elements in the pipeline</returns>
        public static async Task<int> CountAsync(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
            var count = 0;
                count++;
            return count;
        /// Converts the pipeline to a list by enumerating all elements.
        /// <param name="pipeline">The pipeline to convert</param>
        /// <returns>A list containing all elements from the pipeline</returns>
        public static List<object> ToList(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
            return ToListAsync(pipeline, cancellationToken).GetAwaiter().GetResult();
        /// Asynchronously converts the pipeline to a list by enumerating all elements.
        /// <returns>Task that represents the asynchronous operation. The task result is a list containing all elements from the pipeline</returns>
        public static async Task<List<object>> ToListAsync(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
            var list = new List<object>();
                list.Add(enumerator.Current);
            return list;
        /// Converts the pipeline to an array by enumerating all elements.
        /// <returns>An array containing all elements from the pipeline</returns>
        public static object[] ToArray(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
            return ToList(pipeline, cancellationToken).ToArray();
        /// Asynchronously converts the pipeline to an array by enumerating all elements.
        /// <returns>Task that represents the asynchronous operation. The task result is an array containing all elements from the pipeline</returns>
        public static async Task<object[]> ToArrayAsync(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
            var list = await ToListAsync(pipeline, cancellationToken);
            return list.ToArray();
    }
}
