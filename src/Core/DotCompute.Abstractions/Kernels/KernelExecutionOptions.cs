// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Options for kernel execution.
/// </summary>
public class KernelExecutionOptions
{
    /// <summary>
    /// Gets or sets the grid dimensions for kernel execution.
    /// </summary>
    public (int X, int Y, int Z) GridDimensions { get; set; } = (1, 1, 1);


    /// <summary>
    /// Gets or sets the block dimensions for kernel execution.
    /// </summary>
    public (int X, int Y, int Z) BlockDimensions { get; set; } = (1, 1, 1);


    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemoryBytes { get; set; }


    /// <summary>
    /// Gets or sets whether to enable profiling for this execution.
    /// </summary>
    public bool EnableProfiling { get; set; }


    /// <summary>
    /// Gets or sets the stream to execute on (null for default stream).
    /// </summary>
    public object? Stream { get; set; }


    /// <summary>
    /// Gets or sets additional backend-specific options.
    /// </summary>
    public Dictionary<string, object> CustomOptions { get; } = [];
}
