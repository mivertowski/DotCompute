// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Configuration
{
    /// <summary>
    /// Configuration options for pipeline parallelism execution.
    /// Pipeline parallelism divides computation into sequential stages that can process different
    /// parts of the input simultaneously, similar to an assembly line. This approach is particularly
    /// effective for neural network training and inference, stream processing, and any computation
    /// that can be naturally decomposed into sequential stages.
    /// </summary>
    public class PipelineParallelismOptions
    {
        /// <summary>
        /// Gets or sets the number of pipeline stages.
        /// Each stage represents a segment of the computation pipeline that will be executed
        /// on a separate device or compute unit. More stages can increase parallelism but
        /// also introduce more communication overhead.
        /// </summary>
        /// <value>
        /// The number of pipeline stages. Default is 4.
        /// </value>
        /// <remarks>
        /// The optimal number of stages depends on:
        /// <list type="bullet">
        /// <item><description>Available devices and their compute capacity</description></item>
        /// <item><description>The nature of the computation and its natural division points</description></item>
        /// <item><description>Communication overhead between stages</description></item>
        /// <item><description>Memory constraints on each device</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// var options = new PipelineParallelismOptions
        /// {
        ///     StageCount = 8, // 8-stage pipeline for large model
        ///     MicrobatchSize = 2
        /// };
        /// </code>
        /// </example>
        public int StageCount { get; set; } = 4;

        /// <summary>
        /// Gets or sets the microbatch size for pipelining.
        /// Microbatching divides each input batch into smaller chunks that can flow through
        /// the pipeline stages independently, improving pipeline utilization and reducing
        /// memory requirements per stage.
        /// </summary>
        /// <value>
        /// The microbatch size. Default is 1.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Larger microbatches improve compute efficiency but require more memory</description></item>
        /// <item><description>Smaller microbatches reduce memory usage and improve pipeline fill rate</description></item>
        /// <item><description>The optimal size depends on model size, available memory, and compute characteristics</description></item>
        /// </list>
        /// For neural network training, microbatch size typically ranges from 1 to 32.
        /// </remarks>
        public int MicrobatchSize { get; set; } = 1;

        /// <summary>
        /// Gets or sets the buffer depth for each stage.
        /// Buffer depth determines how many microbatches each stage can hold simultaneously,
        /// which affects pipeline throughput and memory usage. Deeper buffers can improve
        /// throughput by hiding communication latency but require more memory.
        /// </summary>
        /// <value>
        /// The buffer depth for each pipeline stage. Default is 2.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Depth of 1: Minimal memory usage but potential stalls</description></item>
        /// <item><description>Depth of 2-4: Good balance for most workloads</description></item>
        /// <item><description>Depth > 4: High throughput but significant memory overhead</description></item>
        /// </list>
        /// The optimal buffer depth depends on the ratio of computation time to communication time.
        /// </remarks>
        public int BufferDepth { get; set; } = 2;

        /// <summary>
        /// Gets or sets the scheduling strategy for pipeline execution.
        /// Different strategies optimize for different scenarios such as training vs. inference,
        /// uniform vs. non-uniform stage execution times, and memory vs. throughput priorities.
        /// </summary>
        /// <value>
        /// The pipeline scheduling strategy. Default is <see cref="PipelineSchedulingStrategy.FillDrain"/>.
        /// </value>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><see cref="PipelineSchedulingStrategy.FillDrain"/>: Simple strategy, fills pipeline then drains</description></item>
        /// <item><description><see cref="PipelineSchedulingStrategy.OneForwardOneBackward"/>: Optimized for neural network training</description></item>
        /// <item><description><see cref="PipelineSchedulingStrategy.Interleaved"/>: Advanced scheduling for maximum utilization</description></item>
        /// </list>
        /// </remarks>
        public PipelineSchedulingStrategy SchedulingStrategy { get; set; } = PipelineSchedulingStrategy.FillDrain;

        /// <summary>
        /// Converts pipeline parallelism options to data parallelism options for device selection.
        /// This provides a compatible configuration for the underlying device management system
        /// while maintaining pipeline parallelism semantics.
        /// </summary>
        /// <returns>
        /// A <see cref="DataParallelismOptions"/> instance configured for pipeline parallelism requirements.
        /// </returns>
        /// <remarks>
        /// The returned options are configured with:
        /// <list type="bullet">
        /// <item><description>MaxDevices set to StageCount to ensure one device per stage</description></item>
        /// <item><description>Peer-to-peer transfers enabled for fast stage-to-stage communication</description></item>
        /// <item><description>Event-based synchronization for precise pipeline coordination</description></item>
        /// <item><description>Computation-communication overlap enabled to hide latency</description></item>
        /// <item><description>Manual load balancing since pipeline stages are pre-assigned</description></item>
        /// </list>
        /// </remarks>
        public DataParallelismOptions ToDataParallelOptions() => new()
        {
            MaxDevices = StageCount,
            EnablePeerToPeer = true,
            SyncStrategy = SynchronizationStrategy.EventBased,
            OverlapComputeAndCommunication = true,
            LoadBalancing = LoadBalancingStrategy.Manual // Pipeline stages are manually assigned
        };
    }
}