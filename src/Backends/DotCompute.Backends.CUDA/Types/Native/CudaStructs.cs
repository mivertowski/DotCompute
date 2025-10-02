// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Native.Types
{

    /// <summary>
    /// DEPRECATED: Use DotCompute.Backends.CUDA.Types.Native.CudaKernelNodeParams from CudaGraphTypes.cs instead.
    /// This struct is kept for backward compatibility during migration.
    /// </summary>
    [Obsolete("Use DotCompute.Backends.CUDA.Types.Native.CudaKernelNodeParams from CudaGraphTypes.cs instead.", false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaKernelNodeParams
    {
        /// <summary>
        /// The function.
        /// </summary>
        public IntPtr Function;
        /// <summary>
        /// The grid dim x.
        /// </summary>
        public uint GridDimX, GridDimY, GridDimZ;
        /// <summary>
        /// The block dim x.
        /// </summary>
        public uint BlockDimX, BlockDimY, BlockDimZ;
        /// <summary>
        /// The shared mem bytes.
        /// </summary>
        public uint SharedMemBytes;
        /// <summary>
        /// The kernel params.
        /// </summary>
        public IntPtr KernelParams;
        /// <summary>
        /// The extra.
        /// </summary>
        public IntPtr Extra;
    }
}
