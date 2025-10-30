// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Enums;

/// <summary>
/// Defines the possible status values for a complete trace execution.
/// Indicates the overall success or failure state of the traced operation.
/// </summary>
public enum TraceStatus
{
    /// <summary>
    /// The trace completed successfully with all operations finishing without errors.
    /// </summary>
    Ok,

    /// <summary>
    /// The trace encountered errors and one or more operations failed.
    /// </summary>
    Error,

    /// <summary>
    /// The trace exceeded the configured timeout period.
    /// </summary>
    Timeout,

    /// <summary>
    /// The trace completed with some operations succeeding and others failing.
    /// </summary>
    PartialFailure
}
