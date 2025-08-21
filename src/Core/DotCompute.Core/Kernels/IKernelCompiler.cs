// <copyright file="IKernelCompiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Core.Kernels.Compilation;
using DotCompute.Core.Kernels.Validation;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Defines the interface for kernel compilation services.
/// Provides methods for compiling, validating, and optimizing compute kernels
/// across different platforms and source types.
/// </summary>
public interface IKernelCompiler
{
    /// <summary>
    /// Compiles a kernel from the specified source.
    /// </summary>
    /// <param name="source">The kernel source to compile.</param>
    /// <param name="options">Compilation options to control the compilation process.</param>
    /// <param name="cancellationToken">Token to cancel the compilation operation.</param>
    /// <returns>A task that represents the asynchronous compilation operation, containing the compiled kernel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or options is null.</exception>
    /// <exception cref="CompilationException">Thrown when compilation fails.</exception>
    public Task<ManagedCompiledKernel> CompileAsync(
        IKernelSource source,
        Types.CompilationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a kernel source without compiling.
    /// Performs syntax checking, resource analysis, and compatibility verification.
    /// </summary>
    /// <param name="source">The kernel source to validate.</param>
    /// <param name="cancellationToken">Token to cancel the validation operation.</param>
    /// <returns>A task that represents the asynchronous validation operation, containing validation results.</returns>
    public Task<KernelValidationResult> ValidateAsync(
        IKernelSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes a compiled kernel for better performance.
    /// May include instruction scheduling, register allocation, and memory access optimization.
    /// </summary>
    /// <param name="kernel">The compiled kernel to optimize.</param>
    /// <param name="level">The optimization level to apply.</param>
    /// <param name="cancellationToken">Token to cancel the optimization operation.</param>
    /// <returns>A task that represents the asynchronous optimization operation, containing the optimized kernel.</returns>
    public Task<ManagedCompiledKernel> OptimizeAsync(
        ManagedCompiledKernel kernel,
        Types.OptimizationLevel level,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the supported source types for this compiler.
    /// </summary>
    public IReadOnlyList<KernelSourceType> SupportedSourceTypes { get; }

    /// <summary>
    /// Gets the compiler capabilities and features.
    /// </summary>
    public IReadOnlyDictionary<string, object> Capabilities { get; }
}