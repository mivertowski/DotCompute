// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Types;

/// <summary>
/// Contains metadata and information about a kernel including its source code,
/// type classification, and parameter definitions.
/// </summary>
/// <remarks>
/// This class serves as a container for kernel metadata used by the CPU backend
/// to analyze, categorize, and execute kernels appropriately. It includes the
/// kernel source code for pattern analysis and optimization selection.
/// </remarks>
internal class KernelInfo
{
    /// <summary>
    /// Gets or sets the name of the kernel.
    /// </summary>
    /// <remarks>
    /// The kernel name is used for identification and logging purposes.
    /// Should be unique within the context of a compilation unit.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type classification of the kernel.
    /// </summary>
    /// <remarks>
    /// The kernel type determines which optimized implementation will be used
    /// for execution. This affects performance characteristics and optimization strategies.
    /// </remarks>
    public KernelType Type { get; set; }

    /// <summary>
    /// Gets or sets the source code of the kernel.
    /// </summary>
    /// <remarks>
    /// The source code is used for pattern analysis in generic kernels to infer
    /// execution strategies when specific optimized implementations are not available.
    /// May contain OpenCL C, CUDA, or other kernel language source code.
    /// </remarks>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of parameters defined for the kernel.
    /// </summary>
    /// <remarks>
    /// Parameter information is used for argument validation and type checking
    /// during kernel execution. Each parameter includes name, type, and scope information.
    /// </remarks>
    public List<KernelParameter> Parameters { get; set; } = [];
}