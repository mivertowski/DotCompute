// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Types
{
    /// <summary>
    /// Workload types for optimization decisions.
    /// </summary>
    public enum WorkloadType
    {
        /// <summary>
        /// Workload is primarily compute-intensive with heavy ALU usage.
        /// </summary>
        ComputeIntensive,

        /// <summary>
        /// Workload is limited by memory bandwidth.
        /// </summary>
        MemoryBandwidthBound,

        /// <summary>
        /// Workload makes intensive use of shared memory.
        /// </summary>
        SharedMemoryIntensive,

        /// <summary>
        /// Workload utilizes tensor core operations.
        /// </summary>
        TensorOperations,

        /// <summary>
        /// Workload has mixed characteristics.
        /// </summary>
        Mixed
    }
}