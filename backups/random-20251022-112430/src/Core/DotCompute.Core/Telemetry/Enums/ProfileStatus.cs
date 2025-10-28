// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Enums;

/// <summary>
/// Defines the possible status values for a profiling session.
/// Indicates the current state or final outcome of the profiling operation.
/// </summary>
public enum ProfileStatus
{
    /// <summary>
    /// The profiling session is currently active and collecting data.
    /// </summary>
    Active,

    /// <summary>
    /// The profiling session has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The profiling session encountered an error and terminated abnormally.
    /// </summary>
    Error,

    /// <summary>
    /// The requested profiling session was not found in the system.
    /// </summary>
    NotFound,

    /// <summary>
    /// The profiling session was cancelled before completion.
    /// </summary>
    Cancelled
}