// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators;

/// <summary>
/// Defines the available compute backends for kernel execution.
/// </summary>
/// <remarks>
/// <para>
/// Each backend represents a different GPU/CPU compute architecture.
/// The runtime automatically selects the optimal backend based on
/// hardware availability and kernel requirements.
/// </para>
/// <para>
/// <strong>Backend Selection Priority:</strong>
/// <list type="number">
/// <item><description>CUDA (NVIDIA GPUs) - highest performance for NVIDIA hardware</description></item>
/// <item><description>Metal (Apple Silicon) - optimal for macOS/iOS</description></item>
/// <item><description>OpenCL (cross-platform) - broad hardware support</description></item>
/// <item><description>CPU (SIMD) - universal fallback with vectorization</description></item>
/// </list>
/// </para>
/// </remarks>
public enum ComputeBackend
{
    /// <summary>
    /// CPU backend using SIMD (SSE, AVX, NEON) vectorization.
    /// </summary>
    /// <remarks>
    /// Always available. Provides good performance for vectorizable
    /// algorithms using hardware SIMD instructions.
    /// </remarks>
    CPU = 0,

    /// <summary>
    /// NVIDIA CUDA backend for GeForce, RTX, and Tesla GPUs.
    /// </summary>
    /// <remarks>
    /// Requires NVIDIA GPU and CUDA drivers. Provides excellent
    /// performance for massively parallel workloads.
    /// </remarks>
    CUDA = 1,

    /// <summary>
    /// OpenCL backend for cross-platform GPU/CPU compute.
    /// </summary>
    /// <remarks>
    /// Supports AMD, Intel, and NVIDIA GPUs, as well as CPU devices.
    /// Provides portable compute across different vendors.
    /// </remarks>
    OpenCL = 2,

    /// <summary>
    /// Apple Metal backend for macOS, iOS, and Apple Silicon.
    /// </summary>
    /// <remarks>
    /// Optimized for Apple hardware with unified memory architecture.
    /// Provides excellent performance on M1/M2/M3 chips.
    /// </remarks>
    Metal = 3,

    /// <summary>
    /// Vulkan Compute backend using compute shaders.
    /// </summary>
    /// <remarks>
    /// Modern low-level API with explicit resource control.
    /// Supports a wide range of GPU vendors.
    /// </remarks>
    Vulkan = 4,

    /// <summary>
    /// AMD ROCm backend for Radeon and Instinct GPUs.
    /// </summary>
    /// <remarks>
    /// AMD's open-source GPU compute platform with HIP.
    /// Provides excellent performance for AMD hardware.
    /// </remarks>
    ROCm = 5
}
