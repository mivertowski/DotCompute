// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Pipeline
{
    /// <summary>
    /// Describes a tensor for pipeline execution.
    /// Contains all necessary metadata to properly handle tensor data
    /// throughout the pipeline execution process, including memory management,
    /// data type information, and device-specific optimizations.
    /// </summary>
    /// <typeparam name="T">The unmanaged data type of the tensor elements.</typeparam>
    public class TensorDescription<T> where T : unmanaged
    {
        /// <summary>Gets or sets the tensor name.</summary>
        public required string Name { get; set; }

        /// <summary>Gets or sets the tensor dimensions.</summary>
        public required int[] Dimensions { get; set; }

        /// <summary>Gets or sets the tensor data type.</summary>
        public required Type DataType { get; set; }

        /// <summary>Gets or sets whether this tensor is shared across devices.</summary>
        public bool IsShared { get; set; }

        /// <summary>Gets or sets the buffer containing the tensor data.</summary>
        public AbstractionsMemory.IBuffer<T>? Buffer { get; set; }

        /// <summary>Gets the total size of the tensor in bytes.</summary>
        public long SizeInBytes => ElementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>();

        /// <summary>Gets the total number of elements in the tensor.</summary>
        public long ElementCount => Dimensions.Aggregate(1L, (acc, dim) => acc * dim);
    }

    /// <summary>
    /// Specification for pipeline input configuration.
    /// Defines how data enters the pipeline, including tensor descriptions,
    /// streaming behavior, and data flow patterns. This specification enables
    /// the pipeline system to optimize data ingestion, buffering, and preprocessing
    /// for maximum throughput and minimal latency.
    /// </summary>
    /// <typeparam name="T">The unmanaged data type used in the input tensors.</typeparam>
    public class PipelineInputSpec<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the input tensor descriptions.
        /// An array of tensor specifications that define the structure, format,
        /// and characteristics of input data. Each tensor description provides
        /// metadata needed for proper memory allocation, data validation,
        /// and device-specific optimizations during pipeline execution.
        /// </summary>
        public required TensorDescription<T>[] Tensors { get; set; }

        /// <summary>
        /// Gets or sets the streaming configuration.
        /// Optional configuration for streaming input data processing.
        /// When specified, enables incremental data processing, chunked execution,
        /// and optimized memory usage for large datasets. Null indicates
        /// batch processing mode where all input data is processed at once.
        /// </summary>
        public StreamingConfig? StreamingConfig { get; set; }
    }
}