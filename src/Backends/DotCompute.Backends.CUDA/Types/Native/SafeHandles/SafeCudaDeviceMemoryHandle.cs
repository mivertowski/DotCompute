// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Win32.SafeHandles;

namespace DotCompute.Backends.CUDA.Native
{
    /// <summary>
    /// Safe handle for CUDA device memory pointers to ensure proper cleanup.
    /// </summary>
    public sealed class SafeCudaDeviceMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Initializes a new instance of the SafeCudaDeviceMemoryHandle class.
        /// </summary>
        public SafeCudaDeviceMemoryHandle() : base(true)
        {
        }
        /// <summary>
        /// Initializes a new instance of the SafeCudaDeviceMemoryHandle class.
        /// </summary>
        /// <param name="handle">The handle.</param>

        public SafeCudaDeviceMemoryHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var result = CudaRuntime.cudaFree(handle);
                return result == CudaError.Success;
            }
            return true;
        }
    }
}
