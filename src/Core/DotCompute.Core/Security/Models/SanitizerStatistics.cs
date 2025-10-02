// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Core.Security.Enums;

namespace DotCompute.Core.Security.Models;

/// <summary>
/// Memory sanitizer statistics and metrics.
/// Tracks allocation patterns, violations, and performance metrics.
/// </summary>
public sealed class SanitizerStatistics
{
    /// <summary>
    /// The total allocations.
    /// </summary>
    /// <summary>
    /// Gets or sets the total number of allocations performed.
    /// </summary>
    public long TotalAllocations;
    /// <summary>
    /// The total deallocations.
    /// </summary>

    /// <summary>
    /// Gets or sets the total number of deallocations performed.
    /// </summary>
    public long TotalDeallocations;
    /// <summary>
    /// The total bytes allocated.
    /// </summary>

    /// <summary>
    /// Gets or sets the total bytes allocated.
    /// </summary>
    public long TotalBytesAllocated;
    /// <summary>
    /// The total bytes freed.
    /// </summary>

    /// <summary>
    /// Gets or sets the total bytes freed.
    /// </summary>
    public long TotalBytesFreed;
    /// <summary>
    /// The active allocations.
    /// </summary>

    /// <summary>
    /// Gets or sets the current number of active allocations.
    /// </summary>
    public int ActiveAllocations;
    /// <summary>
    /// The total violations.
    /// </summary>

    /// <summary>
    /// Gets or sets the total number of security violations detected.
    /// </summary>
    public long TotalViolations;
    /// <summary>
    /// The corruption detections.
    /// </summary>

    /// <summary>
    /// Gets or sets the number of memory corruption incidents detected.
    /// </summary>
    public long CorruptionDetections;
    /// <summary>
    /// The double free attempts.
    /// </summary>

    /// <summary>
    /// Gets or sets the number of double-free attempts blocked.
    /// </summary>
    public long DoubleFreeAttempts;
    /// <summary>
    /// The use after free attempts.
    /// </summary>

    /// <summary>
    /// Gets or sets the number of use-after-free attempts blocked.
    /// </summary>
    public long UseAfterFreeAttempts;

    /// <summary>
    /// Gets the allocations categorized by data classification.
    /// </summary>
    public ConcurrentDictionary<DataClassification, long> AllocationsByClassification { get; } = new();

    /// <summary>
    /// Gets the violations categorized by type.
    /// </summary>
    public ConcurrentDictionary<SanitizationViolationType, long> ViolationsByType { get; } = new();

    /// <summary>
    /// Gets the current memory utilization.
    /// </summary>
    public long CurrentMemoryUsage => TotalBytesAllocated - TotalBytesFreed;

    /// <summary>
    /// Gets the security violation rate as a percentage.
    /// </summary>
    public double ViolationRate => TotalAllocations > 0
        ? (double)TotalViolations / TotalAllocations * 100.0
        : 0.0;
}
