// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Performance metric tracking for specific operations.
/// </summary>
internal sealed class PerformanceMetric
{
    /// <summary>
    /// Gets or sets the count.
    /// </summary>
    /// <value>The count.</value>
    public long Count { get; set; }
    /// <summary>
    /// Gets or sets the total duration.
    /// </summary>
    /// <value>The total duration.</value>
    public double TotalDuration { get; set; }
    /// <summary>
    /// Gets or sets the min duration.
    /// </summary>
    /// <value>The min duration.</value>
    public double MinDuration { get; set; } = double.MaxValue;
    /// <summary>
    /// Gets or sets the max duration.
    /// </summary>
    /// <value>The max duration.</value>
    public double MaxDuration { get; set; }
    /// <summary>
    /// Gets or sets the last updated.
    /// </summary>
    /// <value>The last updated.</value>
    public DateTimeOffset LastUpdated { get; set; }
    /// <summary>
    /// Gets or sets the average duration.
    /// </summary>
    /// <value>The average duration.</value>

    public double AverageDuration => Count > 0 ? TotalDuration / Count : 0.0;
}
