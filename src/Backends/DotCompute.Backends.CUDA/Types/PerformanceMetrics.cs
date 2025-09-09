// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Performance metrics for CUDA operations.
    /// </summary>
    public sealed class PerformanceMetrics
    {
        public long KernelExecutionTimeMs { get; set; }
        public long MemoryTransferTimeMs { get; set; }
        public long TotalExecutionTimeMs { get; set; }
        public double ThroughputGBps { get; set; }
        public double ComputeUtilization { get; set; }
        public double MemoryUtilization { get; set; }
        public long OperationsPerSecond { get; set; }
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Total floating point operations performed.
        /// </summary>
        public long TotalFlops { get; set; }

        /// <summary>
        /// Number of times this operation was called.
        /// </summary>
        public int CallCount { get; set; }
    }
}