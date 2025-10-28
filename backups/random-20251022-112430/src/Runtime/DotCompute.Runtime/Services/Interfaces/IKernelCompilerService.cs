// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Services.Types;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Service for compiling kernels with caching and optimization
/// </summary>
public interface IKernelCompilerService
{
    /// <summary>
    /// Compiles a kernel with optimizations and caching
    /// </summary>
    /// <param name="definition">The kernel definition</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <param name="options">Compilation options</param>
    /// <returns>The compiled kernel</returns>
    public Task<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        IAccelerator accelerator,
        CompilationOptions? options = null);

    /// <summary>
    /// Pre-compiles kernels for better runtime performance
    /// </summary>
    /// <param name="definitions">The kernel definitions to pre-compile</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>A task representing the pre-compilation operation</returns>
    public Task PrecompileAsync(IEnumerable<KernelDefinition> definitions, IAccelerator accelerator);

    /// <summary>
    /// Gets compilation statistics
    /// </summary>
    /// <returns>Compilation statistics</returns>
    public KernelCompilationStatistics GetStatistics();

    /// <summary>
    /// Optimizes a kernel definition for the target accelerator
    /// </summary>
    /// <param name="definition">The kernel definition</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>The optimized kernel definition</returns>
    public Task<KernelDefinition> OptimizeAsync(KernelDefinition definition, IAccelerator accelerator);

    /// <summary>
    /// Validates a kernel definition for compilation
    /// </summary>
    /// <param name="definition">The kernel definition</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>Validation result</returns>
    public Task<KernelValidationResult> ValidateAsync(KernelDefinition definition, IAccelerator accelerator);
}