// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Debugging.Core;

/// <summary>
/// Result of bottleneck detection analysis.
/// </summary>
public class BottleneckAnalysis
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the bottlenecks.
    /// </summary>
    /// <value>The bottlenecks.</value>
    public IList<Bottleneck> Bottlenecks { get; } = [];
    /// <summary>
    /// Gets or sets the analysis time.
    /// </summary>
    /// <value>The analysis time.</value>
    public DateTime AnalysisTime { get; set; }
}

/// <summary>
/// Represents a performance bottleneck.
/// </summary>
public class Bottleneck
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public BottleneckType Type { get; set; }
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public BottleneckSeverity Severity { get; set; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the impact.
    /// </summary>
    /// <value>The impact.</value>
    public string Impact { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the recommendation.
    /// </summary>
    /// <value>The recommendation.</value>
    public string Recommendation { get; set; } = string.Empty;
}

// BottleneckSeverity enum consolidated to DotCompute.Abstractions.Debugging.PerformanceAnalysisResult

// Use: using static DotCompute.Abstractions.Debugging.BottleneckSeverity;