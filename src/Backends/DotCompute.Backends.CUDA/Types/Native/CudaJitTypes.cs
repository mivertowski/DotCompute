// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Input types for cuLink APIs
    /// </summary>
    public enum CUjitInputType
    {
        /// <summary>
        /// Compiled device-class-specific device code (cubin)
        /// </summary>
        CU_JIT_INPUT_CUBIN = 0,

        /// <summary>
        /// PTX source code
        /// </summary>
        CU_JIT_INPUT_PTX = 1,

        /// <summary>
        /// Bundle of multiple cubins and/or PTX of some device code
        /// </summary>
        CU_JIT_INPUT_FATBINARY = 2,

        /// <summary>
        /// Host object with embedded device code
        /// </summary>
        CU_JIT_INPUT_OBJECT = 3,

        /// <summary>
        /// Archive of host objects with embedded device code
        /// </summary>
        CU_JIT_INPUT_LIBRARY = 4,

        /// <summary>
        /// High-level intermediate code for link-time optimization
        /// </summary>
        CU_JIT_INPUT_NVVM = 5
    }

    /// <summary>
    /// JIT compiler options
    /// </summary>
    public enum CUjit_option
    {
        /// <summary>
        /// Max number of registers that a thread may use
        /// </summary>
        CU_JIT_MAX_REGISTERS = 0,

        /// <summary>
        /// Specifies minimum number of threads per block to target compilation for
        /// </summary>
        CU_JIT_THREADS_PER_BLOCK = 1,

        /// <summary>
        /// Overwrites the option value with the total wall clock time
        /// </summary>
        CU_JIT_WALL_TIME = 2,

        /// <summary>
        /// Pointer to a buffer in which to print any info/error log messages
        /// </summary>
        CU_JIT_INFO_LOG_BUFFER = 3,

        /// <summary>
        /// Size in bytes of the buffer
        /// </summary>
        CU_JIT_INFO_LOG_BUFFER_SIZE_BYTES = 4,

        /// <summary>
        /// Pointer to a buffer in which to print any error log messages
        /// </summary>
        CU_JIT_ERROR_LOG_BUFFER = 5,

        /// <summary>
        /// Size in bytes of the buffer
        /// </summary>
        CU_JIT_ERROR_LOG_BUFFER_SIZE_BYTES = 6,

        /// <summary>
        /// Level of optimization to apply to generated code (0 - 4), with 4 being the default
        /// </summary>
        CU_JIT_OPTIMIZATION_LEVEL = 7,

        /// <summary>
        /// No option value required. Determines the target based on the current attached context
        /// </summary>
        CU_JIT_TARGET_FROM_CUCONTEXT = 8,

        /// <summary>
        /// Target is chosen based on supplied CUjit_target
        /// </summary>
        CU_JIT_TARGET = 9,

        /// <summary>
        /// Specifies choice of fallback strategy if matching cubin is not found
        /// </summary>
        CU_JIT_FALLBACK_STRATEGY = 10,

        /// <summary>
        /// Specifies whether to create debug information in output
        /// </summary>
        CU_JIT_GENERATE_DEBUG_INFO = 11,

        /// <summary>
        /// Generate verbose log messages
        /// </summary>
        CU_JIT_LOG_VERBOSE = 12,

        /// <summary>
        /// Generate line number information
        /// </summary>
        CU_JIT_GENERATE_LINE_INFO = 13,

        /// <summary>
        /// Specifies whether to enable caching explicitly
        /// </summary>
        CU_JIT_CACHE_MODE = 14,

        /// <summary>
        /// Device to use for compilation when using CU_JIT_LTO
        /// </summary>
        CU_JIT_NEW_SM3X_OPT = 15,

        /// <summary>
        /// Use fast math operations
        /// </summary>
        CU_JIT_FAST_COMPILE = 16,

        /// <summary>
        /// Array of device symbol names to be relocated
        /// </summary>
        CU_JIT_GLOBAL_SYMBOL_NAMES = 17,

        /// <summary>
        /// Array of host addresses to be used for relocated device symbols
        /// </summary>
        CU_JIT_GLOBAL_SYMBOL_ADDRESSES = 18,

        /// <summary>
        /// Number of entries in arrays for CU_JIT_GLOBAL_SYMBOL_NAMES and CU_JIT_GLOBAL_SYMBOL_ADDRESSES
        /// </summary>
        CU_JIT_GLOBAL_SYMBOL_COUNT = 19,

        /// <summary>
        /// Enable Link Time Optimization for device code
        /// </summary>
        CU_JIT_LTO = 20,

        /// <summary>
        /// Enable position-independent code generation
        /// </summary>
        CU_JIT_FTZ = 21,

        /// <summary>
        /// Control single-precision floating-point division and reciprocals
        /// </summary>
        CU_JIT_PREC_DIV = 22,

        /// <summary>
        /// Control single-precision floating-point square root
        /// </summary>
        CU_JIT_PREC_SQRT = 23,

        /// <summary>
        /// Enable floating-point multiplication and addition/subtraction operations fusion
        /// </summary>
        CU_JIT_FMA = 24
    }

    /// <summary>
    /// JIT compilation targets
    /// </summary>
    public enum CUjit_target
    {
        /// <summary>
        /// Compute device class 2.0
        /// </summary>
        CU_TARGET_COMPUTE_20 = 20,

        /// <summary>
        /// Compute device class 2.1
        /// </summary>
        CU_TARGET_COMPUTE_21 = 21,

        /// <summary>
        /// Compute device class 3.0
        /// </summary>
        CU_TARGET_COMPUTE_30 = 30,

        /// <summary>
        /// Compute device class 3.2
        /// </summary>
        CU_TARGET_COMPUTE_32 = 32,

        /// <summary>
        /// Compute device class 3.5
        /// </summary>
        CU_TARGET_COMPUTE_35 = 35,

        /// <summary>
        /// Compute device class 3.7
        /// </summary>
        CU_TARGET_COMPUTE_37 = 37,

        /// <summary>
        /// Compute device class 5.0
        /// </summary>
        CU_TARGET_COMPUTE_50 = 50,

        /// <summary>
        /// Compute device class 5.2
        /// </summary>
        CU_TARGET_COMPUTE_52 = 52,

        /// <summary>
        /// Compute device class 5.3
        /// </summary>
        CU_TARGET_COMPUTE_53 = 53,

        /// <summary>
        /// Compute device class 6.0
        /// </summary>
        CU_TARGET_COMPUTE_60 = 60,

        /// <summary>
        /// Compute device class 6.1
        /// </summary>
        CU_TARGET_COMPUTE_61 = 61,

        /// <summary>
        /// Compute device class 6.2
        /// </summary>
        CU_TARGET_COMPUTE_62 = 62,

        /// <summary>
        /// Compute device class 7.0
        /// </summary>
        CU_TARGET_COMPUTE_70 = 70,

        /// <summary>
        /// Compute device class 7.2
        /// </summary>
        CU_TARGET_COMPUTE_72 = 72,

        /// <summary>
        /// Compute device class 7.5
        /// </summary>
        CU_TARGET_COMPUTE_75 = 75,

        /// <summary>
        /// Compute device class 8.0
        /// </summary>
        CU_TARGET_COMPUTE_80 = 80,

        /// <summary>
        /// Compute device class 8.6
        /// </summary>
        CU_TARGET_COMPUTE_86 = 86,

        /// <summary>
        /// Compute device class 8.7
        /// </summary>
        CU_TARGET_COMPUTE_87 = 87,

        /// <summary>
        /// Compute device class 8.9
        /// </summary>
        CU_TARGET_COMPUTE_89 = 89,

        /// <summary>
        /// Compute device class 9.0
        /// </summary>
        CU_TARGET_COMPUTE_90 = 90
    }

    /// <summary>
    /// JIT fallback strategies
    /// </summary>
    public enum CUjit_fallback_strategy
    {
        /// <summary>
        /// Prefer to compile PTX if exact binary match not found
        /// </summary>
        CU_PREFER_PTX = 0,

        /// <summary>
        /// Prefer to fall back to compatible binary code if exact match not found
        /// </summary>
        CU_PREFER_BINARY = 1
    }

    /// <summary>
    /// JIT cache modes
    /// </summary>
    public enum CUjit_cacheMode
    {
        /// <summary>
        /// Default cache mode
        /// </summary>
        CU_JIT_CACHE_OPTION_NONE = 0,

        /// <summary>
        /// Enable caching
        /// </summary>
        CU_JIT_CACHE_OPTION_CG = 1,

        /// <summary>
        /// Disable caching
        /// </summary>
        CU_JIT_CACHE_OPTION_CA = 2
    }
}