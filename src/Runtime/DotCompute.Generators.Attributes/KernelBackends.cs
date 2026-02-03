// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Generators;

/// <summary>
/// Defines the execution backends supported by compute kernels.
/// </summary>
/// <remarks>
/// This enumeration uses flags to allow a kernel to target multiple backends simultaneously.
/// The code generator will produce optimized implementations for each selected backend.
/// </remarks>
[Flags]
public enum KernelBackends
{
    /// <summary>
    /// CPU backend with SIMD support.
    /// </summary>
    /// <remarks>
    /// Generates optimized CPU code using SIMD instructions (SSE, AVX, NEON)
    /// and multi-threading for parallel execution. This is the most portable
    /// backend and serves as the fallback for all kernels.
    /// </remarks>
    CPU = 1,

    /// <summary>
    /// NVIDIA CUDA backend.
    /// </summary>
    /// <remarks>
    /// Generates CUDA kernels for execution on NVIDIA GPUs.
    /// Provides access to GPU-specific features like shared memory,
    /// warp-level primitives, and tensor cores on supported hardware.
    /// </remarks>
    CUDA = 2,

    /// <summary>
    /// Apple Metal backend.
    /// </summary>
    /// <remarks>
    /// Generates Metal compute shaders for execution on Apple GPUs.
    /// Optimized for Apple Silicon and provides efficient integration
    /// with the Apple ecosystem and Metal Performance Shaders.
    /// </remarks>
    Metal = 4,

    /// <summary>
    /// OpenCL backend.
    /// </summary>
    /// <remarks>
    /// Generates OpenCL kernels for cross-platform GPU execution.
    /// Provides broad hardware compatibility across different vendors
    /// including AMD, Intel, and NVIDIA GPUs.
    /// </remarks>
    OpenCL = 8,

    /// <summary>
    /// All available backends.
    /// </summary>
    /// <remarks>
    /// Convenience value that targets all supported backends.
    /// The runtime will automatically select the best available
    /// backend based on the current hardware configuration.
    /// </remarks>
    All = CPU | CUDA | Metal | OpenCL
}
