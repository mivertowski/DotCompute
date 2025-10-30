// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Security.Enums;

namespace DotCompute.Core.Security.Models;

/// <summary>
/// Suspected memory leak information.
/// Contains analysis data for potentially leaked memory allocations.
/// </summary>
public sealed class LeakSuspect
{
    /// <summary>
    /// Gets or sets the memory address of the suspected leak.
    /// </summary>
    public IntPtr Address { get; init; }

    /// <summary>
    /// Gets or sets the size of the allocation.
    /// </summary>
    public nuint Size { get; init; }

    /// <summary>
    /// Gets or sets how long the allocation has been active.
    /// </summary>
    public TimeSpan Age { get; init; }

    /// <summary>
    /// Gets or sets the time since last access to this memory.
    /// </summary>
    public TimeSpan TimeSinceLastAccess { get; init; }

    /// <summary>
    /// Gets or sets the number of times this memory has been accessed.
    /// </summary>
    public long AccessCount { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier for this allocation.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets or sets the call site where the allocation occurred.
    /// </summary>
    public required AllocationCallSite CallSite { get; init; }

    /// <summary>
    /// Gets or sets the security classification of the leaked data.
    /// </summary>
    public DataClassification Classification { get; init; }

    /// <summary>
    /// Gets or sets the suspicion level (0.0 to 1.0).
    /// </summary>
    public double SuspicionLevel { get; set; }

    /// <summary>
    /// Gets whether this leak suspect is considered high priority.
    /// </summary>
    public bool IsHighPriority => SuspicionLevel >= 0.8;
}
