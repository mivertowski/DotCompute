// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Native.Types
{

    /// <summary>
    /// P/Invoke structures for CUDA API
    /// </summary>
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
