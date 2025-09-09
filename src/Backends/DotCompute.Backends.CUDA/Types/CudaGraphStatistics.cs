// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA graph statistics.
    /// </summary>
    public sealed class CudaGraphStatistics
    {
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public long EstimatedMemoryUsage { get; set; }
        public double AverageNodeExecutionTime { get; set; }
        public string GraphId { get; set; } = string.Empty;
        public int InstanceCount { get; set; }
        public int TotalExecutions { get; set; }
        public double TotalGpuTimeMs { get; set; }
        public double AverageExecutionTimeMs { get; set; }
        public DateTimeOffset? LastExecutedAt { get; set; }
        public bool IsOptimized { get; set; }
    }
}