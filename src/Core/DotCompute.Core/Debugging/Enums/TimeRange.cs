// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Debugging.Enums;

/// <summary>
/// Defines time ranges for performance analysis and trending.
/// </summary>
public enum TimeRange
{
    /// <summary>
    /// Last hour of data.
    /// </summary>
    LastHour,

    /// <summary>
    /// Last 24 hours of data.
    /// </summary>
    LastDay,

    /// <summary>
    /// Last 7 days of data.
    /// </summary>
    LastWeek,

    /// <summary>
    /// Last 30 days of data.
    /// </summary>
    LastMonth
}