// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Profiling.Types
{
    /// <summary>
    /// Types of performance bottlenecks in kernel execution.
    /// </summary>
    public enum BottleneckType
    {
        /// <summary>
        /// No significant bottleneck detected.
        /// </summary>
        None,

        /// <summary>
        /// Low occupancy limiting performance.
        /// </summary>
        Occupancy,

        /// <summary>
        /// Memory bandwidth limitations.
        /// </summary>
        MemoryBandwidth,

        /// <summary>
        /// Compute throughput limitations.
        /// </summary>
        Compute,

        /// <summary>
        /// Thread divergence causing inefficiency.
        /// </summary>
        ThreadDivergence
    }
}