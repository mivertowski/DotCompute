// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Core.Debugging.Types;
using DotCompute.Abstractions.Debugging;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Comprehensive performance analysis result.
/// </summary>
public class PerformanceAnalysisResult
{
    public required string KernelName { get; set; }
    public PerformanceReport? PerformanceReport { get; set; }
    public MemoryUsageAnalysis? MemoryAnalysis { get; set; }
    public BottleneckAnalysis? BottleneckAnalysis { get; set; }
    public ExecutionStatistics? ExecutionStatistics { get; set; }
    public object? AdvancedAnalysis { get; set; }
    public DateTime GeneratedAt { get; set; }
}