// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA IPC event handle structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaIpcEventHandle
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] reserved;
    }

    /// <summary>
    /// CUDA IPC memory handle structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaIpcMemHandle
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] reserved;
    }
}