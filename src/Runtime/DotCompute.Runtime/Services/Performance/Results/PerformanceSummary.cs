// <copyright file="PerformanceSummary.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Runtime.Services.Performance.Types;

namespace DotCompute.Runtime.Services.Performance.Results;

/// <summary>
/// Comprehensive performance summary.
/// High-level overview of system performance with recommendations.
/// </summary>
public class PerformanceSummary
{
    /// <summary>
    /// Gets when this summary was generated.
    /// Timestamp of summary creation.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the time period covered by this summary.
    /// Range of data included in the analysis.
    /// </summary>
    public TimeRange Period { get; init; } = default!;

    /// <summary>
    /// Gets key performance metrics.
    /// Important performance indicators and their values.
    /// </summary>
    public Dictionary<string, double> KeyMetrics { get; init; } = [];

    /// <summary>
    /// Gets performance recommendations.
    /// Suggested optimizations based on the analysis.
    /// </summary>
    public List<string> Recommendations { get; init; } = [];
}