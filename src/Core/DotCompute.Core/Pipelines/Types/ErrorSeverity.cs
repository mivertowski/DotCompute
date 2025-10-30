// <copyright file="ErrorSeverity.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Pipelines.Types;

/// <summary>
/// Defines error severity levels for pipeline operations.
/// Used to categorize and prioritize errors based on their impact on execution.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// No error or unknown severity level.
    /// </summary>
    None = 0,

    /// <summary>
    /// Informational message, not an actual error.
    /// Used for logging and diagnostic purposes.
    /// </summary>
    Information,

    /// <summary>
    /// Warning that doesn't prevent execution.
    /// Indicates potential issues that should be reviewed.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that affects results but allows continuation.
    /// May produce degraded or partial results.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that prevents execution.
    /// Requires immediate attention and recovery action.
    /// </summary>
    Critical,

    /// <summary>
    /// Fatal error that corrupts state.
    /// System cannot continue and must be restarted.
    /// </summary>
    Fatal
}
