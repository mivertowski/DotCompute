// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA batch memory operation node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaBatchMemOpNodeParams
    {
        public nint ctx;
        public uint count;
        public nint paramArray;
        public uint flags;
    }
}