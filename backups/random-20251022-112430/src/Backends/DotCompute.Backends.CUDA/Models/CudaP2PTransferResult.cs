// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Result of a P2P transfer operation between CUDA devices.
    /// </summary>
    public sealed class CudaP2PTransferResult
    {
        /// <summary>
        /// Gets or sets the unique transfer ID.
        /// </summary>
        public Guid TransferId { get; set; }

        /// <summary>
        /// Gets or sets whether the transfer was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the source device ID.
        /// </summary>
        public int SourceDevice { get; set; }

        /// <summary>
        /// Gets or sets the destination device ID.
        /// </summary>
        public int DestinationDevice { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes transferred.
        /// </summary>
        public ulong BytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the transfer duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the achieved bandwidth in GB/s.
        /// </summary>
        public double BandwidthGBps { get; set; }

        /// <summary>
        /// Gets or sets when the transfer started.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets when the transfer ended.
        /// </summary>
        public DateTimeOffset EndTime { get; set; }

        /// <summary>
        /// Gets or sets an error message if the transfer failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}