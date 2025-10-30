// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Optimization.Enums;
using DotCompute.Core.Optimization.Models;

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Analysis results for a specific workload.
/// </summary>
public class WorkloadAnalysis
{
    /// <summary>
    /// Gets or sets the workload signature.
    /// </summary>
    /// <value>The workload signature.</value>
    public WorkloadSignature WorkloadSignature { get; set; } = new();
    /// <summary>
    /// Gets or sets the workload pattern.
    /// </summary>
    /// <value>The workload pattern.</value>
    public WorkloadPattern WorkloadPattern { get; set; }
    /// <summary>
    /// Gets or sets the estimated execution time ms.
    /// </summary>
    /// <value>The estimated execution time ms.</value>
    public double EstimatedExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the estimated memory usage m b.
    /// </summary>
    /// <value>The estimated memory usage m b.</value>
    public long EstimatedMemoryUsageMB { get; set; }
    /// <summary>
    /// Gets or sets the historical performance.
    /// </summary>
    /// <value>The historical performance.</value>
    public Dictionary<string, BackendPerformanceStats> HistoricalPerformance { get; init; } = [];
    /// <summary>
    /// Gets or sets the total history entries.
    /// </summary>
    /// <value>The total history entries.</value>
    public int TotalHistoryEntries { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sufficient history.
    /// </summary>
    /// <value>The has sufficient history.</value>
    public bool HasSufficientHistory { get; set; }
}
