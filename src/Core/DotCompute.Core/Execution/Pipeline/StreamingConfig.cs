// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Pipeline
{
    /// <summary>
    /// Configuration for streaming pipeline input processing.
    /// Defines parameters that control how data flows through the pipeline
    /// in streaming mode, including chunking behavior, buffering strategies,
    /// and memory management. Streaming configuration enables processing
    /// of large datasets that exceed available memory by breaking them
    /// into manageable chunks with optimized buffering.
    /// </summary>
    public class StreamingConfig
    {
        /// <summary>
        /// Gets or sets whether streaming is enabled.
        /// When true, the pipeline processes data in chunks rather than
        /// loading the entire dataset into memory at once. This enables
        /// processing of arbitrarily large datasets while maintaining
        /// predictable memory usage patterns.
        /// Defaults to true for optimal memory efficiency.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the chunk size for streaming.
        /// Specifies the number of elements processed in each streaming chunk.
        /// Larger chunks may improve throughput but increase memory usage,
        /// while smaller chunks provide better memory efficiency but may
        /// reduce overall performance due to increased overhead.
        /// The optimal chunk size depends on available memory and workload characteristics.
        /// Defaults to 1024 elements for balanced performance and memory usage.
        /// </summary>
        public int ChunkSize { get; set; } = 1024;

        /// <summary>
        /// Gets or sets the buffer depth for streaming.
        /// Determines how many chunks are buffered simultaneously for pipeline processing.
        /// Higher buffer depth can improve throughput by enabling better overlap
        /// between data transfer and computation, but increases memory usage.
        /// Buffer depth should be tuned based on the pipeline's compute-to-transfer
        /// ratio and available memory constraints.
        /// Defaults to 3 buffers for optimal pipeline efficiency.
        /// </summary>
        public int BufferDepth { get; set; } = 3;
    }
}
