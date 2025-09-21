// <copyright file="PrecisionMode.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators.Generation;
/// <summary>
/// Defines precision modes for floating-point kernel operations.
/// </summary>
public enum PrecisionMode
{
    /// <summary>
    /// Default precision based on the accelerator's native mode.
    /// </summary>
    Default,
    /// High precision mode with strict IEEE-754 compliance.
    High,
    /// Fast mode with reduced precision for better performance.
    Fast,
    /// Mixed precision mode allowing different precision levels within the same kernel.
    Mixed
}
