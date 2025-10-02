// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Kernels.Compilation;

namespace DotCompute.Abstractions.Interfaces.Kernels
{

    /// <summary>
    /// Interface for managing kernel generation, compilation, caching, and execution.
    /// </summary>
    public interface IKernelManager : IDisposable
    {
        /// <summary>
        /// Registers a kernel generator for a specific accelerator type.
        /// </summary>
        public void RegisterGenerator(AcceleratorType acceleratorType, IKernelGenerator generator);

        /// <summary>
        /// Registers a kernel compiler for a specific accelerator type.
        /// </summary>
        public void RegisterCompiler(AcceleratorType acceleratorType, IUnifiedKernelCompiler compiler);

        /// <summary>
        /// Registers a kernel executor for a specific accelerator type.
        /// </summary>
        public void RegisterExecutor(AcceleratorType acceleratorType, IKernelExecutor executor);

        /// <summary>
        /// Gets or compiles a kernel from an expression.
        /// </summary>
        public ValueTask<ManagedCompiledKernel> GetOrCompileKernelAsync(
            Expression expression,
            IAccelerator accelerator,
            KernelGenerationContext? context = null,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or compiles a kernel for a specific operation.
        /// </summary>
        public ValueTask<ManagedCompiledKernel> GetOrCompileOperationKernelAsync(
            string operation,
            Type[] inputTypes,
            Type outputType,
            IAccelerator accelerator,
            KernelGenerationContext? context = null,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a compiled kernel with the specified arguments.
        /// </summary>
        public ValueTask<KernelExecutionResult> ExecuteKernelAsync(
            ManagedCompiledKernel kernel,
            KernelArgument[] arguments,
            IAccelerator accelerator,
            KernelExecutionConfig? config = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Profiles a kernel execution.
        /// </summary>
        public ValueTask<KernelProfilingResult> ProfileKernelAsync(
            ManagedCompiledKernel kernel,
            KernelArgument[] arguments,
            IAccelerator accelerator,
            KernelExecutionConfig? config = null,
            int iterations = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the kernel cache.
        /// </summary>
        public void ClearCache();

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public KernelCacheStatistics GetCacheStatistics();
    }
}
