// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Debugging.Types;
using DotCompute.Abstractions.Debugging.Types;
using CorePerformanceReport = DotCompute.Core.Debugging.Types.PerformanceReport;
using CoreMemoryUsageAnalysis = DotCompute.Core.Debugging.Types.MemoryUsageAnalysis;
using AbstractionsExecutionStatistics = DotCompute.Abstractions.Debugging.ExecutionStatistics;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Comprehensive performance analysis result.
/// </summary>
public class PerformanceAnalysisResult
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the performance report.
    /// </summary>
    /// <value>The performance report.</value>
    public CorePerformanceReport? PerformanceReport { get; set; }
    /// <summary>
    /// Gets or sets the memory analysis.
    /// </summary>
    /// <value>The memory analysis.</value>
    public CoreMemoryUsageAnalysis? MemoryAnalysis { get; set; }
    /// <summary>
    /// Gets or sets the bottleneck analysis.
    /// </summary>
    /// <value>The bottleneck analysis.</value>
    public BottleneckAnalysis? BottleneckAnalysis { get; set; }
    /// <summary>
    /// Gets or sets the execution statistics.
    /// </summary>
    /// <value>The execution statistics.</value>
    public AbstractionsExecutionStatistics? ExecutionStatistics { get; set; }
    /// <summary>
    /// Gets or sets the advanced analysis.
    /// </summary>
    /// <value>The advanced analysis.</value>
    public object? AdvancedAnalysis { get; set; }
    /// <summary>
    /// Gets or sets the generated at.
    /// </summary>
    /// <value>The generated at.</value>
    public DateTime GeneratedAt { get; set; }
}