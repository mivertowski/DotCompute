// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Standard memory and cache sizes.
/// </summary>
internal static class NumaSizes
{
    /// <summary>Standard cache line size.</summary>
    public const int CacheLineSize = 64;

    /// <summary>Standard page size.</summary>
    public const int PageSize = 4096;

    /// <summary>Large page size (2MB).</summary>
    public const int LargePageSize = 2 * 1024 * 1024;

    /// <summary>Huge page size (1GB).</summary>
    public const int HugePageSize = 1024 * 1024 * 1024;
}
