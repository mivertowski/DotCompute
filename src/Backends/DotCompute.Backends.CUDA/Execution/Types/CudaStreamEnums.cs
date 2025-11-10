// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Types;

/// <summary>
/// Flags controlling CUDA stream behavior.
/// </summary>
public enum CudaStreamFlags
{
    /// <summary>Default blocking stream behavior.</summary>
    Default,
    /// <summary>Non-blocking stream allowing concurrent execution.</summary>
    NonBlocking
}

/// <summary>
/// Priority levels for CUDA stream scheduling.
/// </summary>
/// <remarks>
/// CUDA driver supports integer priorities where lower values indicate higher priority.
/// Use High for latency-critical operations, Low for background work.
/// </remarks>
public enum CudaStreamPriority
{
    /// <summary>Low priority for background processing.</summary>
    Low,
    /// <summary>Normal priority for typical operations.</summary>
    Normal,
    /// <summary>High priority for latency-sensitive operations.</summary>
    High
}
