using System;
using DotCompute.Linq.Reactive;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub backpressure manager for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 7.
/// </remarks>
public class BackpressureManagerStub : IBackpressureManager
{
    /// <inheritdoc/>
    public void ApplyBackpressure(BackpressureStrategy strategy)
        => throw new NotImplementedException("Phase 7: Reactive Extensions Integration");

    /// <inheritdoc/>
    public BackpressureState GetState()
        => throw new NotImplementedException("Phase 7: Reactive Extensions Integration");
}
