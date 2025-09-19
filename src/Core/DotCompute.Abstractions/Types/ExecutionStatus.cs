// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Represents the execution status of a pipeline or stage.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// Execution has not yet started.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Execution is currently running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Execution completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Execution failed with errors.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Execution was cancelled before completion.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Execution timed out.
    /// </summary>
    Timeout = 5
}