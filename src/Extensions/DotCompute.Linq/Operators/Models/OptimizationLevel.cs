// <copyright file="OptimizationLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators.Models;

/// <summary>
/// Defines optimization levels for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// Debug mode with no optimizations.
    /// </summary>
    Debug,

    /// <summary>
    /// Default optimization level.
    /// </summary>
    Default,

    /// <summary>
    /// Release mode with standard optimizations.
    /// </summary>
    Release,

    /// <summary>
    /// Aggressive optimization mode.
    /// </summary>
    Aggressive
}