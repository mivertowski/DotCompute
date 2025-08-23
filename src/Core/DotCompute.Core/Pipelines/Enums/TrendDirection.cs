// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Enums;

/// <summary>
/// Defines the possible directions for performance trends observed over time.
/// Indicates whether a performance metric is getting better, worse, or staying consistent.
/// </summary>
public enum TrendDirection
{
    /// <summary>
    /// Performance is improving over time, showing better values for the metric.
    /// Indicates positive trends such as faster execution times or higher throughput.
    /// </summary>
    Improving,

    /// <summary>
    /// Performance is stable over time, showing consistent values for the metric.
    /// Indicates predictable performance with minimal variation.
    /// </summary>
    Stable,

    /// <summary>
    /// Performance is degrading over time, showing worse values for the metric.
    /// Indicates negative trends such as slower execution times or lower throughput.
    /// </summary>
    Degrading
}