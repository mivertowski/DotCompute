// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Enums
{
    /// <summary>
    /// Specifies the direction of a CUDA memory copy operation between host and device memory.
    /// </summary>
    /// <remarks>
    /// Memory copy direction affects performance characteristics:
    /// - Host-to-Device and Device-to-Host copies traverse the PCIe bus
    /// - Device-to-Device copies use high-bandwidth GPU memory bus
    /// - Host-to-Host copies use CPU memory bandwidth
    /// Using pinned (page-locked) host memory can significantly improve transfer performance.
    /// </remarks>
    public enum MemcpyKind
    {
        /// <summary>
        /// Copy from host memory to host memory (CPU to CPU).
        /// </summary>
        HostToHost = 0,

        /// <summary>
        /// Copy from host memory to device memory (CPU to GPU).
        /// Typical use case for transferring input data to GPU.
        /// </summary>
        HostToDevice = 1,

        /// <summary>
        /// Copy from device memory to host memory (GPU to CPU).
        /// Typical use case for retrieving computation results from GPU.
        /// </summary>
        DeviceToHost = 2,

        /// <summary>
        /// Copy from device memory to device memory (GPU to GPU).
        /// Uses high-bandwidth GPU memory bus for peer-to-peer transfers.
        /// </summary>
        DeviceToDevice = 3,

        /// <summary>
        /// Automatically determine copy direction based on pointer locations.
        /// Requires unified virtual addressing (UVA) support on the device.
        /// </summary>
        Default = 4
    }
}