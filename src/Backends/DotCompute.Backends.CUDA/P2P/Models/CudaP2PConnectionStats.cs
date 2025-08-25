// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Statistics for a specific P2P connection.
    /// </summary>
    public sealed class CudaP2PConnectionStats
    {
        /// <summary>
        /// Gets or sets the number of transfers through this connection.
        /// </summary>
        public long TransferCount { get; set; }

        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        public ulong TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the average bandwidth in GB/s.
        /// </summary>
        public double AverageBandwidth { get; set; }

        /// <summary>
        /// Gets or sets when the last transfer occurred.
        /// </summary>
        public DateTimeOffset? LastTransfer { get; set; }
    }
}