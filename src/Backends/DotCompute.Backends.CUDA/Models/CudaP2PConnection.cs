// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Represents a peer-to-peer connection between two CUDA devices.
    /// </summary>
    public sealed class CudaP2PConnection
    {
        /// <summary>
        /// Gets or sets the source device ID.
        /// </summary>
        public int SourceDevice { get; set; }

        /// <summary>
        /// Gets or sets the destination device ID.
        /// </summary>
        public int DestinationDevice { get; set; }

        /// <summary>
        /// Gets or sets whether the P2P connection is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the measured bandwidth in GB/s.
        /// </summary>
        public double BandwidthGBps { get; set; }

        /// <summary>
        /// Gets or sets when the connection was discovered.
        /// </summary>
        public DateTimeOffset DiscoveredAt { get; set; }

        /// <summary>
        /// Gets or sets when the connection was enabled.
        /// </summary>
        public DateTimeOffset? EnabledAt { get; set; }

        /// <summary>
        /// Gets or sets when the connection was disabled.
        /// </summary>
        public DateTimeOffset? DisabledAt { get; set; }

        /// <summary>
        /// Gets or sets when the last transfer occurred.
        /// </summary>
        public DateTimeOffset? LastTransferAt { get; set; }

        /// <summary>
        /// Gets or sets the total number of transfers.
        /// </summary>
        public long TransferCount { get; set; }

        /// <summary>
        /// Gets or sets the total bytes transferred through this connection.
        /// </summary>
        public ulong TotalBytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the average bandwidth achieved in GB/s.
        /// </summary>
        public double AverageBandwidthGBps { get; set; }
    }
}