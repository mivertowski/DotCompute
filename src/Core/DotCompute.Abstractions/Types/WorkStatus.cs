// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines the status of a work item in the execution pipeline.
/// Used for tracking work items through the execution lifecycle.
/// </summary>
public enum WorkStatus
{
    /// <summary>
    /// Work item has been created but not yet queued.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Work item is queued and waiting for execution.
    /// </summary>
    Queued,

    /// <summary>
    /// Work item is currently executing.
    /// </summary>
    Executing,

    /// <summary>
    /// Work item has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Work item execution failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Work item was cancelled before or during execution.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Work item timed out during execution.
    /// </summary>
    TimedOut,

    /// <summary>
    /// Work item is being retried after a failure.
    /// </summary>
    Retrying,

    /// <summary>
    /// Work item is suspended and waiting for resources.
    /// </summary>
    Suspended
}
