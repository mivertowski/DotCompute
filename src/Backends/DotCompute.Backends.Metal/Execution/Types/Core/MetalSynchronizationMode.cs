// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Defines the synchronization model for Metal operations
/// </summary>
public enum MetalSynchronizationMode
{
    /// <summary>
    /// Synchronous execution with blocking waits
    /// </summary>
    Synchronous,

    /// <summary>
    /// Asynchronous execution with callbacks
    /// </summary>
    Asynchronous,

    /// <summary>
    /// Event-driven synchronization
    /// </summary>
    EventDriven,

    /// <summary>
    /// Pipeline-based execution
    /// </summary>
    Pipelined
}
