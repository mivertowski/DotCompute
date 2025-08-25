// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Backends.CUDA.Execution.Types;

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Represents a pooled CUDA event with metadata.
    /// </summary>
    internal sealed class PooledEvent
    {
        /// <summary>
        /// Gets or sets the native CUDA event handle.
        /// </summary>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// Gets or sets the type of CUDA event.
        /// </summary>
        public CudaEventType Type { get; set; }

        /// <summary>
        /// Gets or sets when the event was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the event was last acquired from the pool.
        /// </summary>
        public DateTimeOffset? AcquiredAt { get; set; }

        /// <summary>
        /// Gets or sets the number of times this event has been acquired.
        /// </summary>
        public long AcquireCount { get; set; }
    }
}