// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Debugging;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Result of bottleneck detection analysis.
/// </summary>
public class BottleneckAnalysis
{
    public required string KernelName { get; set; }
    public List<Bottleneck> Bottlenecks { get; set; } = new();
    public DateTime AnalysisTime { get; set; }
}

/// <summary>
/// Represents a performance bottleneck.
/// </summary>
public class Bottleneck
{
    public BottleneckType Type { get; set; }
    public BottleneckSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

// BottleneckSeverity enum consolidated to DotCompute.Abstractions.Debugging.PerformanceAnalysisResult

// Use: using static DotCompute.Abstractions.Debugging.BottleneckSeverity;