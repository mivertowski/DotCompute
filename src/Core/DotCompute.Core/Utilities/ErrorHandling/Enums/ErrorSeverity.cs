// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Utilities.ErrorHandling.Enums;

/// <summary>
/// Error severity levels for prioritizing error handling and alerting.
/// Used to determine appropriate response actions and escalation procedures.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Low severity - informational errors that don't impact functionality.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium severity - errors that may degrade performance or user experience.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High severity - errors that significantly impact functionality.
    /// </summary>
    High = 3,

    /// <summary>
    /// Critical severity - errors that cause system failure or data loss.
    /// </summary>
    Critical = 4
}
