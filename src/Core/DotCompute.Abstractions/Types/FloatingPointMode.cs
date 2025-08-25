// <copyright file="FloatingPointMode.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Specifies the floating-point precision and behavior mode.
/// </summary>
public enum FloatingPointMode
{
    /// <summary>
    /// Strict IEEE 754 compliance.
    /// </summary>
    Strict,

    /// <summary>
    /// Relaxed precision for better performance.
    /// </summary>
    Relaxed,

    /// <summary>
    /// Fast math mode with aggressive optimizations.
    /// </summary>
    Fast,

    /// <summary>
    /// Default mode determined by the backend.
    /// </summary>
    Default
}
