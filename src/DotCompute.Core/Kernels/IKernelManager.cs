// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Interface for managing kernel generation, compilation, caching, and execution.
/// </summary>
public interface IKernelManager : IDisposable
{
    /// <summary>
    /// Registers a kernel generator for a specific accelerator type.
    /// </summary>
    void RegisterGenerator(AcceleratorType acceleratorType, IKernelGenerator generator);

    /// <summary>
    /// Registers a kernel compiler for a specific accelerator type.
    /// </summary>
    void RegisterCompiler(AcceleratorType acceleratorType, IKernelCompiler compiler);

    /// <summary>
    /// Registers a kernel executor for a specific accelerator type.
    /// </summary>
    void RegisterExecutor(AcceleratorType acceleratorType, IKernelExecutor executor);

    /// <summary>
    /// Generates a kernel from an expression for a specific accelerator type.
    /// </summary>
    Task<IKernelSource> GenerateKernelAsync(Expression expression, AcceleratorType acceleratorType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compiles a kernel source for a specific accelerator.
    /// </summary>
    Task<ICompiledKernel> CompileKernelAsync(IKernelSource source, IAccelerator accelerator, CompilationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or compiles a kernel from cache.
    /// </summary>
    Task<ICompiledKernel> GetOrCompileKernelAsync(string kernelId, IKernelSource source, IAccelerator accelerator, CompilationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a compiled kernel with the specified arguments.
    /// </summary>
    Task<KernelExecutionResult> ExecuteKernelAsync(ICompiledKernel compiledKernel, KernelArguments arguments, KernelExecutionConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the kernel cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    KernelCacheStatistics GetCacheStatistics();
}