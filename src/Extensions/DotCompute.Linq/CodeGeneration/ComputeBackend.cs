// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Specifies the compute backend to use for kernel execution.
/// </summary>
public enum ComputeBackend
{
    /// <summary>
    /// CPU execution with SIMD vectorization (AVX512/AVX2/NEON).
    /// Always available, lowest transfer overhead.
    /// </summary>
    CpuSimd = 0,

    /// <summary>
    /// NVIDIA CUDA GPU execution.
    /// Requires NVIDIA GPU with Compute Capability 5.0+.
    /// </summary>
    Cuda = 1,

    /// <summary>
    /// Apple Metal GPU execution.
    /// Requires macOS with Metal-capable GPU.
    /// </summary>
    Metal = 2,

    /// <summary>
    /// OpenCL GPU execution (cross-platform).
    /// Supports NVIDIA, AMD, Intel GPUs on Windows/Linux/macOS.
    /// </summary>
    OpenCL = 3
}

/// <summary>
/// Flags indicating available compute backends on the current system.
/// </summary>
[Flags]
public enum AvailableBackends
{
    /// <summary>
    /// No backends available (system error).
    /// </summary>
    None = 0,

    /// <summary>
    /// CPU with SIMD vectorization available (always true).
    /// </summary>
    CpuSimd = 1 << 0,

    /// <summary>
    /// NVIDIA CUDA GPU available.
    /// </summary>
    Cuda = 1 << 1,

    /// <summary>
    /// Apple Metal GPU available.
    /// </summary>
    Metal = 1 << 2,

    /// <summary>
    /// OpenCL GPU available (cross-platform).
    /// </summary>
    OpenCL = 1 << 3,

    /// <summary>
    /// All backends available.
    /// </summary>
    All = CpuSimd | Cuda | Metal | OpenCL
}

/// <summary>
/// Characterizes computational intensity of operations.
/// </summary>
public enum ComputeIntensity
{
    /// <summary>
    /// Simple operations (copy, cast, simple arithmetic).
    /// Better suited for CPU due to low compute-to-memory ratio.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Moderate operations (basic math, simple transforms).
    /// Benefits from parallelization but transfer overhead matters.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// Complex operations (trigonometry, advanced math, multiple operations).
    /// High parallelism benefits, GPU transfer overhead often justified.
    /// </summary>
    High = 2,

    /// <summary>
    /// Very complex operations (multiple nested operations, joins, aggregations).
    /// Strongly benefits from GPU parallelism.
    /// </summary>
    VeryHigh = 3
}

