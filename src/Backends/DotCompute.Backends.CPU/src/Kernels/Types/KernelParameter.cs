// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Types;

/// <summary>
/// Represents a single parameter definition for a kernel, including its name,
/// data type, and scope information.
/// </summary>
/// <remarks>
/// This class describes the parameters that a kernel expects to receive during
/// execution. It includes type information for validation and scope information
/// to distinguish between local and global memory parameters.
/// </remarks>
internal class KernelParameter
{
    /// <summary>
    /// Gets or sets the name of the parameter.
    /// </summary>
    /// <remarks>
    /// The parameter name as defined in the kernel source code.
    /// Used for identification and debugging purposes.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data type of the parameter.
    /// </summary>
    /// <remarks>
    /// The parameter type as a string representation (e.g., "float*", "int", "__global float*").
    /// Used for type validation and argument matching during kernel execution.
    /// </remarks>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this parameter refers to global memory.
    /// </summary>
    /// <remarks>
    /// Global parameters typically represent memory buffers that can be accessed
    /// across all work items in a kernel execution. This affects how the parameter
    /// is handled during argument binding and memory management.
    /// </remarks>
    public bool IsGlobal { get; set; }
}