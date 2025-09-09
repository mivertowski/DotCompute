// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
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
        public bool EnableOptimization { get; set; } = true;
        public DotCompute.Backends.CUDA.Types.CudaArchitecture TargetArchitecture { get; set; } = DotCompute.Backends.CUDA.Types.CudaArchitecture.Ada;
        public bool EnableKernelFusion { get; set; } = true;
        public DotCompute.Backends.CUDA.Types.CudaGraphOptimizationLevel OptimizationLevel { get; set; } = DotCompute.Backends.CUDA.Types.CudaGraphOptimizationLevel.Balanced;
    }
}