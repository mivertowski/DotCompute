// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Platform-specific limits.
/// </summary>
internal static class NumaLimits
{
    /// <summary>Maximum CPUs in a single mask (ulong limit).</summary>
    public const int MaxCpusInMask = 64;

    /// <summary>Maximum processors per NUMA node estimate.</summary>
    public const int MaxProcessorsPerNode = 128;

    /// <summary>Maximum NUMA nodes supported.</summary>
    public const int MaxNumaNodes = 256;
}
