// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// Error trends analysis.
/// </summary>
public sealed class ErrorTrends
{
    /// <summary>
    /// Gets or sets the total errors.
    /// </summary>
    /// <value>The total errors.</value>
    public long TotalErrors { get; init; }
    /// <summary>
    /// Gets or sets the recent error rate.
    /// </summary>
    /// <value>The recent error rate.</value>
    public double RecentErrorRate { get; init; }
    /// <summary>
    /// Gets or sets the recovery success rate.
    /// </summary>
    /// <value>The recovery success rate.</value>
    public double RecoverySuccessRate { get; init; }
    /// <summary>
    /// Gets or sets the trend direction.
    /// </summary>
    /// <value>The trend direction.</value>
    public ErrorTrendDirection TrendDirection { get; init; }
}
