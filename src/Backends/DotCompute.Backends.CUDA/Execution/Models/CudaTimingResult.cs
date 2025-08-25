// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Result of a single timing measurement for a CUDA operation.
    /// </summary>
    public sealed class CudaTimingResult
    {
        /// <summary>
        /// Gets or sets the name of the operation that was timed.
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the GPU execution time in milliseconds.
        /// </summary>
        public float GpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the CPU time including overhead in milliseconds.
        /// </summary>
        public float CpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the overhead time (CPU - GPU) in milliseconds.
        /// </summary>
        public float OverheadMs { get; set; }

        /// <summary>
        /// Gets or sets when the operation started.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets when the operation ended.
        /// </summary>
        public DateTimeOffset EndTime { get; set; }

        /// <summary>
        /// Gets or sets the stream the operation was executed on.
        /// </summary>
        public IntPtr Stream { get; set; }
    }
}