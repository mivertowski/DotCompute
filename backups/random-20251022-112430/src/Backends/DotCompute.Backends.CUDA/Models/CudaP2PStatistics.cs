// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Statistics for P2P operations across all devices.
    /// </summary>
    public sealed class CudaP2PStatistics
    {
        /// <summary>
        /// Gets or sets the total number of devices.
        /// </summary>
        public int TotalDevices { get; set; }

        /// <summary>
        /// Gets or sets the total number of P2P connections.
        /// </summary>
        public int TotalConnections { get; set; }

        /// <summary>
        /// Gets or sets the number of enabled connections.
        /// </summary>
        public int EnabledConnections { get; set; }

        /// <summary>
        /// Gets or sets the total number of transfers performed.
        /// </summary>
        public long TotalTransfers { get; set; }

        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        public ulong TotalBytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the average bandwidth achieved in GB/s.
        /// </summary>
        public double AverageBandwidthGBps { get; set; }

        /// <summary>
        /// Gets or initializes per-connection utilization statistics.
        /// Key is "source-destination" string.
        /// </summary>
        public Dictionary<string, CudaP2PConnectionStats> ConnectionUtilization { get; init; } = [];
    }
}