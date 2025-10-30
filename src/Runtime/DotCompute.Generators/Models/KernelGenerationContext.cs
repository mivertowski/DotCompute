// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Models;

/// <summary>
/// Context for kernel generation in source generators.
/// </summary>
public sealed class KernelGenerationContext
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target accelerator type.
    /// </summary>
    public string AcceleratorType { get; set; } = "CPU";

    /// <summary>
    /// Gets or sets whether to enable vectorization.
    /// </summary>
    public bool EnableVectorization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable loop unrolling.
    /// </summary>
    public bool EnableLoopUnrolling { get; set; } = true;

    /// <summary>
    /// Gets or sets the work group dimensions.
    /// </summary>
    public IReadOnlyList<int>? WorkGroupDimensions { get; set; }

    /// <summary>
    /// Gets or sets whether to use shared memory.
    /// </summary>
    public bool UseSharedMemory { get; set; }

    /// <summary>
    /// Gets or sets whether to use vector types.
    /// </summary>
    public bool UseVectorTypes { get; set; }

    /// <summary>
    /// Gets additional metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; }
}
