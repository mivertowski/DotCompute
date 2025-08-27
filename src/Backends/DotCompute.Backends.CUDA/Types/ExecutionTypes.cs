// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Warp scheduling mode for CUDA execution.
    /// </summary>
    public enum WarpSchedulingMode
    {
        Default,
        Persistent,
        Dynamic,
        Cooperative
    }

    /// <summary>
    /// Tensor core configuration.
    /// </summary>
    public sealed class TensorCoreConfig
    {
        public bool Enabled { get; set; }
        public DataType InputType { get; set; }
        public DataType OutputType { get; set; }
        public int TileSize { get; set; } = 16;
    }

    /// <summary>
    /// Memory access pattern for optimization.
    /// </summary>
    public enum MemoryAccessPattern
    {
        Sequential,
        Strided,
        Random,
        Coalesced,
        Broadcast
    }

    /// <summary>
    /// Kernel launch configuration.
    /// </summary>
    public sealed class LaunchConfiguration
    {
        public (int x, int y, int z) GridDimensions { get; set; }
        public (int x, int y, int z) BlockDimensions { get; set; }
        public int SharedMemoryBytes { get; set; }
        public nint Stream { get; set; }
    }

    /// <summary>
    /// Kernel fusion candidate.
    /// </summary>
    public sealed class KernelFusionCandidate
    {
        public string KernelA { get; set; } = string.Empty;
        public string KernelB { get; set; } = string.Empty;
        public double FusionBenefit { get; set; }
        public bool IsEligible { get; set; }
    }

    /// <summary>
    /// CUDA kernel type.
    /// </summary>
    public enum CudaKernelType
    {
        Compute,
        Memory,
        Reduction,
        Scan,
        Sort,
        Custom
    }
    /// <summary>
    /// CUDA graph capture mode.
    /// </summary>
    public enum CudaGraphCaptureMode
    {
        Global,
        ThreadLocal,
        Relaxed
    }
    
    /// <summary>
    /// CUDA cache configuration.
    /// </summary>
    public enum CacheConfig
    {
        PreferNone,
        PreferShared,
        PreferL1,
        PreferEqual
    }
    
    /// <summary>
    /// Options for CUDA graph optimization.
    /// </summary>
    public sealed class CudaGraphOptimizationOptions
    {
        public bool EnableFusion { get; set; } = true;
        public bool EnableCoalescing { get; set; } = true;
        public bool EnablePipelining { get; set; } = true;
        public int MaxNodesPerGraph { get; set; } = 1000;
        public bool UseInstantiatedGraphs { get; set; } = true;
    }


    /// <summary>
    /// Result of CUDA graph execution.
    /// </summary>
    public sealed class CudaGraphExecutionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public long ExecutionTimeMs { get; set; }
        public int NodesExecuted { get; set; }
    }

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
    }

    /// <summary>
    /// Validation options for CUDA operations.
    /// </summary>
    public sealed class ValidationOptions
    {
        public bool ValidateMemoryAccess { get; set; } = true;
        public bool ValidateLaunchParameters { get; set; } = true;
        public bool ValidateKernelExistence { get; set; } = true;
        public bool EnableBoundsChecking { get; set; } = false;
        public bool EnableNanDetection { get; set; } = false;
    }

    /// <summary>
    /// CUDA kernel fusion options.
    /// </summary>
    public sealed class CudaKernelFusionOptions
    {
        public bool EnableAutoFusion { get; set; } = true;
        public int MaxFusedKernels { get; set; } = 4;
        public double MinimumBenefitThreshold { get; set; } = 0.2;
    }
    
    /// <summary>
    /// CUDA graph update parameters.
    /// </summary>
    public sealed class CudaGraphUpdateParameters
    {
        public bool UpdateNodeParams { get; set; } = true;
        public bool UpdateKernelParams { get; set; } = true;
        public bool PreserveTopology { get; set; } = true;
    }
    
    /// <summary>
    /// CUDA graph statistics.
    /// </summary>
    public sealed class CudaGraphStatistics
    {
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public long EstimatedMemoryUsage { get; set; }
        public double AverageNodeExecutionTime { get; set; }
    }
    
    /// <summary>
    /// CUDA unified memory buffer implementation.
    /// </summary>
    public sealed class CudaUnifiedMemoryBuffer
    {
        public nint DevicePointer { get; set; }
        public long SizeInBytes { get; set; }
        public bool IsManaged { get; set; }
        public int DeviceId { get; set; }
    }
}