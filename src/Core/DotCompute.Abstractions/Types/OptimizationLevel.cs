// <copyright file="OptimizationLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Specifies the optimization level for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimizations. Best for debugging.
    /// </summary>
    None = 0,

    /// <summary>
    /// Minimal optimizations for faster compilation.
    /// </summary>
    Minimal = 1,

    /// <summary>
    /// Default optimization level. Balanced between compilation time and performance.
    /// </summary>
    Default = 2,

    /// <summary>
    /// High optimization level. Longer compilation time but better performance.
    /// </summary>
    High = 3,

    /// <summary>
    /// Maximum optimizations. Longest compilation time but best performance.
    /// </summary>
    Maximum = 4,

    /// <summary>
    /// Size optimization. Minimize code size.
    /// </summary>
    Size = 5,

    /// <summary>
    /// Aggressive optimizations that may alter floating-point behavior.
    /// </summary>
    Aggressive = 6,

    /// <summary>
    /// Custom optimization level defined by backend-specific flags.
    /// </summary>
    Custom = 7
}
