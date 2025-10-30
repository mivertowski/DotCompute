// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Types;

/// <summary>
/// Defines the available strategies for GPU recovery operations.
/// These strategies provide different levels of intervention when GPU operations fail.
/// </summary>
/// <remarks>
/// Recovery strategies are ordered by increasing severity of intervention:
/// - SimpleRetry: Minimal intervention, just retry the operation
/// - MemoryRecovery: Clear memory allocations and retry
/// - KernelTermination: Terminate hanging kernels
/// - ContextReset: Reset the GPU context
/// - DeviceReset: Full device reset (most severe)
/// </remarks>
public enum GpuRecoveryStrategy
{
    /// <summary>
    /// Simple retry of the failed operation without any cleanup.
    /// Used for transient failures that may resolve on retry.
    /// </summary>
    SimpleRetry,

    /// <summary>
    /// Attempt to recover by clearing memory allocations and retrying.
    /// Used when memory pressure or allocation failures are suspected.
    /// </summary>
    MemoryRecovery,

    /// <summary>
    /// Terminate hanging or long-running kernels before retrying.
    /// Used when kernel execution appears to be stuck or unresponsive.
    /// </summary>
    KernelTermination,

    /// <summary>
    /// Reset the GPU context and retry the operation.
    /// Used when the GPU context may be in an invalid state.
    /// </summary>
    ContextReset,

    /// <summary>
    /// Perform a full device reset and retry.
    /// Used as a last resort when other recovery strategies have failed.
    /// </summary>
    DeviceReset
}
