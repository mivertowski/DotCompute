// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Memory usage analysis result.
/// </summary>
public class MemoryUsageAnalysis
{
    public string KernelName { get; set; } = string.Empty;
    public double AverageMemoryUsage { get; set; }
    public long PeakMemoryUsage { get; set; }
    public long MinMemoryUsage { get; set; }
    public long TotalMemoryAllocated { get; set; }
    public DateTime AnalysisTime { get; set; }
}