// <copyright file="OptimizationLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Kernels.Types;

/// <summary>
/// Defines the optimization level for kernel compilation.
/// Different levels provide trade-offs between compilation time, code size, and runtime performance.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization. Fastest compilation, largest code size, slowest runtime.
    /// Useful for debugging and development.
    /// </summary>
    O0,

    /// <summary>
    /// Basic optimization. Moderate compilation time with basic performance improvements.
    /// Suitable for debug builds with reasonable performance.
    /// </summary>
    O1,

    /// <summary>
    /// Standard optimization. Balanced compilation time and runtime performance.
    /// Recommended for most production scenarios.
    /// </summary>
    O2,

    /// <summary>
    /// Aggressive optimization. Longer compilation time for maximum runtime performance.
    /// May increase code size but provides best performance.
    /// </summary>
    O3,

    /// <summary>
    /// Size optimization. Optimizes for minimal code size rather than speed.
    /// Useful for memory-constrained environments.
    /// </summary>
    Os
}