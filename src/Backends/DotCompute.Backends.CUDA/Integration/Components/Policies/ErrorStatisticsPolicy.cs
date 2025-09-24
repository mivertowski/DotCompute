// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Policies;

/// <summary>
/// Error statistics policy.
/// </summary>
public sealed class ErrorStatisticsPolicy
{
    public bool CaptureDetailedDiagnostics { get; init; } = true;
    public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromHours(24);
    public int MaxRecentErrors { get; init; } = 100;
}