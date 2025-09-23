// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Information about huge pages support on a NUMA node.
/// </summary>
public sealed class HugePagesInfo
{
    /// <summary>
    /// Gets the supported huge page sizes.
    /// </summary>
    public required IReadOnlyList<HugePageSize> SupportedSizes { get; init; }

    /// <summary>
    /// Gets whether huge pages are supported on this node.
    /// </summary>
    public bool IsSupported => SupportedSizes.Count > 0;

    /// <summary>
    /// Gets the largest available huge page size.
    /// </summary>
    public long LargestPageSize => SupportedSizes.Count > 0 ? SupportedSizes.Max(s => s.SizeInBytes) : 0;

    /// <summary>
    /// Gets the total free huge page memory.
    /// </summary>
    public long TotalFreeMemory => SupportedSizes.Sum(s => s.FreeMemoryBytes);

    /// <summary>
    /// Gets the total huge page memory.
    /// </summary>
    public long TotalMemory => SupportedSizes.Sum(s => s.TotalMemoryBytes);
}

/// <summary>
/// Information about a specific huge page size.
/// </summary>
public sealed class HugePageSize
{
    /// <summary>
    /// Gets the page size in bytes.
    /// </summary>
    public required long SizeInBytes { get; init; }

    /// <summary>
    /// Gets the number of free pages of this size.
    /// </summary>
    public required int FreePages { get; init; }

    /// <summary>
    /// Gets the total number of pages of this size.
    /// </summary>
    public required int TotalPages { get; init; }

    /// <summary>
    /// Gets the total free memory in bytes for this page size.
    /// </summary>
    public long FreeMemoryBytes => FreePages * SizeInBytes;

    /// <summary>
    /// Gets the total memory in bytes for this page size.
    /// </summary>
    public long TotalMemoryBytes => TotalPages * SizeInBytes;

    /// <summary>
    /// Gets the utilization percentage.
    /// </summary>
    public double UtilizationPercentage => TotalPages > 0 ? (double)(TotalPages - FreePages) / TotalPages * 100.0 : 0.0;
}

/// <summary>
/// Information about cache hierarchy on a NUMA node.
/// </summary>
public sealed class CacheHierarchy
{
    /// <summary>
    /// Gets the cache levels.
    /// </summary>
    public required IReadOnlyList<CacheLevel> Levels { get; init; }

    /// <summary>
    /// Gets a specific cache level.
    /// </summary>
    /// <param name="level">The cache level to retrieve.</param>
    /// <returns>Cache level information or null if not found.</returns>
    public CacheLevel? GetLevel(int level) => Levels.FirstOrDefault(l => l.Level == level);

    /// <summary>
    /// Gets the total cache size across all levels.
    /// </summary>
    public long TotalCacheSize => Levels.Sum(l => l.SizeInBytes);

    /// <summary>
    /// Gets the L1 cache size.
    /// </summary>
    public long L1CacheSize => GetLevel(1)?.SizeInBytes ?? 0;

    /// <summary>
    /// Gets the L2 cache size.
    /// </summary>
    public long L2CacheSize => GetLevel(2)?.SizeInBytes ?? 0;

    /// <summary>
    /// Gets the L3 cache size.
    /// </summary>
    public long L3CacheSize => GetLevel(3)?.SizeInBytes ?? 0;

    /// <summary>
    /// Gets the maximum cache level.
    /// </summary>
    public int MaxCacheLevel => Levels.Count > 0 ? Levels.Max(l => l.Level) : 0;
}

/// <summary>
/// Information about a specific cache level.
/// </summary>
public sealed class CacheLevel
{
    /// <summary>
    /// Gets the cache level (1, 2, 3, etc.).
    /// </summary>
    public required int Level { get; init; }

    /// <summary>
    /// Gets the cache size in bytes.
    /// </summary>
    public required long SizeInBytes { get; init; }

    /// <summary>
    /// Gets the cache type (Data, Instruction, Unified).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the cache line size in bytes.
    /// </summary>
    public required int LineSize { get; init; }

    /// <summary>
    /// Gets the associativity of the cache.
    /// </summary>
    public int Associativity { get; init; }

