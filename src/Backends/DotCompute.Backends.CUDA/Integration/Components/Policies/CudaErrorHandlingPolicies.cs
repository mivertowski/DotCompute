// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Policies;

/// <summary>
/// Error handling policies configuration.
/// </summary>
public sealed class CudaErrorHandlingPolicies
{
    public ErrorStatisticsPolicy StatisticsPolicy { get; init; } = new();
    public ErrorRecoveryPolicy RecoveryPolicy { get; init; } = new();
}