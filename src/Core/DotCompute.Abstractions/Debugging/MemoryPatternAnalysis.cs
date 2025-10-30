// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents the result of memory pattern analysis for a kernel.
/// </summary>
public sealed class MemoryPatternAnalysis
{
    /// <summary>
    /// Gets the name of the kernel that was analyzed.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the time when the analysis was performed.
    /// </summary>
    public DateTime AnalysisTime { get; init; }

    /// <summary>
    /// Gets whether the memory access patterns are safe.
    /// </summary>
    public bool IsMemorySafe { get; set; }

    /// <summary>
    /// Gets the list of memory issues found.
    /// </summary>
    public Collection<MemoryIssue> Issues { get; init; } = [];

    /// <summary>
    /// Gets the list of recommendations for memory optimization.
    /// </summary>
    public Collection<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets the total memory used by input data.
    /// </summary>
    public long TotalInputMemory { get; set; }

    /// <summary>
    /// Gets the estimated peak memory usage during kernel execution.
    /// </summary>
    public long EstimatedPeakMemory { get; init; }

    /// <summary>
    /// Gets the memory efficiency score (0-100).
    /// </summary>
    public double EfficiencyScore { get; init; }
}

/// <summary>
/// Represents a memory-related issue found during analysis.
/// </summary>
public sealed class MemoryIssue
{
    /// <summary>
    /// Gets the type of memory issue.
    /// </summary>
    public MemoryIssueType Type { get; init; }

    /// <summary>
    /// Gets the severity of the issue.
    /// </summary>
    public MemoryIssueSeverity Severity { get; init; }

    /// <summary>
    /// Gets the description of the issue.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the context where the issue was found.
    /// </summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>
    /// Gets the location of the issue if applicable.
    /// </summary>
    public string? Location { get; init; }
}

/// <summary>
/// Types of memory issues that can be detected.
/// </summary>
public enum MemoryIssueType
{
    /// <summary>
    /// Buffer overflow or underflow.
    /// </summary>
    BufferOverflow,

    /// <summary>
    /// Large memory allocation.
    /// </summary>
    LargeAllocation,

    /// <summary>
    /// Memory leak detected.
    /// </summary>
    MemoryLeak,

    /// <summary>
    /// Inefficient memory access pattern.
    /// </summary>
    InefficientAccess,

    /// <summary>
    /// Empty array input.
    /// </summary>
    EmptyArray,

    /// <summary>
    /// Memory alignment issue.
    /// </summary>
    AlignmentIssue,

    /// <summary>
    /// Analysis error occurred.
    /// </summary>
    AnalysisError
}

/// <summary>
/// Severity levels for memory issues.
/// </summary>
public enum MemoryIssueSeverity
{
    /// <summary>
    /// Informational - no action required.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - should be addressed but not critical.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - may cause problems.
    /// </summary>
    Error,

    /// <summary>
    /// Critical - will likely cause failures.
    /// </summary>
    Critical
}
