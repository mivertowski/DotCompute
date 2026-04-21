// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// Nested public types are intentional here: they group physically-distinct domains
// (cache, alignment, CUDA, CPU cache, memory pool) under one discoverable static
// class so callers can write HardwareConstants.Cuda.WarpSize without disambiguation.
#pragma warning disable CA1034 // Nested types should not be visible

namespace DotCompute.Abstractions.Hardware;

/// <summary>
/// Hardware-physical constants used across backends. Single source of truth for
/// values that are dictated by physical hardware (cache geometry, GPU alignment
/// requirements, warp/block limits, etc.) rather than by application policy.
/// </summary>
/// <remarks>
/// Values in this class MUST NOT vary across builds. If a value is tunable at
/// runtime or differs between devices (e.g. per-SKU VRAM size, per-device warp
/// count), it belongs in a device-info type, not here. Keep this class small and
/// frozen — add new entries only when the same literal appears in three or more
/// places.
/// </remarks>
public static class HardwareConstants
{
    /// <summary>
    /// Cache-line-size constants. <see cref="Bytes"/> covers modern Zen4 and
    /// NVIDIA Hopper L2 (128 B). <see cref="Legacy64"/> is the 64 B value that
    /// most x86 parts (Intel/AMD pre-Zen4) and older CPUs use; migrated callers
    /// that assumed 64 B should reference <see cref="Legacy64"/> explicitly.
    /// </summary>
    public static class CacheLine
    {
        /// <summary>Cache line size for modern architectures (128 bytes).</summary>
        public const int Bytes = 128;

        /// <summary>Legacy 64-byte cache line used by most x86 parts through Zen3.</summary>
        public const int Legacy64 = 64;
    }

    /// <summary>
    /// Memory alignment constants for GPU / SIMD operations.
    /// </summary>
    public static class Alignment
    {
        /// <summary>Minimum alignment for coalesced GPU access (CUDA, Metal).</summary>
        public const int Gpu = 256;

        /// <summary>AVX-512 vector alignment (64 bytes).</summary>
        public const int SimdAvx512 = 64;

        /// <summary>AVX2 vector alignment (32 bytes).</summary>
        public const int SimdAvx2 = 32;

        /// <summary>Standard memory page size (4 KB).</summary>
        public const int Page = 4096;

        /// <summary>Large memory page size (2 MB).</summary>
        public const int LargePage = 2 * 1024 * 1024;
    }

    /// <summary>
    /// CUDA thread / block / warp limits that are fixed by the CUDA architecture
    /// (not per-device). Per-device values (SM count, L2 capacity, etc.) belong
    /// on <see cref="DotCompute.Abstractions.AcceleratorInfo"/>.
    /// </summary>
    public static class Cuda
    {
        /// <summary>Maximum threads per CUDA block (all compute capabilities 2.0+).</summary>
        public const int MaxThreadsPerBlock = 1024;

        /// <summary>Warp size for NVIDIA GPUs.</summary>
        public const int WarpSize = 32;
    }

    /// <summary>
    /// Typical CPU cache sizes used for tiling heuristics. These are rough
    /// defaults; when available, runtime-queried cache sizes should be preferred.
    /// </summary>
    public static class CpuCache
    {
        /// <summary>Typical L1 data cache size per core (32 KB).</summary>
        public const int L1Bytes = 32 * 1024;

        /// <summary>Typical L2 cache size per core (256 KB).</summary>
        public const int L2Bytes = 256 * 1024;

        /// <summary>Typical L3 cache budget per core (2 MB). Total L3 scales with core count.</summary>
        public const int L3BytesPerCore = 2 * 1024 * 1024;
    }

    /// <summary>
    /// Memory-pool size class boundaries for power-of-two pool allocators.
    /// </summary>
    public static class MemoryPool
    {
        /// <summary>Smallest pool size class (256 bytes).</summary>
        public const int MinClassBytes = 256;

        /// <summary>Largest pool size class (256 MB).</summary>
        public const int MaxClassBytes = 256 * 1024 * 1024;

        /// <summary>Growth factor between consecutive size classes (powers of two).</summary>
        public const int SizeClassMultiplier = 2;
    }
}
