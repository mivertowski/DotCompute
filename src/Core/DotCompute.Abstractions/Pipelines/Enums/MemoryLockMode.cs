// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Specifies memory locking modes for pipeline memory management.
/// Controls how memory regions are locked and accessed during execution.
/// </summary>
public enum MemoryLockMode
{
    /// <summary>
    /// No memory locking, allows concurrent access with potential contention.
    /// </summary>
    None,

    /// <summary>
    /// Shared read lock allowing multiple concurrent readers.
    /// </summary>
    SharedRead,

    /// <summary>
    /// Exclusive write lock preventing all other access during writes.
    /// </summary>
    ExclusiveWrite,

    /// <summary>
    /// Read-write lock allowing multiple readers or a single writer.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// Optimistic locking using compare-and-swap operations.
    /// </summary>
    Optimistic,

    /// <summary>
    /// Pessimistic locking acquiring locks before access.
    /// </summary>
    Pessimistic,

    /// <summary>
    /// Spin lock for short-duration, low-contention scenarios.
    /// </summary>
    SpinLock,

    /// <summary>
    /// Mutex lock for longer-duration, high-contention scenarios.
    /// </summary>
    Mutex,

    /// <summary>
    /// Semaphore-based locking with configurable access count.
    /// </summary>
    Semaphore,

    /// <summary>
    /// Lock-free operations using atomic instructions.
    /// </summary>
    LockFree,

    /// <summary>
    /// Wait-free operations guaranteeing progress in bounded steps.
    /// </summary>
    WaitFree,

    /// <summary>
    /// Custom locking mechanism defined by the user.
    /// </summary>
    Custom
}
