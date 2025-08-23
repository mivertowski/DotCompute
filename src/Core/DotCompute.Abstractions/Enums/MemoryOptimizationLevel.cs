// <copyright file="MemoryOptimizationLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Enums;

/// <summary>
/// Specifies the memory optimization level for kernel execution.
/// </summary>
public enum MemoryOptimizationLevel
{
    /// <summary>
    /// No memory optimizations. Uses default memory allocation strategies.
    /// </summary>
    None = 0,

    /// <summary>
    /// Conservative memory optimizations. Minimal memory reuse, safer for debugging.
    /// </summary>
    Conservative = 1,

    /// <summary>
    /// Balanced memory optimizations. Standard memory pooling and reuse.
    /// </summary>
    Balanced = 2,

    /// <summary>
    /// Aggressive memory optimizations. Maximum memory reuse and pooling.
    /// </summary>
    Aggressive = 3
}