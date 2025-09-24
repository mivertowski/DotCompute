// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// Error trends analysis.
/// </summary>
public sealed class ErrorTrends
{
    public long TotalErrors { get; init; }
    public double RecentErrorRate { get; init; }
    public double RecoverySuccessRate { get; init; }
    public ErrorTrendDirection TrendDirection { get; init; }
}