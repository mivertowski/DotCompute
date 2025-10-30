// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Defines the type of accelerator device.
    /// </summary>
    public enum AcceleratorType
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,

        /// <summary>
        /// Auto-detect the best available accelerator.
        /// </summary>
        Auto = -1,

        /// <summary>
        /// CPU-based computation.
        /// </summary>
        CPU = 1,

        /// <summary>
        /// NVIDIA CUDA GPU.
        /// </summary>
        CUDA = 2,

        /// <summary>
        /// AMD ROCm GPU.
        /// </summary>
        ROCm = 3,

        /// <summary>
        /// Intel oneAPI GPU.
        /// </summary>
        OneAPI = 4,

        /// <summary>
        /// Apple Metal GPU.
        /// </summary>
        Metal = 5,

        /// <summary>
        /// OpenCL-compatible device.
        /// </summary>
        OpenCL = 6,

        /// <summary>
        /// DirectML-compatible device.
        /// </summary>
        DirectML = 7,

        /// <summary>
        /// DirectCompute-compatible device.
        /// </summary>
        DirectCompute = 8,

        /// <summary>
        /// Generic GPU device.
        /// </summary>
        GPU = 9,

        /// <summary>
        /// Field-Programmable Gate Array.
        /// </summary>
        FPGA = 10,

        /// <summary>
        /// Tensor Processing Unit.
        /// </summary>
        TPU = 11,

        /// <summary>
        /// Custom or unknown accelerator type.
        /// </summary>
        Custom = 100
    }
}
