// <copyright file="OptimizationLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators.Models;

/// <summary>
/// Defines the optimization level for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// Debug mode with minimal optimizations for easier debugging.
    /// </summary>
    Debug = 0,

    /// <summary>
    /// Default optimization level with balanced performance and compile time.
    /// </summary>
    Default = 1,

    /// <summary>
    /// Release mode with standard optimizations.
    /// </summary>
    Release = 2,

    /// <summary>
    /// Aggressive optimization mode that may increase compile time significantly.
    /// </summary>
    Aggressive = 3
}