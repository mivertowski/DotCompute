// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Analysis;

/// <summary>
/// Represents a performance bottleneck identified during trace analysis.
/// Contains information about the bottleneck location, impact, and potential solutions.
/// </summary>
public sealed class PerformanceBottleneck
{
    /// <summary>
    /// Gets or sets the unique identifier for this bottleneck.
    /// Used for tracking and referencing specific bottlenecks across analyses.
    /// </summary>
    /// <value>The bottleneck identifier as a string.</value>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name or type of the bottleneck.
    /// Provides a human-readable classification of the bottleneck.
    /// </summary>
    /// <value>The bottleneck name as a string.</value>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the bottleneck.
    /// Explains what is causing the performance issue and its characteristics.
    /// </summary>
    /// <value>The bottleneck description as a string.</value>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the location where the bottleneck occurs.
    /// Could be a span ID, device ID, or operation name.
    /// </summary>
    /// <value>The bottleneck location as a string.</value>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the severity score of the bottleneck (0.0 to 1.0).
    /// Higher values indicate more severe performance impact.
    /// </summary>
    /// <value>The severity score as a decimal between 0.0 and 1.0.</value>
    public double Severity { get; set; }

    /// <summary>
    /// Gets or sets the estimated performance impact percentage.
    /// Represents how much the bottleneck is slowing down overall execution.
    /// </summary>
    /// <value>The impact percentage as a decimal (e.g., 0.25 = 25% impact).</value>
    public double ImpactPercentage { get; set; }

    /// <summary>
    /// Gets or sets the duration of the bottleneck effect.
    /// Represents how long the bottleneck was active during the trace.
    /// </summary>
    /// <value>The bottleneck duration as a TimeSpan.</value>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the recommended actions to address this bottleneck.
    /// Provides specific suggestions for resolving or mitigating the issue.
    /// </summary>
    /// <value>A list of recommendation strings.</value>
    public IList<string> Recommendations { get; } = [];

    /// <summary>
    /// Gets or sets additional metadata about the bottleneck.
    /// Contains context-specific information that may be useful for analysis.
    /// </summary>
    /// <value>A dictionary of metadata key-value pairs.</value>
    public Dictionary<string, object?> Metadata { get; } = [];
}