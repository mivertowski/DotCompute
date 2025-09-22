// <copyright file="OptimizationLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators.Models;
{
/// <summary>
/// Defines optimization levels for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// Debug mode with no optimizations.
    /// </summary>
    Debug,
    /// Default optimization level.
    Default,
    /// Basic optimization level (-O1 equivalent).
    O1,
    /// Standard optimization level (-O2 equivalent).
    O2,
    /// Advanced optimization level (-O3 equivalent).
    O3,
    /// Release mode with standard optimizations.
    Release,
    /// Aggressive optimization mode.
    Aggressive,
    /// Balanced optimization mode.
    Balanced
}
