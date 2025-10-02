// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Win32.SafeHandles;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Native
{
    /// <summary>
    /// Safe handle for CUDA events to ensure proper cleanup.
    /// </summary>
    public sealed class SafeCudaEventHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Initializes a new instance of the SafeCudaEventHandle class.
        /// </summary>
        public SafeCudaEventHandle() : base(true)
        {
        }
        /// <summary>
        /// Initializes a new instance of the SafeCudaEventHandle class.
        /// </summary>
        /// <param name="handle">The handle.</param>

        public SafeCudaEventHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var result = CudaRuntime.cudaEventDestroy(handle);
                return result == CudaError.Success;
            }
            return true;
        }
    }
}