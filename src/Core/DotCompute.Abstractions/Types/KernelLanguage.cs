// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Kernels.Types
{
    /// <summary>
    /// Defines the language or format of kernel source code.
    /// Specifies the programming language, assembly format, or bytecode type
    /// used to represent compute kernel implementations.
    /// </summary>
    public enum KernelLanguage
    {
        /// <summary>
        /// Automatically detect and select the most appropriate kernel language
        /// based on the target platform and available runtime features.
        /// </summary>
        Auto,

        /// <summary>
        /// CUDA C/C++ source code for NVIDIA GPU programming.
        /// Supports parallel computing on NVIDIA graphics processing units.
        /// </summary>
        Cuda,

        /// <summary>
        /// OpenCL C source code for cross-platform parallel computing.
        /// Enables execution across CPUs, GPUs, and other accelerators.
        /// </summary>
        OpenCL,

        /// <summary>
        /// NVIDIA PTX (Parallel Thread Execution) assembly language.
        /// Low-level virtual instruction set for NVIDIA GPUs.
        /// </summary>
        Ptx,

        /// <summary>
        /// HLSL (High-Level Shading Language) shader code for DirectCompute.
        /// Microsoft's shading language for Direct3D compute shaders.
        /// </summary>
        HLSL,

        /// <summary>
        /// SPIR-V (Standard Portable Intermediate Representation - Vulkan) bytecode.
        /// Cross-API intermediate language for parallel compute and graphics.
        /// </summary>
        SPIRV,

        /// <summary>
        /// Metal Shading Language for Apple platforms.
        /// Apple's unified graphics and compute shading language.
        /// </summary>
        Metal,

        /// <summary>
        /// ROCm HIP (Heterogeneous-Compute Interface for Portability) source code.
        /// AMD's runtime API and kernel language for GPU computing.
        /// </summary>
        HIP,

        /// <summary>
        /// SYCL/DPC++ (Data Parallel C++) source code.
        /// Open standard for heterogeneous parallel programming in modern C++.
        /// </summary>
        SYCL,

        /// <summary>
        /// C# Intermediate Language (IL) or expression tree representation.
        /// .NET managed code compiled to bytecode for runtime execution.
        /// </summary>
        CSharpIL,

        /// <summary>
        /// Pre-compiled binary kernel code.
        /// Platform-specific executable binary format ready for direct execution.
        /// </summary>
        Binary,

        /// <summary>
        /// C# source code for managed kernel implementations.
        /// High-level C# code that will be compiled to optimized kernels.
        /// </summary>
        CSharp,

        /// <summary>
        /// DirectCompute shader code for Windows GPU programming.
        /// Microsoft's GPU compute API integrated with Direct3D.
        /// </summary>
        DirectCompute,

        /// <summary>
        /// Vulkan compute shader code.
        /// Cross-platform GPU compute using Vulkan API.
        /// </summary>
        Vulkan,

        /// <summary>
        /// WebGPU shader code for web-based GPU computing.
        /// Modern web standard for GPU access in browsers.
        /// </summary>
        WebGPU
    }
}
