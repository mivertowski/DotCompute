// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Specifies the type of compute device used for pipeline execution.
/// </summary>
public enum ComputeDeviceType
{
    /// <summary>
    /// Unknown or unspecified device type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Central Processing Unit with general-purpose computing capabilities.
    /// Supports multi-core parallel execution and SIMD vectorization.
    /// </summary>
    CPU,

    /// <summary>
    /// NVIDIA Graphics Processing Unit with CUDA support.
    /// Optimized for massively parallel computations.
    /// </summary>
    CUDA,

    /// <summary>
    /// AMD Graphics Processing Unit with ROCm/HIP support.
    /// Alternative GPU platform for parallel computing.
    /// </summary>
    ROCm,

    /// <summary>
    /// Apple Metal Performance Shaders on macOS and iOS devices.
    /// Native GPU computing framework for Apple platforms.
    /// </summary>
    Metal,

    /// <summary>
    /// OpenCL-compatible device supporting cross-platform parallel computing.
    /// Can target CPUs, GPUs, and other accelerators.
    /// </summary>
    OpenCL,

    /// <summary>
    /// Intel Graphics integrated or discrete GPU with OneAPI support.
    /// Intel's unified programming model for heterogeneous computing.
    /// </summary>
    IntelGPU,

    /// <summary>
    /// Field-Programmable Gate Array for custom hardware acceleration.
    /// Provides reconfigurable computing capabilities.
    /// </summary>
    FPGA,

    /// <summary>
    /// Application-Specific Integrated Circuit for specialized computations.
    /// Fixed-function hardware optimized for specific algorithms.
    /// </summary>
    ASIC,

    /// <summary>
    /// Digital Signal Processor optimized for signal processing algorithms.
    /// Specialized for real-time audio and video processing.
    /// </summary>
    DSP,

    /// <summary>
    /// Neural Processing Unit specialized for machine learning workloads.
    /// Optimized for tensor operations and neural network inference.
    /// </summary>
    NPU,

    /// <summary>
    /// Tensor Processing Unit designed for machine learning acceleration.
    /// Google's custom silicon for AI workloads.
    /// </summary>
    TPU,

    /// <summary>
    /// ARM-based processor with NEON SIMD capabilities.
    /// Common in mobile and embedded devices.
    /// </summary>
    ARM,

    /// <summary>
    /// RISC-V processor with open-source instruction set architecture.
    /// Emerging platform for custom and embedded computing.
    /// </summary>
    RISCV,

    /// <summary>
    /// WebAssembly runtime for portable, sandboxed execution.
    /// Cross-platform bytecode format for web and edge computing.
    /// </summary>
    WebAssembly,

    /// <summary>
    /// Edge computing device with specialized AI acceleration.
    /// Optimized for low-power, real-time inference.
    /// </summary>
    EdgeAI,

    /// <summary>
    /// Quantum processing unit for quantum computing algorithms.
    /// Experimental platform for quantum advantage applications.
    /// </summary>
    QPU,

    /// <summary>
    /// Hybrid device combining multiple compute types.
    /// Heterogeneous computing with automatic work distribution.
    /// </summary>
    Hybrid,

    /// <summary>
    /// Cloud-based virtual compute resource.
    /// Elastic computing with pay-per-use pricing model.
    /// </summary>
    Cloud,

    /// <summary>
    /// Distributed computing cluster across multiple nodes.
    /// Scalable computing for large-scale data processing.
    /// </summary>
    Cluster,

    /// <summary>
    /// Any available device, letting the system choose automatically.
    /// Adaptive selection based on workload characteristics.
    /// </summary>
    Auto
}