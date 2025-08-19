// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions;

namespace DotCompute.Core.Kernels
{

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
    /// Gets or compiles a kernel from an expression.
    /// </summary>
    ValueTask<ManagedCompiledKernel> GetOrCompileKernelAsync(
        Expression expression,
        IAccelerator accelerator,
        KernelGenerationContext? context = null,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or compiles a kernel for a specific operation.
    /// </summary>
    ValueTask<ManagedCompiledKernel> GetOrCompileOperationKernelAsync(
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
    ValueTask<KernelExecutionResult> ExecuteKernelAsync(
        ManagedCompiledKernel kernel,
        KernelArgument[] arguments,
        IAccelerator accelerator,
        KernelExecutionConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Profiles a kernel execution.
    /// </summary>
    ValueTask<KernelProfilingResult> ProfileKernelAsync(
        ManagedCompiledKernel kernel,
        KernelArgument[] arguments,
        IAccelerator accelerator,
        KernelExecutionConfig? config = null,
        int iterations = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the kernel cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    KernelCacheStatistics GetCacheStatistics();
}}
