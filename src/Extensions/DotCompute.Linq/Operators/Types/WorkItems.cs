// <copyright file="WorkItems.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators.Types;

/// <summary>
/// Defines work dimensions for kernel execution.
/// </summary>
public class WorkItems
{
    /// <summary>
    /// Gets or sets the global work size dimensions.
    /// </summary>
    public int[] GlobalWorkSize { get; set; } = [1];

    /// <summary>
    /// Gets or sets the local work size dimensions.
    /// </summary>
    public int[]? LocalWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the work offset.
    /// </summary>
    public int[]? Offset { get; set; }
}