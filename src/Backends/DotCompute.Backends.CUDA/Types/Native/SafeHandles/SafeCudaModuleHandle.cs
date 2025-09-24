// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Win32.SafeHandles;
using DotCompute.Backends.CUDA.Native.Types;

namespace DotCompute.Backends.CUDA.Native
{
    /// <summary>
    /// Safe handle for CUDA module handles to ensure proper cleanup.
    /// </summary>
    public sealed class SafeCudaModuleHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeCudaModuleHandle() : base(true)
        {
        }

        public SafeCudaModuleHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var result = CudaRuntime.cuModuleUnload(handle);
                return result == CudaError.Success;
            }
            return true;
        }
    }
}