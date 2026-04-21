// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Hardware;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Standard memory and cache sizes used by NUMA allocation helpers.
/// Hardware-physical values re-export <see cref="HardwareConstants"/> so there
/// is a single source of truth; HugePageSize stays local because 1 GB pages are
/// a NUMA-specific policy choice rather than a universal hardware constant.
/// </summary>
internal static class NumaSizes
{
    /// <summary>Standard cache line size (64 bytes on most x86 parts).</summary>
    public const int CacheLineSize = HardwareConstants.CacheLine.Legacy64;

    /// <summary>Standard page size.</summary>
    public const int PageSize = HardwareConstants.Alignment.Page;

    /// <summary>Large page size (2MB).</summary>
    public const int LargePageSize = HardwareConstants.Alignment.LargePage;

    /// <summary>Huge page size (1GB).</summary>
    public const int HugePageSize = 1024 * 1024 * 1024;
}
