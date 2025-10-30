// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Enums;

/// <summary>
/// Defines the possible performance trend directions observed during analysis.
/// Indicates whether performance is getting better, worse, or staying consistent.
/// </summary>
public enum PerformanceTrend
{
    /// <summary>
    /// Performance is improving over time, showing better execution characteristics.
    /// </summary>
    Improving,

    /// <summary>
    /// Performance is stable, showing consistent execution characteristics over time.
    /// </summary>
    Stable,

    /// <summary>
    /// Performance is degrading over time, showing worse execution characteristics.
    /// </summary>
    Degrading
}
