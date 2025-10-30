// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Recovery;

/// <summary>
/// Base interface for all recovery strategies in DotCompute
/// </summary>
public interface IRecoveryStrategy<TContext>
{
    /// <summary>
    /// The type of errors this strategy can handle
    /// </summary>
    public RecoveryCapability Capability { get; }

    /// <summary>
    /// Priority of this strategy (higher values = higher priority)
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Determines if this strategy can handle the given error
    /// </summary>
    public bool CanHandle(Exception exception, TContext context);

    /// <summary>
    /// Attempts to recover from the error
    /// </summary>
    public Task<RecoveryResult> RecoverAsync(Exception exception, TContext context, RecoveryOptions options, CancellationToken cancellationToken = default);
}
