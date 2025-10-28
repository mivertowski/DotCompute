// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Analysis;

/// <summary>
/// Contains comprehensive analysis results for performance bottlenecks identified during pipeline execution.
/// Provides insights into the primary factors limiting performance and their relative impact.
/// </summary>
public sealed class BottleneckAnalysis
{
    /// <summary>
    /// Gets the primary bottleneck that has the most significant impact on performance.
    /// This represents the most critical performance issue that should be addressed first.
    /// </summary>
    /// <value>The primary bottleneck information.</value>
    public required BottleneckInfo PrimaryBottleneck { get; init; }

    /// <summary>
    /// Gets the secondary bottlenecks that also impact performance but to a lesser degree.
    /// These should be addressed after resolving the primary bottleneck.
    /// </summary>
    /// <value>A read-only list of secondary bottleneck information, or null if none exist.</value>
    public IReadOnlyList<BottleneckInfo>? SecondaryBottlenecks { get; init; }

    /// <summary>
    /// Gets the estimated percentage of performance impact from the identified bottlenecks.
    /// Represents how much the overall performance could be improved by addressing these bottlenecks.
    /// </summary>
    /// <value>The estimated impact percentage as a double (e.g., 0.25 = 25% impact).</value>
    public required double EstimatedImpactPercentage { get; init; }
}