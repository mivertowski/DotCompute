// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Models;

/// <summary>
/// Memory leak detection report.
/// Contains analysis results from memory leak detection scans.
/// </summary>
public sealed class MemoryLeakReport
{
    /// <summary>
    /// Gets or sets the time when the scan was performed.
    /// </summary>
    public DateTimeOffset ScanTime { get; init; }

    /// <summary>
    /// Gets or sets the total number of active allocations.
    /// </summary>
    public int TotalActiveAllocations { get; init; }

    /// <summary>
    /// Gets or sets the list of suspicious allocations that may be leaks.
    /// </summary>
    public IList<LeakSuspect> SuspiciousAllocations { get; } = [];

    /// <summary>
    /// Gets or sets the total bytes in suspicious allocations.
    /// </summary>
    public long TotalSuspiciousBytes { get; set; }

    /// <summary>
    /// Gets or sets the count of high-suspicion allocations.
    /// </summary>
    public int HighSuspicionCount { get; set; }

    /// <summary>
    /// Gets the percentage of allocations flagged as suspicious.
    /// </summary>
    public double SuspiciousPercentage => TotalActiveAllocations > 0
        ? (double)SuspiciousAllocations.Count / TotalActiveAllocations * 100.0
        : 0.0;
}
