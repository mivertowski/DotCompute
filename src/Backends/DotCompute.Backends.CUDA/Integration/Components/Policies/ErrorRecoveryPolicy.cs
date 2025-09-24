// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Policies;

/// <summary>
/// Error recovery policy.
/// </summary>
public sealed class ErrorRecoveryPolicy
{
    public bool EnableAutoRecovery { get; init; } = true;
    public int MaxRecoveryAttempts { get; init; } = 3;
    public TimeSpan RecoveryTimeout { get; init; } = TimeSpan.FromSeconds(30);
}