// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Optimization.Enums;
using DotCompute.Core.Optimization.Models;

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Analysis results for a specific workload.
/// </summary>
public class WorkloadAnalysis
{
    public WorkloadSignature WorkloadSignature { get; set; } = new();
    public WorkloadPattern WorkloadPattern { get; set; }
    public double EstimatedExecutionTimeMs { get; set; }
    public long EstimatedMemoryUsageMB { get; set; }
    public Dictionary<string, BackendPerformanceStats> HistoricalPerformance { get; set; } = [];
    public int TotalHistoryEntries { get; set; }
    public bool HasSufficientHistory { get; set; }
}