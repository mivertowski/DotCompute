// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Policies;

/// <summary>
/// Error handling policies configuration.
/// </summary>
public sealed class CudaErrorHandlingPolicies
{
    /// <summary>
    /// Gets or sets the statistics policy.
    /// </summary>
    /// <value>The statistics policy.</value>
    public ErrorStatisticsPolicy StatisticsPolicy { get; init; } = new();
    /// <summary>
    /// Gets or sets the recovery policy.
    /// </summary>
    /// <value>The recovery policy.</value>
    public ErrorRecoveryPolicy RecoveryPolicy { get; init; } = new();
}