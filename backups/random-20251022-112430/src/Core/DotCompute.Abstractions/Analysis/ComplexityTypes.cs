// <copyright file="ComplexityTypes.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Analysis;

/// <summary>
/// Defines computational complexity classes (Big O notation).
/// </summary>
public enum ComplexityClass
{
    /// <summary>O(1) - Constant time.</summary>
    Constant,

    /// <summary>O(log n) - Logarithmic time.</summary>
    Logarithmic,

    /// <summary>O(n) - Linear time.</summary>
    Linear,

    /// <summary>O(n log n) - Linearithmic time.</summary>
    Linearithmic,

    /// <summary>O(n²) - Quadratic time.</summary>
    Quadratic,

    /// <summary>O(n³) - Cubic time.</summary>
    Cubic,

    /// <summary>O(2^n) - Exponential time.</summary>
    Exponential,

    /// <summary>O(n!) - Factorial time.</summary>
    Factorial
}

/// <summary>
/// Types of memory access patterns.
/// </summary>
public enum MemoryAccessPattern
{
    /// <summary>Sequential access pattern.</summary>
    Sequential,

    /// <summary>Random access pattern.</summary>
    Random,

    /// <summary>Strided access pattern.</summary>
    Strided,

    /// <summary>Coalesced access pattern (GPU).</summary>
    Coalesced,

    /// <summary>Broadcast access pattern.</summary>
    Broadcast,

    /// <summary>Gather/scatter access pattern.</summary>
    GatherScatter
}

/// <summary>
/// Represents memory access complexity for specific patterns.
/// </summary>
public record MemoryAccessComplexity
{
    /// <summary>Gets the access pattern type.</summary>
    public MemoryAccessPattern Pattern { get; init; }

    /// <summary>Gets the frequency of this pattern.</summary>
    public int Frequency { get; init; }

    /// <summary>Gets the complexity impact factor.</summary>
    public double ComplexityImpact { get; init; }

    /// <summary>Gets the memory region size.</summary>
    public long RegionSize { get; init; }

    /// <summary>Gets the stride size for strided access.</summary>
    public int StrideSize { get; init; } = 1;
}

/// <summary>
/// Represents a memory region accessed in complexity analysis.
/// </summary>
public record MemoryRegion
{
    /// <summary>Gets the base address offset.</summary>
    public long BaseOffset { get; init; }

    /// <summary>Gets the size of the region in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Gets the access alignment.</summary>
    public int Alignment { get; init; } = 1;

    /// <summary>Gets whether the region is read-only.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>Gets the region type.</summary>
    public MemoryRegionType Type { get; init; }
}

/// <summary>
/// Types of memory regions.
/// </summary>
public enum MemoryRegionType
{
    /// <summary>Global memory region.</summary>
    Global,

    /// <summary>Shared memory region.</summary>
    Shared,

    /// <summary>Local memory region.</summary>
    Local,

    /// <summary>Constant memory region.</summary>
    Constant,

    /// <summary>Texture memory region.</summary>
    Texture
}

/// <summary>
/// Represents a memory access hotspot.
/// </summary>
public record MemoryHotspot
{
    /// <summary>Gets the location of the hotspot.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>Gets the access frequency.</summary>
    public int AccessFrequency { get; init; }

    /// <summary>Gets the memory region accessed.</summary>
    public MemoryRegion Region { get; init; } = new();

    /// <summary>Gets the hotspot intensity (0.0 to 1.0).</summary>
    public double Intensity { get; init; }

    /// <summary>Gets optimization recommendations.</summary>
    public IReadOnlyList<string> OptimizationRecommendations { get; init; } = [];
}

/// <summary>
/// Represents a memory access conflict.
/// </summary>
public record MemoryConflict
{
    /// <summary>Gets the conflicting access locations.</summary>
    public IReadOnlyList<string> ConflictingLocations { get; init; } = [];

    /// <summary>Gets the conflict type.</summary>
    public ConflictType Type { get; init; }

    /// <summary>Gets the severity of the conflict (0.0 to 1.0).</summary>
    public double Severity { get; init; }

    /// <summary>Gets the performance impact estimate.</summary>
    public double PerformanceImpact { get; init; }

    /// <summary>Gets suggested resolutions.</summary>
    public IReadOnlyList<string> Resolutions { get; init; } = [];
}

/// <summary>
/// Defines types of memory conflicts.
/// </summary>
public enum ConflictType
{
    /// <summary>Bank conflict in shared memory.</summary>
    BankConflict,

    /// <summary>Cache line conflict.</summary>
    CacheLineConflict,

    /// <summary>False sharing between threads.</summary>
    FalseSharing,

    /// <summary>Memory coalescing conflict.</summary>
    CoalescingConflict,

    /// <summary>Write-after-read dependency.</summary>
    WriteAfterRead,

    /// <summary>Read-after-write dependency.</summary>
    ReadAfterWrite
}

/// <summary>
/// Memory location information for access pattern analysis.
/// </summary>
public record MemoryLocation
{
    /// <summary>Gets the memory address offset.</summary>
    public long Offset { get; init; }

    /// <summary>Gets the size of the access.</summary>
    public int Size { get; init; }

    /// <summary>Gets the access frequency.</summary>
    public int AccessFrequency { get; init; }

    /// <summary>Gets whether this is a read or write access.</summary>
    public bool IsWrite { get; init; }

    /// <summary>Gets the memory region type.</summary>
    public MemoryRegionType RegionType { get; init; } = MemoryRegionType.Global;
}
