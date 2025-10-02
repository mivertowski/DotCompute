// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Policies;

/// <summary>
/// Error recovery policy.
/// </summary>
public sealed class ErrorRecoveryPolicy
{
    /// <summary>
    /// Gets or sets the enable auto recovery.
    /// </summary>
    /// <value>The enable auto recovery.</value>
    public bool EnableAutoRecovery { get; init; } = true;
    /// <summary>
    /// Gets or sets the max recovery attempts.
    /// </summary>
    /// <value>The max recovery attempts.</value>
    public int MaxRecoveryAttempts { get; init; } = 3;
    /// <summary>
    /// Gets or sets the recovery timeout.
    /// </summary>
    /// <value>The recovery timeout.</value>
    public TimeSpan RecoveryTimeout { get; init; } = TimeSpan.FromSeconds(30);
}