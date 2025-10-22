// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Information about an active CUDA event.
    /// </summary>
    internal sealed class CudaEventInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for this event.
        /// </summary>
        public EventId EventId { get; set; }

        /// <summary>
        /// Gets or sets the native CUDA event handle.
        /// </summary>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// Gets or sets the type of this event.
        /// </summary>
        public CudaEventType Type { get; set; }

        /// <summary>
        /// Gets or sets when the event was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the event was recorded to a stream.
        /// </summary>
        public DateTimeOffset? RecordedAt { get; set; }

        /// <summary>
        /// Gets or sets when the event completed.
        /// </summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the stream this event was recorded on.
        /// </summary>
        public IntPtr Stream { get; set; }

        /// <summary>
        /// Gets or sets whether this event was acquired from the pool.
        /// </summary>
        public bool IsFromPool { get; set; }
    }
}