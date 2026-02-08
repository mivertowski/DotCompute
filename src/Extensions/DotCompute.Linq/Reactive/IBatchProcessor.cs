using System;

namespace DotCompute.Linq.Reactive;

/// <summary>
/// Interface for batch processing in streaming compute scenarios.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 7: Reactive Extensions Integration.
/// Batching improves GPU efficiency by amortizing kernel launch overhead.
/// </remarks>
public interface IBatchProcessor
{
    /// <summary>
    /// Processes elements in batches with adaptive sizing based on throughput.
    /// </summary>
    /// <typeparam name="T">The type of elements in the source stream.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result stream.</typeparam>
    /// <param name="source">The source observable stream.</param>
    /// <param name="minBatchSize">The minimum number of elements per batch.</param>
    /// <param name="maxBatchSize">The maximum number of elements per batch.</param>
    /// <param name="timeout">The maximum time to wait for a full batch.</param>
    /// <returns>An observable stream of batch processing results.</returns>
    public IObservable<TResult> BatchProcess<T, TResult>(
        IObservable<T> source,
        int minBatchSize,
        int maxBatchSize,
        TimeSpan timeout);
}
