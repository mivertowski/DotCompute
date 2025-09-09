// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Result of CUDA graph execution.
    /// </summary>
    public sealed class CudaGraphExecutionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public long ExecutionTimeMs { get; set; }
        public int NodesExecuted { get; set; }
        public string InstanceId { get; set; } = string.Empty;
        public string GraphId { get; set; } = string.Empty;
        public TimeSpan ExecutionTime { get; set; }
        public double GpuTimeMs { get; set; }
        public int ExecutionCount { get; set; }
    }
}