using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DotCompute.Linq.Reactive;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub streaming compute provider for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 7.
/// </remarks>
public class StreamingComputeProviderStub : IStreamingComputeProvider
{
    /// <inheritdoc/>
    public IObservable<TResult> Compute<T, TResult>(
        IObservable<T> source,
        Expression<Func<T, TResult>> transformation)
        => throw new NotImplementedException("Phase 7: Reactive Extensions Integration");

    /// <inheritdoc/>
    public IObservable<TResult> ComputeBatch<T, TResult>(
        IObservable<T> source,
        Expression<Func<IEnumerable<T>, IEnumerable<TResult>>> batchTransform)
        => throw new NotImplementedException("Phase 7: Reactive Extensions Integration");
}
