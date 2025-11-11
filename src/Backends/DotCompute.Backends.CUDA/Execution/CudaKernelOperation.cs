// <copyright file="CudaKernelOperation.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Types;

namespace DotCompute.Backends.CUDA.Execution.Operations;

/// <summary>
/// Represents a CUDA kernel operation within a graph or execution pipeline.
/// Encapsulates all information needed to launch and optimize a kernel.
/// </summary>
public sealed class CudaKernelOperation
{
    /// <summary>
    /// Gets or sets the compiled kernel to execute.
    /// </summary>
    public CudaCompiledKernel Kernel { get; set; } = null!;

    /// <summary>
    /// Gets or sets the arguments to pass to the kernel.
    /// </summary>
    public CudaKernelArguments Arguments { get; set; } = null!;

    /// <summary>
    /// Gets or sets the launch configuration for the kernel.
    /// Defines grid and block dimensions.
    /// </summary>
    public CudaLaunchConfig LaunchConfig { get; set; }

    /// <summary>
    /// Gets or sets the name of the operation for identification and logging.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of kernel operation.
    /// Used for optimization and fusion decisions.
    /// </summary>
    public CudaKernelType Type { get; set; } = CudaKernelType.Custom;

    /// <summary>
    /// Gets or sets whether this kernel should use Tensor Cores if available.
    /// </summary>
    public bool UseTensorCores { get; set; }

    /// <summary>
    /// Gets or sets the Tensor Core configuration if enabled.
    /// </summary>
    public TensorCoreConfig? TensorCoreConfig { get; set; }

    /// <summary>
    /// Gets or sets the memory access pattern of the kernel.
    /// Used for memory optimization strategies.
    /// </summary>
    public MemoryAccessPattern MemoryAccessPattern { get; set; } = MemoryAccessPattern.Sequential;

    /// <summary>
    /// Gets or sets the cache configuration preference.
    /// </summary>
    public CUDA.Types.CacheConfig CacheConfig { get; set; } = CUDA.Types.CacheConfig.PreferNone;

    /// <summary>
    /// Gets or sets the warp scheduling mode.
    /// </summary>
    public DotCompute.Abstractions.Types.WarpSchedulingMode WarpScheduling { get; set; } = DotCompute.Abstractions.Types.WarpSchedulingMode.Default;

    /// <summary>
    /// Gets or sets the output dimensions as a string representation.
    /// Used for shape inference and optimization.
    /// </summary>
    public string OutputDimensions { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input dimensions as a string representation.
    /// Used for shape inference and optimization.
    /// </summary>
    public string InputDimensions { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is a fused kernel combining multiple operations.
    /// </summary>
    public bool IsFused { get; set; }

    /// <summary>
    /// Gets or sets the original operations if this is a fused kernel.
    /// Maintains traceability after kernel fusion.
    /// </summary>
    public IReadOnlyList<CudaKernelOperation>? OriginalOperations { get; set; }
}
