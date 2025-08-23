// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Types;

namespace DotCompute.Backends.CUDA.Execution.Graph.Configuration
{
    /// <summary>
    /// Provides configuration options for kernel fusion operations within CUDA graphs.
    /// Manages kernel arguments and launch configurations for fusion candidate kernels.
    /// </summary>
    /// <remarks>
    /// This class maintains mappings between kernels and their execution parameters,
    /// enabling the fusion system to correctly configure fused operations while preserving
    /// the original execution semantics of individual kernels.
    /// </remarks>
    public sealed class CudaKernelFusionOptions
    {
        private readonly Dictionary<CudaCompiledKernel, CudaKernelArguments> _arguments = [];
        private readonly Dictionary<CudaCompiledKernel, CudaLaunchConfig> _configs = [];

        /// <summary>
        /// Retrieves the kernel arguments for the specified compiled kernel.
        /// </summary>
        /// <param name="kernel">The compiled kernel for which to retrieve arguments.</param>
        /// <returns>
        /// The <see cref="CudaKernelArguments"/> associated with the kernel, or an empty argument set if not found.
        /// </returns>
        /// <remarks>
        /// If no arguments have been explicitly set for the kernel, this method returns an empty
        /// argument collection to prevent null reference exceptions during fusion operations.
        /// </remarks>
        public CudaKernelArguments GetArgumentsForKernel(CudaCompiledKernel kernel) => 
            _arguments.TryGetValue(kernel, out var args) ? args : new CudaKernelArguments([]);

        /// <summary>
        /// Retrieves the launch configuration for the specified compiled kernel.
        /// </summary>
        /// <param name="kernel">The compiled kernel for which to retrieve the launch configuration.</param>
        /// <returns>
        /// The <see cref="CudaLaunchConfig"/> associated with the kernel, or a default configuration if not found.
        /// </returns>
        /// <remarks>
        /// If no launch configuration has been explicitly set for the kernel, this method returns
        /// a default configuration suitable for most kernels: 1 grid dimension with 256 threads per block.
        /// </remarks>
        public CudaLaunchConfig GetLaunchConfigForKernel(CudaCompiledKernel kernel) => 
            _configs.TryGetValue(kernel, out var config) ? config : new CudaLaunchConfig(1, 1, 1, 256, 1, 1);

        /// <summary>
        /// Sets the kernel arguments for the specified compiled kernel.
        /// </summary>
        /// <param name="kernel">The compiled kernel for which to set arguments.</param>
        /// <param name="arguments">The kernel arguments to associate with the kernel.</param>
        /// <remarks>
        /// This method allows configuration of kernel-specific arguments that will be used
        /// during the fusion process. The arguments must be compatible with the kernel's parameter signature.
        /// </remarks>
        public void SetCudaKernelArguments(CudaCompiledKernel kernel, CudaKernelArguments arguments) => 
            _arguments[kernel] = arguments;

        /// <summary>
        /// Sets the launch configuration for the specified compiled kernel.
        /// </summary>
        /// <param name="kernel">The compiled kernel for which to set the launch configuration.</param>
        /// <param name="config">The launch configuration to associate with the kernel.</param>
        /// <remarks>
        /// This method allows configuration of kernel-specific execution dimensions that will be used
        /// during the fusion process. The configuration should be optimized for the kernel's computational requirements.
        /// </remarks>
        public void SetKernelLaunchConfig(CudaCompiledKernel kernel, CudaLaunchConfig config) => 
            _configs[kernel] = config;
    }
}