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
        public SafeCudaEventHandle() : base(true)
        {
        }

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