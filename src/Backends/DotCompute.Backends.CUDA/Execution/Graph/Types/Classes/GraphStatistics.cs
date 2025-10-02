// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Graph statistics for performance tracking
    /// </summary>
    public class GraphStatistics
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public int ExecutionCount { get; set; }
        public int InstantiationCount { get; set; }
        public int UpdateCount { get; set; }
        public int OptimizationCount { get; set; }
        public int ErrorCount { get; set; }
        public float TotalExecutionTimeMs { get; set; }
        public float LastExecutionTimeMs { get; set; }
        public string? ClonedFrom { get; set; }
        public CudaGraphCaptureMode? CaptureMode { get; set; }
    }
}