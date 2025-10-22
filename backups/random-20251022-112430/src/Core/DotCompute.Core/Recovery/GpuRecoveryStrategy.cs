// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;
/// <summary>
/// An gpu recovery strategy enumeration.
/// </summary>

/// <summary>
/// GPU recovery strategies
/// </summary>
public enum GpuRecoveryStrategy
{
    /// <summary>
    /// Simple retry of the failed operation.
    /// </summary>
    SimpleRetry,

    /// <summary>
    /// Attempt to recover GPU memory before retry.
    /// </summary>
    MemoryRecovery,

    /// <summary>
    /// Terminate the currently executing kernel.
    /// </summary>
    KernelTermination,

    /// <summary>
    /// Reset the GPU context to a clean state.
    /// </summary>
    ContextReset,

    /// <summary>
    /// Perform a full device reset (most aggressive recovery).
    /// </summary>
    DeviceReset
}