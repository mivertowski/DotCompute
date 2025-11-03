using System;
using DotCompute.Linq.Reactive;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub batch processor for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 7.
/// </remarks>
public class BatchProcessorStub : IBatchProcessor
{
    /// <inheritdoc/>
    public IObservable<TResult> BatchProcess<T, TResult>(
        IObservable<T> source,
        int minBatchSize,
        int maxBatchSize,
        TimeSpan timeout)
        => throw new NotImplementedException("Phase 7: Reactive Extensions Integration");
}
