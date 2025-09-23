// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Kernels.Simd;

/// <summary>
/// Memory prefetching utilities for SIMD operations.
/// Provides cross-platform memory prefetching to improve cache performance.
/// </summary>
public static class SimdMemoryPrefetcher
{
    /// <summary>
    /// Performs memory prefetching for upcoming data access.
    /// </summary>
    /// <param name="address">Memory address to prefetch.</param>
    /// <param name="mode">Prefetch mode (temporal or non-temporal).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Prefetch(void* address, PrefetchMode mode)
    {
        if (Sse.IsSupported)
        {
            switch (mode)
            {
                case PrefetchMode.Temporal:
                    Sse.Prefetch0(address);
                    break;
                case PrefetchMode.NonTemporal:
                    Sse.PrefetchNonTemporal(address);
                    break;
            }
        }
        // No-op on platforms without prefetch support
    }
}

/// <summary>
/// Prefetch modes for memory access optimization.
/// </summary>
public enum PrefetchMode
{
    /// <summary>
    /// Temporal prefetch - expects data to be reused soon.
    /// </summary>
    Temporal,

    /// <summary>
    /// Non-temporal prefetch - data will be used once and not cached.
    /// </summary>
    NonTemporal
}