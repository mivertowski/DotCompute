// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;
using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Interfaces.Kernels;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Report comparing results from multiple backend executions.
/// </summary>
public class ResultComparisonReport
{
    public string KernelName { get; init; } = string.Empty;
    public bool ResultsMatch { get; set; }
    public IReadOnlyList<string> BackendsCompared { get; init; } = Array.Empty<string>();
    public Collection<ResultDifference> Differences { get; init; } = [];
    public ComparisonStrategy Strategy { get; init; }
    public float Tolerance { get; init; }
    public Dictionary<string, PerformanceMetrics> PerformanceComparison { get; init; } = [];

    /// <summary>
    /// First execution result being compared.
    /// </summary>
    public KernelExecutionResult? Result1 { get; init; }

    /// <summary>
    /// Second execution result being compared.
    /// </summary>
    public KernelExecutionResult? Result2 { get; init; }

    /// <summary>
    /// List of comparison issues found between the results.
    /// </summary>
    public Collection<ComparisonIssue> Issues { get; init; } = [];

    /// <summary>
    /// Time when the comparison was performed.
    /// </summary>
    public DateTimeOffset ComparisonTime { get; init; }

    /// <summary>
    /// Alternative property name for ResultDifference (backward compatibility).
    /// </summary>
    public IReadOnlyList<ResultDifference> ResultDifference => Differences;
}