    /// <summary>
    /// Gets the cache latency in cycles (if available).
    /// </summary>
    public int LatencyCycles { get; init; }

    /// <summary>
    /// Gets whether this is a shared cache across multiple cores.
    /// </summary>
    public bool IsShared { get; init; }

    /// <summary>
    /// Gets the cache efficiency score (0.0 to 1.0).
    /// </summary>
    public double EfficiencyScore
    {
        get
        {
            // Simple heuristic based on size and latency
            if (LatencyCycles == 0)
            {
                return 0.8; // Default for unknown latency
            }


            return Level switch
            {
                1 => Math.Max(0.0, 1.0 - (LatencyCycles / 10.0)), // L1 should be very fast
                2 => Math.Max(0.0, 1.0 - (LatencyCycles / 50.0)), // L2 moderate latency
                3 => Math.Max(0.0, 1.0 - (LatencyCycles / 100.0)), // L3 higher latency acceptable
                _ => 0.5 // Unknown level
            };
        }
    }
}

/// <summary>
/// CPU performance characteristics for NUMA awareness.
/// </summary>
public sealed class CpuPerformanceInfo
{
    /// <summary>
    /// Gets the CPU frequency in MHz.
    /// </summary>
    public required double FrequencyMhz { get; init; }

    /// <summary>
    /// Gets the number of cores.
    /// </summary>
    public required int CoreCount { get; init; }

    /// <summary>
    /// Gets the number of logical processors.
    /// </summary>
    public required int LogicalProcessorCount { get; init; }

    /// <summary>
    /// Gets whether hyper-threading is enabled.
    /// </summary>
    public bool HyperThreadingEnabled => LogicalProcessorCount > CoreCount;

    /// <summary>
    /// Gets the threads per core ratio.
    /// </summary>
    public double ThreadsPerCore => CoreCount > 0 ? (double)LogicalProcessorCount / CoreCount : 1.0;

    /// <summary>
    /// Gets the CPU architecture.
    /// </summary>
    public string? Architecture { get; init; }

    /// <summary>
    /// Gets the CPU model name.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Gets the CPU vendor.
    /// </summary>
    public string? Vendor { get; init; }

    /// <summary>
    /// Gets the CPU stepping.
    /// </summary>
    public int Stepping { get; init; }

    /// <summary>
    /// Gets the CPU family.
    /// </summary>
    public int Family { get; init; }

    /// <summary>
    /// Gets the CPU model.
    /// </summary>
    public int Model { get; init; }

    /// <summary>
    /// Gets the performance rating (0.0 to 1.0).
    /// </summary>
    public double PerformanceRating
    {
        get
        {
            // Simple heuristic based on frequency and core count
            var baseScore = Math.Min(FrequencyMhz / 4000.0, 1.0); // 4GHz as reference
            var coreBonus = Math.Min(CoreCount / 32.0, 0.5); // Up to 32 cores bonus
            return Math.Min(baseScore + coreBonus, 1.0);
        }
    }
}

/// <summary>
/// Utility methods for CPU and NUMA operations.
/// </summary>
public static class CpuUtilities
{
    /// <summary>
    /// Counts the number of set bits in a processor mask.
    /// </summary>
    /// <param name="mask">The processor mask.</param>
    /// <returns>Number of set bits.</returns>
    public static int CountSetBits(ulong mask)
    {
        var count = 0;
        while (mask != 0)
        {
            count++;
            mask &= mask - 1; // Clear the lowest set bit
        }
        return count;
    }

    /// <summary>
    /// Creates a CPU mask for a range of processors.
    /// </summary>
    /// <param name="startCpu">Start CPU index.</param>
    /// <param name="endCpu">End CPU index.</param>
    /// <returns>CPU mask with specified range set.</returns>
    public static ulong CreateCpuMask(int startCpu, int endCpu)
    {
        ulong mask = 0;
        for (var i = startCpu; i <= endCpu && i < NumaConstants.Limits.MaxCpusInMask; i++)
        {
            mask |= (1UL << i);
        }
        return mask;
    }

