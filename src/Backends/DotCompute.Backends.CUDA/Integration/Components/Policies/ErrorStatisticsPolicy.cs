// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Policies;

/// <summary>
/// Error statistics policy.
/// </summary>
public sealed class ErrorStatisticsPolicy
{
    /// <summary>
    /// Gets or sets the capture detailed diagnostics.
    /// </summary>
    /// <value>The capture detailed diagnostics.</value>
    public bool CaptureDetailedDiagnostics { get; init; } = true;
    /// <summary>
    /// Gets or sets the retention period.
    /// </summary>
    /// <value>The retention period.</value>
    public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromHours(24);
    /// <summary>
    /// Gets or sets the max recent errors.
    /// </summary>
    /// <value>The max recent errors.</value>
    public int MaxRecentErrors { get; init; } = 100;
}
