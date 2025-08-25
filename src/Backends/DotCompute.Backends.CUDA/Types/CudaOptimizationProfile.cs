// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Types
{
    /// <summary>
    /// Optimization profiles for different use cases.
    /// </summary>
    public enum CudaOptimizationProfile
    {
        /// <summary>
        /// Safe optimizations only.
        /// </summary>
        Conservative,

        /// <summary>
        /// Good balance of performance and stability.
        /// </summary>
        Balanced,

        /// <summary>
        /// Maximum performance optimizations.
        /// </summary>
        Aggressive,

        /// <summary>
        /// ML/AI specific optimizations.
        /// </summary>
        MachineLearning,

        /// <summary>
        /// Optimize for throughput over latency.
        /// </summary>
        HighThroughput,

        /// <summary>
        /// Optimize for latency over throughput.
        /// </summary>
        LowLatency
    }
}