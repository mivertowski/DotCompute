// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// Error recovery result.
/// </summary>
public sealed class ErrorRecoveryResult
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; init; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public string Message { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the actions performed.
    /// </summary>
    /// <value>The actions performed.</value>
    public IReadOnlyList<string> ActionsPerformed { get; init; } = [];
}