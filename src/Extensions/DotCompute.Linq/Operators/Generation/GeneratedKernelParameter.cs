// <copyright file="GeneratedKernelParameter.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;

namespace DotCompute.Linq.Operators.Generation;

/// <summary>
/// Represents a parameter in a generated kernel.
/// </summary>
public class GeneratedKernelParameter
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter type.
    /// </summary>
    public Type Type { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets a value indicating whether this is an input parameter.
    /// </summary>
    public bool IsInput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this is an output parameter.
    /// </summary>
    public bool IsOutput { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes (for buffer parameters).
    /// </summary>
    public int SizeInBytes { get; set; }

    /// <summary>
    /// Gets or sets the element count (for array/buffer parameters).
    /// </summary>
    public int ElementCount { get; set; }

    /// <summary>
    /// Gets or sets the element type (for array/buffer parameters).
    /// </summary>
    public Type? ElementType { get; set; }
}

