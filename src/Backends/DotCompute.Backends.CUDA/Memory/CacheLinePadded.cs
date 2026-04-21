// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// Cache-line padded wrappers for atomic counters in lock-free queues.
/// </summary>
/// <remarks>
/// <para>
/// Two counters that share a cache line ping-pong the line between cores on every
/// access — false sharing — invalidating the entire L1/L2 line. RustCompute's
/// SpscQueue measured a 22–28% throughput improvement after padding producer and
/// consumer counters onto separate cache lines.
/// </para>
/// <para>
/// Padding is set to 128 bytes to cover modern x86 (Zen 4) and NVIDIA Hopper L2
/// line widths. On older 64-byte-line CPUs this is a one-line waste per counter,
/// which is negligible.
/// </para>
/// </remarks>
public static class CacheLine
{
    /// <summary>Padding size in bytes (128 covers modern Zen and Hopper L2).</summary>
    public const int Size = 128;
}

/// <summary>
/// A 64-bit counter padded to its own cache line to prevent false sharing.
/// Pass <c>ref counter.Value</c> to <c>Interlocked.*</c> APIs.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = CacheLine.Size * 2)]
public struct PaddedLong
{
    /// <summary>The 64-bit value, placed at offset Cache.Size to ensure no neighbour shares its line.</summary>
    [FieldOffset(CacheLine.Size)]
    public long Value;
}

/// <summary>
/// A 32-bit counter padded to its own cache line.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = CacheLine.Size * 2)]
public struct PaddedInt
{
    /// <summary>The 32-bit value, placed at offset CacheLine.Size to ensure no neighbour shares its line.</summary>
    [FieldOffset(CacheLine.Size)]
    public int Value;
}
