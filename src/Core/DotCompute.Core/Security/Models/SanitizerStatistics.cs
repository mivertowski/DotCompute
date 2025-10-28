// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DotCompute.Core.Security.Enums;

namespace DotCompute.Core.Security.Models;

/// <summary>
/// Memory sanitizer statistics and metrics.
/// Tracks allocation patterns, violations, and performance metrics.
/// </summary>
[SuppressMessage("Design", "CA1051:Do not declare visible instance fields",
    Justification = "Public fields are required for thread-safe Interlocked operations (Interlocked.Increment/Add require ref parameters to fields, not properties).")]
public sealed class SanitizerStatistics
{
    /// <summary>
    /// Total number of allocations performed.
    /// </summary>
    public long TotalAllocations;

    /// <summary>
    /// Total number of deallocations performed.
    /// </summary>
    public long TotalDeallocations;

    /// <summary>
    /// Total bytes allocated.
    /// </summary>
    public long TotalBytesAllocated;

    /// <summary>
    /// Total bytes freed.
    /// </summary>
    public long TotalBytesFreed;

    /// <summary>
    /// Current number of active allocations.
    /// </summary>
    public int ActiveAllocations;

    /// <summary>
    /// Total number of security violations detected.
    /// </summary>
    public long TotalViolations;

    /// <summary>
    /// Number of memory corruption incidents detected.
    /// </summary>
    public long CorruptionDetections;

    /// <summary>
    /// Number of double-free attempts blocked.
    /// </summary>
    public long DoubleFreeAttempts;

    /// <summary>
    /// Number of use-after-free attempts blocked.
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
