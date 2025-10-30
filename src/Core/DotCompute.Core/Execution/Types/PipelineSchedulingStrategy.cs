// <copyright file="PipelineSchedulingStrategy.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines scheduling strategies for pipeline parallel execution.
    /// These strategies determine how work flows through pipeline stages to optimize throughput and resource utilization.
    /// </summary>
    public enum PipelineSchedulingStrategy
    {
        /// <summary>
        /// Simple fill-drain scheduling strategy.
        /// Fills the pipeline with data, processes it stage by stage, then drains the results.
        /// Simple to implement but may have lower pipeline utilization during fill and drain phases.
        /// </summary>
        FillDrain,

        /// <summary>
        /// 1F1B (One Forward One Backward) scheduling strategy.
        /// Alternates between forward and backward passes to maintain balanced pipeline utilization.
        /// Commonly used in neural network training to optimize memory usage and pipeline efficiency.
        /// </summary>
        OneForwardOneBackward,

        /// <summary>
        /// Interleaved scheduling for better pipeline utilization.
        /// Overlaps multiple data streams through the pipeline to maximize stage utilization.
        /// Provides highest throughput by minimizing idle time in pipeline stages.
        /// </summary>
        Interleaved
    }
}
