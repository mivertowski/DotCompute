// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Defines the memory space where kernel parameters reside.
/// </summary>
public enum MemorySpace
{
    /// <summary>
    /// Global memory space accessible by all work items.
    /// </summary>
    Global,

    /// <summary>
    /// Local memory space shared within a work group.
    /// </summary>
    Local,

    /// <summary>
    /// Shared memory space within a thread block (CUDA terminology).
    /// </summary>
    Shared,

    /// <summary>
    /// Constant memory space for read-only data.
    /// </summary>
    Constant,

    /// <summary>
    /// Private memory space for each work item.
    /// </summary>
    Private,

    /// <summary>
    /// Device memory space for GPU devices.
    /// </summary>
    Device
}