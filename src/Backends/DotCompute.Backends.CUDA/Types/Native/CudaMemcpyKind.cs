// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Specifies the direction of a memory copy operation in CUDA.
    /// </summary>
    /// <remarks>
    /// This enumeration is used with memory copy functions like cudaMemcpy and cudaMemcpyAsync
    /// to specify whether data is being copied between host memory, device memory, or both.
    /// The Default option allows CUDA to automatically determine the copy direction based on
    /// the memory pointers involved.
    /// </remarks>
    public enum CudaMemcpyKind
    {
        /// <summary>
        /// Copy from host memory to host memory.
        /// </summary>
        HostToHost = 0,

        /// <summary>
        /// Copy from host memory to device memory.
        /// </summary>
        HostToDevice = 1,

        /// <summary>
        /// Copy from device memory to host memory.
        /// </summary>
        DeviceToHost = 2,

        /// <summary>
        /// Copy from device memory to device memory.
        /// </summary>
        DeviceToDevice = 3,

        /// <summary>
        /// Let CUDA automatically determine the copy direction.
        /// This is the recommended option when using unified virtual addressing.
        /// </summary>
        Default = 4
    }
}