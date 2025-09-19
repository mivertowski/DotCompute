using System;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Profiling.Types;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents performance profiling data for memory operations.
    /// </summary>
    public class MemoryProfile
    {
        /// <summary>
        /// Gets or initializes the name of the memory operation.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the type of memory transfer.
        /// </summary>
        public MemoryTransferType TransferType { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes transferred.
        /// </summary>
        public long BytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the time taken for the transfer.
        /// </summary>
        public TimeSpan TransferTime { get; set; }

        /// <summary>
        /// Gets the calculated bandwidth in bytes per second.
        /// </summary>
        public double Bandwidth => BytesTransferred / TransferTime.TotalSeconds;

        /// <summary>
        /// Gets or sets whether the transfer was asynchronous.
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// Gets or sets the CUDA stream ID used for the transfer.
        /// </summary>
        public int StreamId { get; set; }
    }
}