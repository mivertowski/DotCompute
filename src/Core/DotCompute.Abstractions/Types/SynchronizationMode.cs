// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Synchronization modes for parallel stage execution.
/// </summary>
public enum SynchronizationMode
{
    /// <summary>
    /// Wait for all parallel stages to complete.
    /// </summary>
    WaitAll,

    /// <summary>
    /// Wait for any of the parallel stages to complete.
    /// </summary>
    WaitAny,

    /// <summary>
    /// Fire and forget - don't wait for completion.
    /// </summary>
    FireAndForget,

    /// <summary>
    /// Sequential execution (no parallelism).
    /// </summary>
    Sequential,

    /// <summary>
    /// Barrier synchronization - all stages must reach sync point.
    /// </summary>
    Barrier,

    /// <summary>
    /// Custom synchronization strategy.
    /// </summary>
    Custom
}
