// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

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
        /// The shared size bytes.
        /// </summary>
        /// <summary>
        /// Size of shared memory required by the function in bytes.
        /// </summary>
        public ulong SharedSizeBytes;
        /// <summary>
        /// The const size bytes.
        /// </summary>

        /// <summary>
        /// Size of constant memory required by the function in bytes.
        /// </summary>
        public ulong ConstSizeBytes;
        /// <summary>
        /// The local size bytes.
        /// </summary>

        /// <summary>
        /// Size of local memory required by each thread in bytes.
        /// </summary>
        public ulong LocalSizeBytes;
        /// <summary>
        /// The max threads per block.
        /// </summary>

        /// <summary>
        /// Maximum number of threads per block for this function.
        /// </summary>
        public int MaxThreadsPerBlock;
        /// <summary>
        /// The num regs.
        /// </summary>

        /// <summary>
        /// Number of registers used by each thread.
        /// </summary>
        public int NumRegs;
        /// <summary>
        /// The ptx version.
        /// </summary>

        /// <summary>
        /// PTX version of the function.
        /// </summary>
        public int PtxVersion;
        /// <summary>
        /// The binary version.
        /// </summary>

        /// <summary>
        /// Binary version of the function.
        /// </summary>
        public int BinaryVersion;
        /// <summary>
        /// The cache mode c a.
        /// </summary>

        /// <summary>
        /// Pointer to cache mode configuration.
        /// </summary>
        public IntPtr CacheModeCA;
        /// <summary>
        /// The max dynamic shared size bytes.
        /// </summary>

        /// <summary>
        /// Maximum dynamic shared memory size in bytes.
        /// </summary>
        public int MaxDynamicSharedSizeBytes;
        /// <summary>
        /// The preferred shmem carveout.
        /// </summary>

        /// <summary>
        /// Preferred shared memory carveout percentage.
        /// </summary>
        public int PreferredShmemCarveout;
        /// <summary>
        /// The name.
        /// </summary>

        /// <summary>
        /// Pointer to the function name.
        /// </summary>
        public IntPtr Name;
    }
}