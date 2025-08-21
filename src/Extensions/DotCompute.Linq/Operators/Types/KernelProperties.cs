// <copyright file="KernelProperties.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators.Types;

/// <summary>
/// Contains properties and constraints for a kernel.
/// </summary>
public class KernelProperties
{
    /// <summary>
    /// Gets or sets the maximum threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the register count per thread.
    /// </summary>
    public int RegisterCount { get; set; }

    /// <summary>
    /// Gets or sets the preferred work group size.
    /// </summary>
    public int[]? PreferredWorkGroupSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the kernel uses local memory.
    /// </summary>
    public bool UsesLocalMemory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the kernel uses atomics.
    /// </summary>
    public bool UsesAtomics { get; set; }
}