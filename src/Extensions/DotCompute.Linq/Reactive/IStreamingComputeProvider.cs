using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotCompute.Linq.Reactive;

/// <summary>
/// Interface for streaming compute with reactive extensions integration.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 7: Reactive Extensions Integration.
/// Enables GPU-accelerated streaming compute with backpressure handling.
/// </remarks>
public interface IStreamingComputeProvider
{
    /// <summary>
    /// Applies a transformation to each element in a stream using GPU acceleration.
    /// </summary>
    /// <typeparam name="T">The type of elements in the source stream.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result stream.</typeparam>
    /// <param name="source">The source observable stream.</param>
    /// <param name="transformation">The transformation expression to apply.</param>
    /// <returns>An observable stream of transformed elements.</returns>
    public IObservable<TResult> Compute<T, TResult>(
        IObservable<T> source,
        Expression<Func<T, TResult>> transformation);

    /// <summary>
    /// Applies a batch transformation to groups of elements in a stream.
    /// </summary>
    /// <typeparam name="T">The type of elements in the source stream.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result stream.</typeparam>
    /// <param name="source">The source observable stream.</param>
    /// <param name="batchTransform">The batch transformation expression to apply.</param>
    /// <returns>An observable stream of transformed elements.</returns>
    public IObservable<TResult> ComputeBatch<T, TResult>(
        IObservable<T> source,
        Expression<Func<IEnumerable<T>, IEnumerable<TResult>>> batchTransform);
}
