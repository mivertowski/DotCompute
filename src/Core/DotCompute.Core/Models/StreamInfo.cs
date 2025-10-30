using DotCompute.Abstractions.Execution;
using DotCompute.Backends.CUDA.Execution.Types;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Information about a CUDA stream.
    /// </summary>
    public class StreamInfo
    {
        /// <summary>
        /// Gets or sets the stream handle.
        /// </summary>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// Gets or sets the stream name for identification.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the stream priority.
        /// </summary>
        public StreamPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the stream creation flags.
        /// </summary>
        public StreamFlags Flags { get; set; }

        /// <summary>
        /// Gets or sets when the stream was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the stream was last used.
        /// </summary>
        public DateTimeOffset? LastUsedAt { get; set; }

        /// <summary>
        /// Gets or sets whether the stream is currently in use.
        /// </summary>
        public bool IsInUse { get; set; }

        /// <summary>
        /// Gets or sets whether the stream is being captured for a graph.
        /// </summary>
        public bool IsCapturing { get; set; }

        /// <summary>
        /// Gets or sets the number of operations executed on this stream.
        /// </summary>
        public long OperationCount { get; set; }
    }
}
