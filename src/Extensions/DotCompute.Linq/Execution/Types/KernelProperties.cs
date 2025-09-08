// <copyright file="KernelProperties.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Execution.Types;
/// <summary>
/// Contains properties and constraints for a kernel.
/// </summary>
public class KernelProperties
{
    /// <summary>
    /// Gets or sets the maximum threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; set; }
    /// Gets or sets the shared memory size in bytes.
    public int SharedMemorySize { get; set; }
    /// Gets or sets the register count per thread.
    public int RegisterCount { get; set; }
    /// Gets or sets the preferred work group size.
    public int[]? PreferredWorkGroupSize { get; set; }
    /// Gets or sets a value indicating whether the kernel uses local memory.
    public bool UsesLocalMemory { get; set; }
    /// Gets or sets a value indicating whether the kernel uses atomics.
    public bool UsesAtomics { get; set; }
}
