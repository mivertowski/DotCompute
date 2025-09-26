// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Analysis of kernel memory access patterns.
/// </summary>
public class MemoryAnalysisReport
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public List<MemoryAccessPattern> AccessPatterns { get; init; } = [];
    public List<PerformanceOptimization> Optimizations { get; init; } = [];
    public long TotalMemoryAccessed { get; init; }
    public float MemoryEfficiency { get; init; }
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// Allocation efficiency score (0-1).
    /// </summary>
    public double AllocationEfficiency { get; init; }

    /// <summary>
    /// Probability of memory leaks (0-1).
    /// </summary>
    public double LeakProbability { get; init; }

    /// <summary>
    /// Cache efficiency score (0-100).
    /// </summary>
    public double CacheEfficiency { get; init; }
}

/// <summary>
/// Memory access pattern information.
/// </summary>
public class MemoryAccessPattern
{
    public string PatternType { get; init; } = string.Empty;
    public long AccessCount { get; init; }
    public float CoalescingEfficiency { get; init; }
    public List<string> Issues { get; init; } = [];
}

/// <summary>
/// Performance optimization suggestion.
/// </summary>
public class PerformanceOptimization
{
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public float PotentialSpeedup { get; init; }
    public string Implementation { get; init; } = string.Empty;
}
