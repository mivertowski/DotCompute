// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// GPU architecture types for optimization
/// </summary>
public enum MetalGpuArchitecture
{
    /// <summary>
    /// Unknown or unsupported architecture
    /// </summary>
    Unknown,

    /// <summary>
    /// Apple Silicon M1 family
    /// </summary>
    AppleM1,

    /// <summary>
    /// Apple Silicon M2 family
    /// </summary>
    AppleM2,

    /// <summary>
    /// Apple Silicon M3 family
    /// </summary>
    AppleM3,

    /// <summary>
    /// Intel Integrated Graphics
    /// </summary>
    IntelIntegrated,

    /// <summary>
    /// AMD Discrete GPU
    /// </summary>
    AmdDiscrete,

    /// <summary>
    /// Intel Discrete GPU
    /// </summary>
    IntelDiscrete,

    /// <summary>
    /// Other discrete GPU
    /// </summary>
    OtherDiscrete
}
