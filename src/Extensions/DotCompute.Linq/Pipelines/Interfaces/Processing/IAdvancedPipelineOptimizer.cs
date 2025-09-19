// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Threading.Tasks;

namespace DotCompute.Linq.Pipelines.Interfaces.Processing
{
    /// <summary>
    /// Temporary interface definition for IAdvancedPipelineOptimizer.
    /// This will be moved to proper location once infrastructure is ready.
    /// </summary>
    public interface IAdvancedPipelineOptimizer
    {
        /// <summary>
        /// Optimizes a pipeline using advanced query plan optimization.
        /// </summary>
        /// <param name="pipeline">The pipeline to optimize</param>
        /// <returns>Optimized pipeline</returns>
        Task<object> OptimizeQueryPlanAsync(object pipeline);

        /// <summary>
        /// Applies kernel fusion optimizations to the pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline to optimize</param>
        /// <returns>Pipeline with fused kernels</returns>
        Task<object> FuseKernelsAsync(object pipeline);

        /// <summary>
        /// Optimizes memory access patterns in the pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline to optimize</param>
        /// <returns>Memory-optimized pipeline</returns>
        Task<object> OptimizeMemoryAccessAsync(object pipeline);
    }
}