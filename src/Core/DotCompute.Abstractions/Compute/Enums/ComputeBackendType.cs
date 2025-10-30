// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Compute.Enums
{
    /// <summary>
    /// Defines the available compute backend types for kernel execution.
    /// Each backend represents a different compute architecture and programming model.
    /// </summary>
    /// <remarks>
    /// The choice of backend affects performance characteristics, memory access patterns,
    /// and API compatibility. Some backends may not be available on all systems depending
    /// on hardware capabilities and driver availability.
    /// </remarks>
    public enum ComputeBackendType
    {
        /// <summary>
        /// CPU backend using SIMD (Single Instruction, Multiple Data) instructions.
        /// </summary>
        /// <remarks>
        /// This backend leverages CPU vector instructions (SSE, AVX, NEON) for parallel
        /// computation. It provides good performance for algorithms that can be vectorized
        /// and is available on all systems. Memory access is through system RAM with
        /// cache hierarchy optimization.
        /// </remarks>
        CPU,

        /// <summary>
        /// CUDA backend for NVIDIA GPUs using the CUDA Compute Capability architecture.
        /// </summary>
        /// <remarks>
        /// Requires NVIDIA graphics hardware and CUDA drivers. Provides excellent performance
        /// for massively parallel workloads with thousands of threads. Features include
        /// shared memory, texture memory, and advanced memory coalescing optimizations.
        /// Only available on systems with compatible NVIDIA hardware.
        /// </remarks>
        CUDA,

        /// <summary>
        /// OpenCL backend for cross-platform heterogeneous computing.
        /// </summary>
        /// <remarks>
        /// Industry-standard API supporting CPUs, GPUs, and other accelerators from multiple
        /// vendors (NVIDIA, AMD, Intel, ARM). Provides portable compute kernels that can
        /// run across different hardware platforms with consistent behavior and performance
        /// characteristics.
        /// </remarks>
        OpenCL,

        /// <summary>
        /// Metal backend for Apple devices including iOS, macOS, and Apple Silicon.
        /// </summary>
        /// <remarks>
        /// Apple's proprietary compute API optimized for Apple hardware ecosystem.
        /// Provides deep integration with Apple's unified memory architecture and
        /// GPU designs. Features include Metal Performance Shaders integration
        /// and optimized memory management for Apple Silicon.
        /// </remarks>
        Metal,

        /// <summary>
        /// Vulkan Compute backend using the Vulkan API's compute shader capabilities.
        /// </summary>
        /// <remarks>
        /// Modern low-level compute API providing explicit control over GPU resources
        /// and memory management. Offers excellent performance through reduced driver
        /// overhead and fine-grained resource control. Supports advanced features like
        /// subgroups, device-local memory, and multi-queue execution.
        /// </remarks>
        Vulkan,

        /// <summary>
        /// DirectCompute backend for Windows platforms using DirectX compute shaders.
        /// </summary>
        /// <remarks>
        /// Microsoft's compute API integrated with DirectX graphics pipeline.
        /// Provides good integration with Windows graphics stack and supports
        /// interoperability with DirectX graphics resources. Features include
        /// structured buffers, unordered access views, and integration with
        /// Windows display drivers.
        /// </remarks>
        DirectCompute,

        /// <summary>
        /// ROCm backend for AMD GPUs using the ROCm platform.
        /// </summary>
        /// <remarks>
        /// AMD's open-source platform for GPU computing with HIP (Heterogeneous-compute
        /// Interface for Portability). Provides excellent performance for AMD Radeon and
        /// Instinct GPUs with features including shared virtual memory, peer-to-peer
        /// transfers, and support for large memory workloads. Compatible with both
        /// consumer and enterprise AMD GPU hardware.
        /// </remarks>
        ROCm
    }
}
