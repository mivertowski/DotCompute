using System;
using DotCompute.Linq.Compilation;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub kernel cache for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 4.
/// </remarks>
public class KernelCacheStub : IKernelCache
{
    /// <inheritdoc/>
    public Delegate? GetCached(string key) => null;

    /// <inheritdoc/>
    public void Store(string key, Delegate compiled, TimeSpan ttl)
        => throw new NotImplementedException("Phase 4: CPU SIMD Code Generation");

    /// <inheritdoc/>
    public bool Remove(string key) => false;

    /// <inheritdoc/>
    public void Clear() { }
}
