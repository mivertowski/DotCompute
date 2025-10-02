// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.Intrinsics.X86;

namespace DotCompute.Algorithms.Selection.Models;

/// <summary>
/// Hardware capabilities and performance characteristics for algorithm selection.
/// Provides static analysis of the current system's computational capabilities.
/// </summary>
public static class HardwareProfile
{
    // Hardware capability detection
    private static readonly bool _hasAvx2 = Avx2.IsSupported;
    private static readonly bool _hasFma = Fma.IsSupported;
    private static readonly bool _hasSse42 = Sse42.IsSupported;
    private static readonly int _coreCount = Environment.ProcessorCount;

    /// <summary>
    /// Gets whether the system supports vector instructions (SIMD).
    /// </summary>
    public static readonly bool HasVectorInstructions = _hasAvx2 || _hasSse42;

    /// <summary>
    /// Gets whether the system supports parallelism (multi-core).
    /// </summary>
    public static readonly bool SupportsParallelism = _coreCount > 1;

    /// <summary>
    /// Gets whether the system has high memory bandwidth.
    /// </summary>
    public static readonly bool HasHighMemoryBandwidth = DetectHighMemoryBandwidth();

    /// <summary>
    /// Gets the optimal thread count for compute-intensive tasks.
    /// </summary>
    public static readonly int OptimalThreadCount = CalculateOptimalThreadCount();

    /// <summary>
    /// Gets the estimated L1 cache size per core.
    /// </summary>
    public static readonly long L1CacheSize = GetCacheSize(CacheLevel.L1);

    /// <summary>
    /// Gets the estimated L2 cache size per core.
    /// </summary>
    public static readonly long L2CacheSize = GetCacheSize(CacheLevel.L2);

    /// <summary>
    /// Gets the estimated L3 cache size (shared).
    /// </summary>
    public static readonly long L3CacheSize = GetCacheSize(CacheLevel.L3);

    /// <summary>
    /// Gets whether AVX2 instructions are supported.
    /// </summary>
    public static bool HasAvx2 => _hasAvx2;

    /// <summary>
    /// Gets whether FMA (Fused Multiply-Add) instructions are supported.
    /// </summary>
    public static bool HasFma => _hasFma;

    /// <summary>
    /// Gets whether SSE 4.2 instructions are supported.
    /// </summary>
    public static bool HasSse42 => _hasSse42;

    /// <summary>
    /// Gets the number of logical processor cores.
    /// </summary>
    public static int CoreCount => _coreCount;
    /// <summary>
    /// An cache level enumeration.
    /// </summary>

    private enum CacheLevel { L1, L2, L3 }

    private static bool DetectHighMemoryBandwidth()
        // Simplified heuristic based on core count and architecture

        => _coreCount >= 8 && (_hasAvx2 || _hasFma);

    private static int CalculateOptimalThreadCount()
        // Conservative approach: use 75% of available cores for compute-intensive tasks

        => Math.Max(1, (int)(_coreCount * 0.75));

    private static long GetCacheSize(CacheLevel level)
    {
        // Platform-specific cache detection would go here
        // For now, use reasonable defaults based on modern CPUs
        return level switch
        {
            CacheLevel.L1 => 32 * 1024,      // 32KB
            CacheLevel.L2 => 256 * 1024,     // 256KB
            CacheLevel.L3 => 8 * 1024 * 1024, // 8MB
            _ => 0
        };
    }
}
