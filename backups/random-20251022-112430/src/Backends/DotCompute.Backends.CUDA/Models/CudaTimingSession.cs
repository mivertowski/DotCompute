// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Timing session for tracking long-running profiling operations.
    /// </summary>
    internal sealed class CudaTimingSession
    {
        /// <summary>
        /// Gets or sets the unique session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the operation being profiled.
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stream the operation is executed on.
        /// </summary>
        public IntPtr Stream { get; set; }

        /// <summary>
        /// Gets or sets when the session started.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets when the session ended.
        /// </summary>
        public DateTimeOffset? EndTime { get; set; }

        /// <summary>
        /// Gets or sets the planned number of iterations.
        /// </summary>
        public int PlannedIterations { get; set; }

        /// <summary>
        /// Gets or sets the actual number of iterations completed.
        /// </summary>
        public int ActualIterations { get; set; }

        /// <summary>
        /// Gets or sets the number of iterations completed so far.
        /// </summary>
        public int CompletedIterations { get; set; }
    }
}