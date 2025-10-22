// <copyright file="CommunicationBackend.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines communication backends for multi-device data transfer and synchronization.
    /// These backends provide different approaches to moving data between devices during parallel execution.
    /// </summary>
    public enum CommunicationBackend
    {
        /// <summary>
        /// NVIDIA Collective Communications Library (NCCL).
        /// Optimized for NVIDIA GPU clusters with support for high-performance collective operations.
        /// Provides the best performance for multi-GPU training and inference on NVIDIA hardware.
        /// </summary>
        NCCL,

        /// <summary>
        /// Message Passing Interface (MPI).
        /// Industry-standard communication protocol for distributed computing.
        /// Suitable for heterogeneous clusters and provides broad compatibility across different systems.
        /// </summary>
        MPI,

        /// <summary>
        /// Direct peer-to-peer GPU transfers.
        /// Uses direct memory access between GPUs when supported by hardware.
        /// Provides lowest latency for compatible devices with high-speed interconnects like NVLink.
        /// </summary>
        P2P,

        /// <summary>
        /// Host-based communication through CPU memory.
        /// Data transfers go through host memory as an intermediate step.
        /// Most compatible option but with higher latency and host memory bandwidth requirements.
        /// </summary>
        Host
    }
}
