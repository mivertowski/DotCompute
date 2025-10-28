// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Execution priority levels for pipeline stages and operations.
/// </summary>
public enum ExecutionPriority
{
    /// <summary>
    /// Lowest priority for background operations.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Low priority for non-critical operations.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Below normal priority.
    /// </summary>
    BelowNormal = 2,

    /// <summary>
    /// Normal priority for standard operations.
    /// </summary>
    Normal = 3,

    /// <summary>
    /// Above normal priority.
    /// </summary>
    AboveNormal = 4,

    /// <summary>
    /// High priority for important operations.
    /// </summary>
    High = 5,

    /// <summary>
    /// Highest priority for critical operations.
    /// </summary>
    Critical = 6,

    /// <summary>
    /// Real-time priority for time-sensitive operations.
    /// </summary>
    RealTime = 7
}
