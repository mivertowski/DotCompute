// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Optimization.Types
{
    /// <summary>
    /// CUDA function attributes for kernel analysis.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CudaFuncAttributes
    {
        /// <summary>
        /// Size of shared memory required by the function in bytes.
        /// </summary>
        public ulong SharedSizeBytes;

        /// <summary>
        /// Size of constant memory required by the function in bytes.
        /// </summary>
        public ulong ConstSizeBytes;

        /// <summary>
        /// Size of local memory required by each thread in bytes.
        /// </summary>
        public ulong LocalSizeBytes;

        /// <summary>
        /// Maximum number of threads per block for this function.
        /// </summary>
        public int MaxThreadsPerBlock;

        /// <summary>
        /// Number of registers used by each thread.
        /// </summary>
        public int NumRegs;

        /// <summary>
        /// PTX version of the function.
        /// </summary>
        public int PtxVersion;

        /// <summary>
        /// Binary version of the function.
        /// </summary>
        public int BinaryVersion;

        /// <summary>
        /// Pointer to cache mode configuration.
        /// </summary>
        public IntPtr CacheModeCA;

        /// <summary>
        /// Maximum dynamic shared memory size in bytes.
        /// </summary>
        public int MaxDynamicSharedSizeBytes;

        /// <summary>
        /// Preferred shared memory carveout percentage.
        /// </summary>
        public int PreferredShmemCarveout;

        /// <summary>
        /// Pointer to the function name.
        /// </summary>
        public IntPtr Name;
    }
}