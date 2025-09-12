// <copyright file="ExecutionStrategyType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines the types of parallel execution strategies available for compute operations.
    /// Each strategy represents a different approach to distributing work across computing devices.
    /// </summary>
    public enum ExecutionStrategyType
    {


        /// <summary>
        /// Single device execution strategy.
        /// Executes the entire workload on a single computing device without parallelization.
        /// Best for small workloads or when device resources are limited.
        /// </summary>
#pragma warning disable CA1720 // Identifier contains type name
        Single,
#pragma warning restore CA1720 // Identifier contains type name


        /// <summary>
        /// Data parallelism across multiple devices.
        /// Distributes input data across multiple devices while replicating the computation.
        /// Optimal for large datasets that can be partitioned independently.
        /// </summary>
        DataParallel,

        /// <summary>
        /// Model parallelism for large models.
        /// Splits the computational model across multiple devices when the model is too large for a single device.
        /// Essential for training or inference with very large neural networks.
        /// </summary>
        ModelParallel,

        /// <summary>
        /// Pipeline parallelism with streaming.
        /// Divides the computation into sequential stages executed on different devices with overlapping execution.
        /// Provides high throughput for sequential computations with natural stage boundaries.
        /// </summary>
        PipelineParallel,

        /// <summary>
        /// Dynamic work stealing for load balancing.
        /// Allows idle devices to steal work from busy devices to maintain optimal load distribution.
        /// Best for workloads with irregular or unpredictable execution times.
        /// </summary>
        WorkStealing,

        /// <summary>
        /// Heterogeneous CPU+GPU execution.
        /// Coordinates execution across different types of computing devices (CPUs and GPUs).
        /// Maximizes utilization of all available computing resources.
        /// </summary>
        Heterogeneous
    }
}
