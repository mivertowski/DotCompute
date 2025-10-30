// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Enums;

/// <summary>
/// Compute intensity level for CPU kernel optimization.
/// </summary>
public enum ComputeIntensity
{
    /// <summary>
    /// Low computational complexity - simple operations.
    /// </summary>
    Low,

    /// <summary>
    /// Medium computational complexity - moderate operations.
    /// </summary>
    Medium,

    /// <summary>
    /// High computational complexity - complex operations.
    /// </summary>
    High,

    /// <summary>
    /// Very high computational complexity - intensive operations.
    /// </summary>
    VeryHigh
}
