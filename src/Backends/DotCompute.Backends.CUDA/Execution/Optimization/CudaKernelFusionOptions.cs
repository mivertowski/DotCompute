// <copyright file="CudaKernelFusionOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Backends.CUDA.Compilation;

namespace DotCompute.Backends.CUDA.Execution.Optimization;

/// <summary>
/// Configuration options for kernel fusion optimization.
/// Manages kernel arguments and launch configurations for fused operations.
/// </summary>
public sealed class CudaKernelFusionOptions
{
    private readonly Dictionary<CudaCompiledKernel, CudaKernelArguments> _arguments = [];
    private readonly Dictionary<CudaCompiledKernel, CudaLaunchConfig> _configs = [];

    /// <summary>
    /// Gets the kernel arguments for the specified kernel.
    /// Returns empty arguments if not found.
    /// </summary>
    /// <param name="kernel">The kernel to get arguments for.</param>
    /// <returns>The kernel arguments or empty arguments if not configured.</returns>
    public CudaKernelArguments GetArgumentsForKernel(CudaCompiledKernel kernel) 
        => _arguments.TryGetValue(kernel, out var args) ? args : new CudaKernelArguments([]);

    /// <summary>
    /// Gets the launch configuration for the specified kernel.
    /// Returns a default configuration if not found.
    /// </summary>
    /// <param name="kernel">The kernel to get launch configuration for.</param>
    /// <returns>The launch configuration or default if not configured.</returns>
    public CudaLaunchConfig GetLaunchConfigForKernel(CudaCompiledKernel kernel) 
        => _configs.TryGetValue(kernel, out var config) ? config : new CudaLaunchConfig(1, 1, 1, 256, 1, 1);

    /// <summary>
    /// Sets the kernel arguments for a specific kernel.
    /// </summary>
    /// <param name="kernel">The kernel to configure.</param>
    /// <param name="arguments">The arguments to set.</param>
    public void SetCudaKernelArguments(CudaCompiledKernel kernel, CudaKernelArguments arguments) 
        => _arguments[kernel] = arguments;

    /// <summary>
    /// Sets the launch configuration for a specific kernel.
    /// </summary>
    /// <param name="kernel">The kernel to configure.</param>
    /// <param name="config">The launch configuration to set.</param>
    public void SetKernelLaunchConfig(CudaCompiledKernel kernel, CudaLaunchConfig config) 
        => _configs[kernel] = config;
}