// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Performance metrics for telemetry operations.
/// </summary>
public sealed class TelemetryPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the total operations.
    /// </summary>
    /// <value>The total operations.</value>
    public long TotalOperations { get; init; }
    /// <summary>
    /// Gets or sets the total errors.
    /// </summary>
    /// <value>The total errors.</value>
    public long TotalErrors { get; init; }
    /// <summary>
    /// Gets or sets the overhead ticks.
    /// </summary>
    /// <value>The overhead ticks.</value>
    public long OverheadTicks { get; init; }
    /// <summary>
    /// Gets or sets the overhead percentage.
    /// </summary>
    /// <value>The overhead percentage.</value>
    public double OverheadPercentage { get; init; }
    /// <summary>
    /// Gets or sets the event queue size.
    /// </summary>
    /// <value>The event queue size.</value>
    public int EventQueueSize { get; init; }
    /// <summary>
    /// Gets or sets the active metrics.
    /// </summary>
    /// <value>The active metrics.</value>
    public int ActiveMetrics { get; init; }
    /// <summary>
    /// Gets or sets the backend type.
    /// </summary>
    /// <value>The backend type.</value>
    public string BackendType { get; init; } = string.Empty;
}
