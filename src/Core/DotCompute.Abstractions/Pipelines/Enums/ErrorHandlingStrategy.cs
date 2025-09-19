// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Error handling strategies for kernel chain execution.
/// </summary>
public enum ErrorHandlingStrategy
{
    /// <summary>
    /// Continue execution, ignoring the error.
    /// </summary>
    Continue,

    /// <summary>
    /// Retry the failed operation.
    /// </summary>
    Retry,

    /// <summary>
    /// Skip the failed step and continue with the next.
    /// </summary>
    Skip,

    /// <summary>
    /// Abort the entire chain execution.
    /// </summary>
    Abort,

    /// <summary>
    /// Fall back to a default value and continue.
    /// </summary>
    Fallback
}