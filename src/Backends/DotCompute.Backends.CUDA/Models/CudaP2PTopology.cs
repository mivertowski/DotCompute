// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using DotCompute.Backends.CUDA.Models;

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Represents the P2P topology of available CUDA devices.
    /// </summary>
    public sealed class CudaP2PTopology
    {
        /// <summary>
        /// Gets or sets the total number of devices.
        /// </summary>
        public int DeviceCount { get; set; }

        /// <summary>
        /// Gets or sets the list of available devices.
        /// </summary>
        public List<CudaDeviceInfo> Devices { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of P2P connections between devices.
        /// </summary>
        public List<CudaP2PConnection> Connections { get; set; } = [];

        /// <summary>
        /// Gets or sets whether all devices are fully connected via P2P.
        /// </summary>
        public bool IsFullyConnected { get; set; }

        /// <summary>
        /// Gets or sets the optimal transfer paths between device pairs.
        /// Key is (source, destination) tuple, value is list of intermediate device IDs.
        /// </summary>
        public Dictionary<(int, int), List<int>> OptimalTransferPaths { get; set; } = [];
    }
}