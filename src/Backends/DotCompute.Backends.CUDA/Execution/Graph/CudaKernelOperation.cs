// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Types;

namespace DotCompute.Backends.CUDA.Execution.Graph
{
    /// <summary>
    /// Represents a single kernel operation within a CUDA graph, including all necessary
    /// execution parameters and optimization configurations for RTX 2000 Ada architecture.
    /// </summary>
    /// <remarks>
    /// This class encapsulates all information needed to execute a kernel within a graph context,
    /// including advanced features like tensor core usage, memory access patterns, and warp scheduling.
    /// </remarks>
    public sealed class CudaKernelOperation
    {
        /// <summary>
        /// Gets or sets the compiled kernel to be executed.
        /// </summary>
        /// <value>A <see cref="CudaCompiledKernel"/> instance representing the executable kernel.</value>
        public CudaCompiledKernel Kernel { get; set; } = null!;

        /// <summary>
        /// Gets or sets the arguments to pass to the kernel during execution.
        /// </summary>
        /// <value>A <see cref="CudaKernelArguments"/> instance containing the kernel parameters.</value>
        public CudaKernelArguments Arguments { get; set; } = null!;

        /// <summary>
        /// Gets or sets the launch configuration specifying grid and block dimensions.
        /// </summary>
        /// <value>A <see cref="CudaLaunchConfig"/> instance defining the execution dimensions.</value>
        public CudaLaunchConfig LaunchConfig { get; set; }

        /// <summary>
        /// Gets or sets a human-readable name for this operation.
        /// </summary>
        /// <value>A string representing the operation name for debugging and profiling purposes.</value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type classification of this kernel operation.
        /// </summary>
        /// <value>A CudaKernelType value indicating the operation category.</value>
        public DotCompute.Backends.CUDA.Types.CudaKernelType Type { get; set; } = DotCompute.Backends.CUDA.Types.CudaKernelType.Custom;

        /// <summary>
        /// Gets or sets a value indicating whether this operation should utilize tensor cores.
        /// </summary>
        /// <value><c>true</c> if tensor cores should be used for matrix operations; otherwise, <c>false</c>.</value>
        public bool UseTensorCores { get; set; }

        /// <summary>
        /// Gets or sets the tensor core configuration for optimized matrix operations.
        /// </summary>
        /// <value>A nullable <see cref="TensorCoreConfig"/> specifying tensor core parameters.</value>
        public TensorCoreConfig? TensorCoreConfig { get; set; }

        /// <summary>
        /// Gets or sets the memory access pattern for this operation.
        /// </summary>
        /// <value>A <see cref="MemoryAccessPattern"/> value indicating the memory access strategy.</value>
        public MemoryAccessPattern MemoryAccessPattern { get; set; } = MemoryAccessPattern.Sequential;

        /// <summary>
        /// Gets or sets the cache configuration preference for this operation.
        /// </summary>
        /// <value>A <see cref="CacheConfig"/> value specifying cache utilization preferences.</value>
        public DotCompute.Backends.CUDA.Types.CacheConfig CacheConfig { get; set; } = DotCompute.Backends.CUDA.Types.CacheConfig.PreferNone;

        /// <summary>
        /// Gets or sets the warp scheduling mode for this operation.
        /// </summary>
        /// <value>A <see cref="WarpSchedulingMode"/> value indicating the scheduling strategy.</value>
        public DotCompute.Backends.CUDA.Types.WarpSchedulingMode WarpScheduling { get; set; } = DotCompute.Backends.CUDA.Types.WarpSchedulingMode.Default;

        /// <summary>
        /// Gets or sets a string representation of the output data dimensions.
        /// </summary>
        /// <value>A string describing the output tensor dimensions for optimization purposes.</value>
        public string OutputDimensions { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a string representation of the input data dimensions.
        /// </summary>
        /// <value>A string describing the input tensor dimensions for optimization purposes.</value>
        public string InputDimensions { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this operation is the result of kernel fusion.
        /// </summary>
        /// <value><c>true</c> if this operation was created by fusing multiple kernels; otherwise, <c>false</c>.</value>
        public bool IsFused { get; set; }

        /// <summary>
        /// Gets or sets the original operations that were fused to create this operation.
        /// </summary>
        /// <value>An array of <see cref="CudaKernelOperation"/> instances that were combined, or <c>null</c> if not fused.</value>
        public CudaKernelOperation[]? OriginalOperations { get; set; }
    }
}