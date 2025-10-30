// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Enums;

/// <summary>
/// Defines the possible status values for a span execution.
/// Indicates the success or failure state of the tracked operation.
/// </summary>
public enum SpanStatus
{
    /// <summary>
    /// The operation completed successfully without errors.
    /// </summary>
    Ok,

    /// <summary>
    /// The operation encountered an error and failed to complete.
    /// </summary>
    Error,

    /// <summary>
    /// The operation exceeded the configured timeout period.
    /// </summary>
    Timeout,

    /// <summary>
    /// The operation was cancelled before completion.
    /// </summary>
    Cancelled
}
