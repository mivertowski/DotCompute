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
        /// <summary>
        /// The ctx.
        /// </summary>
        public nint ctx;
        /// <summary>
        /// The count.
        /// </summary>
        public uint count;
        /// <summary>
        /// The param array.
        /// </summary>
        public nint paramArray;
        /// <summary>
        /// The flags.
        /// </summary>
        public uint flags;
    }
}