// <copyright file="KernelExecutionParameters.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;

namespace DotCompute.Linq.Operators.Models;

/// <summary>
/// Contains parameters for kernel execution.
/// </summary>
public class KernelExecutionParameters
{
    /// <summary>
    /// Gets or sets the global work size dimensions.
    /// </summary>
    public int[]? GlobalWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the local work size dimensions.
    /// </summary>
    public int[]? LocalWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the kernel arguments.
    /// </summary>
    public Dictionary<string, object>? Arguments { get; set; }

    /// <summary>
    /// Gets or sets the kernel parameters (for backwards compatibility).
    /// </summary>
    public object[]? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the CUDA stream pointer.
    /// </summary>
    public IntPtr Stream { get; set; }
}