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
    /// Gets or sets the parameter type.
    public Type Type { get; set; } = typeof(object);
    /// Gets or sets a value indicating whether this is an input parameter.
    public bool IsInput { get; set; } = true;
    /// Gets or sets a value indicating whether this is an output parameter.
    public bool IsOutput { get; set; }
    /// Gets or sets the size in bytes (for buffer parameters).
    public int SizeInBytes { get; set; }
    /// Gets or sets the element count (for array/buffer parameters).
    public int ElementCount { get; set; }
    /// Gets or sets the element type (for array/buffer parameters).
    public Type? ElementType { get; set; }
}
