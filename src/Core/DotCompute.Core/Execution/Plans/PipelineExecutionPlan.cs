// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Types;
using ManagedCompiledKernel = DotCompute.Core.Execution.ManagedCompiledKernel;

namespace DotCompute.Core.Execution.Plans
{
    /// <summary>
    /// Execution plan for pipeline parallel execution across multiple devices.
    /// In pipeline parallelism, computation is divided into sequential stages, with each stage
    /// executing on a different device, allowing for overlapped execution of different microbatches.
    /// </summary>
    /// <typeparam name="T">The data type for the execution plan. Must be an unmanaged type.</typeparam>
    public class PipelineExecutionPlan<T> : ExecutionPlan<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the pipeline stages.
        /// Each stage represents a computational step that executes on a specific device.
        /// </summary>
        public required PipelineStage<T>[] Stages { get; set; }

        /// <summary>
        /// Gets or sets the microbatch configuration.
        /// Defines how input data is divided into smaller batches for pipeline processing.
        /// </summary>
        public required MicrobatchConfiguration MicrobatchConfig { get; set; }

        /// <summary>
        /// Gets or sets the buffer management strategy.
        /// Manages buffer allocation, reuse, and transfer between pipeline stages.
        /// </summary>
        public required PipelineBufferStrategy<T> BufferStrategy { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineExecutionPlan{T}"/> class.
        /// Sets the strategy type to PipelineParallel by default.
        /// </summary>
        public PipelineExecutionPlan()
        {
            StrategyType = ExecutionStrategyType.PipelineParallel;
        }
    }

    /// <summary>
    /// Represents a stage in a pipeline.
    /// Each stage processes data sequentially, with outputs from one stage becoming inputs to the next.
    /// </summary>
    /// <typeparam name="T">The data type for the pipeline stage. Must be an unmanaged type.</typeparam>
    public class PipelineStage<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the stage identifier.
        /// Unique identifier for this stage within the pipeline.
        /// </summary>
        public required int StageId { get; set; }

        /// <summary>
        /// Gets or sets the stage name.
        /// Human-readable name for this pipeline stage.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the device assigned to this stage.
        /// The specific accelerator that will execute this stage.
        /// </summary>
        public required IAccelerator Device { get; set; }

        /// <summary>
        /// Gets or sets the kernel for this stage.
        /// The compiled computational kernel that implements this stage's operations.
        /// </summary>
        public required ManagedCompiledKernel Kernel { get; set; }

        /// <summary>
        /// Gets or sets the input buffers for this stage.
        /// Buffers containing the input data that this stage will process.
        /// </summary>
        public required AbstractionsMemory.IUnifiedMemoryBuffer<T>[] InputBuffers { get; set; }

        /// <summary>
        /// Gets or sets the output buffers for this stage.
        /// Buffers where this stage will write its computed results.
        /// </summary>
        public required AbstractionsMemory.IUnifiedMemoryBuffer<T>[] OutputBuffers { get; set; }

        /// <summary>
        /// Gets or sets the processing time estimate for this stage in milliseconds.
        /// Used for scheduling and load balancing decisions.
        /// </summary>
        public double EstimatedProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Configuration for microbatching in pipeline execution.
    /// Defines how large batches are subdivided into smaller microbatches for pipeline processing.
    /// </summary>
    public class MicrobatchConfiguration
    {
        /// <summary>
        /// Gets or sets the microbatch size.
        /// Number of elements in each microbatch that flows through the pipeline.
        /// </summary>
        public required int Size { get; set; }

        /// <summary>
        /// Gets or sets the number of microbatches to process.
        /// Total number of microbatches that will be processed in this execution.
        /// </summary>
        public required int Count { get; set; }

        /// <summary>
        /// Gets or sets the scheduling strategy for microbatches.
        /// Determines how microbatches are scheduled and prioritized through the pipeline.
        /// </summary>
        public required MicrobatchSchedulingStrategy SchedulingStrategy { get; set; }
    }

    /// <summary>
    /// Buffer management strategy for pipeline execution.
    /// Handles allocation, reuse, and optimization of buffers throughout the pipeline.
    /// </summary>
    /// <typeparam name="T">The data type for buffer management. Must be an unmanaged type.</typeparam>
    public class PipelineBufferStrategy<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the buffer pool for reusing buffers.
        /// Manages a pool of reusable buffers to reduce allocation overhead.
        /// </summary>
        public required BufferPool<T> BufferPool { get; set; }

        /// <summary>
        /// Gets or sets the double buffering configuration.
        /// Enables overlapped computation and data transfer using double buffering.
        /// </summary>
        public required DoubleBufferingConfig DoubleBuffering { get; set; }

        /// <summary>
        /// Gets or sets the prefetching strategy.
        /// Optimizes performance by prefetching data for upcoming pipeline stages.
        /// </summary>
        public required PrefetchingStrategy Prefetching { get; set; }
    }

    /// <summary>
    /// Buffer pool for managing reusable buffers in pipeline execution.
    /// Reduces memory allocation overhead by maintaining a pool of reusable buffers.
    /// Note: This is a simplified placeholder implementation.
    /// </summary>
    /// <typeparam name="T">The data type for buffers in the pool. Must be an unmanaged type.</typeparam>
    public class BufferPool<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the maximum number of buffers to keep in the pool.
        /// Controls memory usage by limiting pool size.
        /// </summary>
        public int MaxPoolSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets the default buffer size in elements.
        /// Used when creating new buffers for the pool.
        /// </summary>
        public int DefaultBufferSize { get; set; } = 1024;
    }

    /// <summary>
    /// Double buffering configuration for pipeline execution.
    /// Enables overlapped computation and data transfer by using two sets of buffers.
    /// </summary>
    public class DoubleBufferingConfig
    {
        /// <summary>
        /// Gets or sets whether double buffering is enabled.
        /// When enabled, allows computation on one buffer while transferring data to/from another.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the buffer swap strategy.
        /// Determines when and how buffers are swapped in double buffering mode.
        /// </summary>
        public BufferSwapStrategy SwapStrategy { get; set; } = BufferSwapStrategy.Automatic;
    }

    /// <summary>
    /// Prefetching strategy for pipeline execution.
    /// Optimizes performance by preloading data for upcoming pipeline stages.
    /// </summary>
    public class PrefetchingStrategy
    {
        /// <summary>
        /// Gets or sets whether prefetching is enabled.
        /// When enabled, data for upcoming stages is loaded in advance.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of stages to prefetch ahead.
        /// Determines how many stages in advance to prefetch data.
        /// </summary>
        public int PrefetchDepth { get; set; } = 2;

        /// <summary>
        /// Gets or sets the prefetching policy.
        /// Determines the aggressiveness of the prefetching strategy.
        /// </summary>
        public PrefetchingPolicy Policy { get; set; } = PrefetchingPolicy.Aggressive;
    }
}