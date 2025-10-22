// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines scheduling strategies for microbatch processing in pipeline parallel execution.
    /// These strategies control how microbatches are processed through the pipeline stages
    /// to optimize throughput and resource utilization.
    /// </summary>
    public enum MicrobatchSchedulingStrategy
    {
        /// <summary>
        /// Process microbatches sequentially.
        /// Each microbatch completes all pipeline stages before the next microbatch begins.
        /// Simple strategy with predictable memory usage but potentially lower throughput.
        /// </summary>
        Sequential,

        /// <summary>
        /// Interleave forward and backward passes.
        /// Overlaps forward passes of newer microbatches with backward passes of older microbatches.
        /// Improves pipeline utilization by reducing idle time between stages.
        /// </summary>
        Interleaved,

        /// <summary>
        /// Use 1F1B (One Forward One Backward) scheduling.
        /// Alternates between forward and backward passes to maintain steady pipeline flow.
        /// Optimizes memory usage by minimizing the number of activations stored simultaneously.
        /// </summary>
        OneForwardOneBackward
    }
}