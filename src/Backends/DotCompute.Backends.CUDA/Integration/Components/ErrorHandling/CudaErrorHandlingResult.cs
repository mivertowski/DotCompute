// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

/// <summary>
/// CUDA error handling result.
/// </summary>
public sealed class CudaErrorHandlingResult
{
    /// <summary>
    /// Gets or sets the original error.
    /// </summary>
    /// <value>The original error.</value>
    public CudaError OriginalError { get; init; }
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    /// <value>The severity.</value>
    public CudaErrorSeverity Severity { get; init; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the recovery attempted.
    /// </summary>
    /// <value>The recovery attempted.</value>
    public bool RecoveryAttempted { get; init; }
    /// <summary>
    /// Gets or sets the recovery successful.
    /// </summary>
    /// <value>The recovery successful.</value>
    public bool RecoverySuccessful { get; init; }
    /// <summary>
    /// Gets or sets the recovery actions.
    /// </summary>
    /// <value>The recovery actions.</value>
    public IReadOnlyList<string> RecoveryActions { get; init; } = [];
    /// <summary>
    /// Gets or sets the recovery message.
    /// </summary>
    /// <value>The recovery message.</value>
    public string? RecoveryMessage { get; init; }
}
