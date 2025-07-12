// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel
{
    /// <summary>
    /// Accelerator types supported by the kernel generator.
    /// This is a copy of the AcceleratorType from DotCompute.Abstractions
    /// to avoid netstandard2.0 compatibility issues.
    /// </summary>
    internal enum AcceleratorType
    {
        /// <summary>
        /// CPU backend with SIMD support
        /// </summary>
        CPU = 0,

        /// <summary>
        /// NVIDIA CUDA GPU backend
        /// </summary>
        CUDA = 1,

        /// <summary>
        /// Apple Metal GPU backend
        /// </summary>
        Metal = 2,

        /// <summary>
        /// OpenCL backend
        /// </summary>
        OpenCL = 3,

        /// <summary>
        /// Intel OneAPI backend
        /// </summary>
        OneAPI = 4,

        /// <summary>
        /// AMD ROCm backend
        /// </summary>
        ROCm = 5,

        /// <summary>
        /// Vulkan compute backend
        /// </summary>
        Vulkan = 6,

        /// <summary>
        /// WebGPU backend
        /// </summary>
        WebGPU = 7,

        /// <summary>
        /// Custom backend
        /// </summary>
        Custom = 99
    }
}