    /// <summary>
    /// Parses a CPU list string (e.g., "0-3,8-11") into a mask and count.
    /// </summary>
    /// <param name="cpuList">CPU list string.</param>
    /// <returns>Tuple of CPU mask and count.</returns>
    public static (ulong mask, int count) ParseCpuList(string cpuList)
    {
        ulong mask = 0;
        var count = 0;

        if (string.IsNullOrWhiteSpace(cpuList))
        {
            return (mask, count);
        }

        var parts = cpuList.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Contains('-', StringComparison.Ordinal))
            {
                var range = trimmed.Split('-');
                if (range.Length == 2 &&
                    int.TryParse(range[0], out var start) &&
                    int.TryParse(range[1], out var end))
                {
                    for (var i = start; i <= end && i < NumaConstants.Limits.MaxCpusInMask; i++)
                    {
                        mask |= (1UL << i);
                        count++;
                    }
                }
            }
            else if (int.TryParse(trimmed, out var cpu) && cpu < NumaConstants.Limits.MaxCpusInMask)
            {
                mask |= (1UL << cpu);
                count++;
            }
        }

        return (mask, count);
    }

    /// <summary>
    /// Parses a CPU mask string (e.g., "00000000,00000fff") into a mask and count.
    /// </summary>
    /// <param name="cpuMap">CPU mask string.</param>
    /// <param name="cpuMask">Parsed CPU mask.</param>
    /// <param name="cpuCount">Number of CPUs in mask.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool ParseCpuMask(string cpuMap, out ulong cpuMask, out int cpuCount)
    {
        cpuMask = 0;
        cpuCount = 0;

        try
        {
            // CPU mask is typically in hex format like "00000000,00000fff"
            var cleanMask = cpuMap.Replace(",", "", StringComparison.Ordinal)
                                  .Replace(" ", "", StringComparison.Ordinal)
                                  .Replace("0x", "", StringComparison.OrdinalIgnoreCase);

            // Parse as hex and convert to CPU mask
            if (ulong.TryParse(cleanMask, System.Globalization.NumberStyles.HexNumber, null, out cpuMask))
            {
                cpuCount = CountSetBits(cpuMask);
                return cpuCount > 0;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return false;
    }

    /// <summary>
    /// Gets the first CPU ID from a CPU list string.
    /// </summary>
    /// <param name="cpuList">CPU list string.</param>
    /// <returns>First CPU ID or -1 if not found.</returns>
    public static int ParseFirstCpu(string cpuList)
    {
        if (string.IsNullOrWhiteSpace(cpuList))
        {
            return -1;
        }

        var parts = cpuList.Split(',');
        if (parts.Length > 0)
        {
            var firstPart = parts[0].Trim();
            if (firstPart.Contains('-', StringComparison.Ordinal))
            {
                var range = firstPart.Split('-');
                if (int.TryParse(range[0], out var start))
                {
                    return start;
                }
            }
            else if (int.TryParse(firstPart, out var cpu))
            {
                return cpu;
            }
        }
        return -1;
    }

    /// <summary>
    /// Estimates the optimal number of threads for a workload on a given node.
    /// </summary>
    /// <param name="node">NUMA node information.</param>
    /// <param name="workloadType">Type of workload (CPU-bound, I/O-bound, etc.).</param>
    /// <returns>Optimal thread count.</returns>
    public static int EstimateOptimalThreadCount(NumaNode node, WorkloadType workloadType)
    {
        return workloadType switch
        {
            WorkloadType.CpuBound => node.ProcessorCount, // One thread per core for CPU-bound
            WorkloadType.IoBound => node.ProcessorCount * 2, // More threads for I/O-bound
            WorkloadType.Mixed => (int)(node.ProcessorCount * 1.5), // Balanced approach
            WorkloadType.MemoryBound => Math.Max(1, node.ProcessorCount / 2), // Fewer threads for memory-bound
            _ => node.ProcessorCount
        };
    }
}

/// <summary>
/// Types of workloads for optimization purposes.
/// </summary>
public enum WorkloadType
{
    /// <summary>CPU-intensive workload.</summary>
    CpuBound,

    /// <summary>I/O-intensive workload.</summary>
    IoBound,

    /// <summary>Memory-intensive workload.</summary>
    MemoryBound,

    /// <summary>Mixed workload characteristics.</summary>
    Mixed,

    /// <summary>Unknown workload characteristics.</summary>
    Unknown
}