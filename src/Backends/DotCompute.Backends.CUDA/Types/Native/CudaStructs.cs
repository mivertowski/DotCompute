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
        public IntPtr Function;
        public uint GridDimX, GridDimY, GridDimZ;
        public uint BlockDimX, BlockDimY, BlockDimZ;
        public uint SharedMemBytes;
        public IntPtr KernelParams;
        public IntPtr Extra;
    }
}
