// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Validation;

namespace DotCompute.Abstractions;

/// <summary>
/// Unified kernel compiler interface that replaces all duplicate compiler interfaces.
/// This is the ONLY kernel compiler interface in the entire solution.
/// </summary>
/// <typeparam name="TSource">The type of the kernel source.</typeparam>
/// <typeparam name="TCompiled">The type of the compiled kernel.</typeparam>
public interface IUnifiedKernelCompiler<in TSource, TCompiled>
    where TSource : class
    where TCompiled : ICompiledKernel
{
    /// <summary>
    /// Gets the name of the compiler.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the supported source types this compiler can handle.
    /// </summary>
    IReadOnlyList<KernelLanguage> SupportedSourceTypes { get; }
    
    /// <summary>
    /// Gets compiler capabilities as a dictionary of feature flags.
    /// </summary>
    IReadOnlyDictionary<string, object> Capabilities { get; }
    
    /// <summary>
    /// Compiles a kernel source to executable form.
    /// </summary>
    /// <param name="source">The kernel source to compile.</param>
    /// <param name="options">Optional compilation options.</param>
    /// <param name="cancellationToken">Token to cancel the compilation.</param>
    /// <returns>A task that represents the asynchronous compilation operation.</returns>
    ValueTask<TCompiled> CompileAsync(
        TSource source,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a kernel source without compiling.
    /// </summary>
    /// <param name="source">The kernel source to validate.</param>
    /// <returns>A validation result indicating whether compilation is possible.</returns>
    UnifiedValidationResult Validate(TSource source);
    
    /// <summary>
    /// Asynchronously validates a kernel source with detailed analysis.
    /// </summary>
    /// <param name="source">The kernel source to validate.</param>
    /// <param name="cancellationToken">Token to cancel the validation operation.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    ValueTask<UnifiedValidationResult> ValidateAsync(
        TSource source,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Optimizes an already compiled kernel for better performance.
    /// </summary>
    /// <param name="kernel">The compiled kernel to optimize.</param>
    /// <param name="level">The optimization level to apply.</param>
    /// <param name="cancellationToken">Token to cancel the optimization.</param>
    /// <returns>A task that represents the asynchronous optimization operation.</returns>
    ValueTask<TCompiled> OptimizeAsync(
        TCompiled kernel,
        OptimizationLevel level,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-generic version of the unified kernel compiler interface for convenience.
/// </summary>
public interface IUnifiedKernelCompiler : IUnifiedKernelCompiler<KernelDefinition, ICompiledKernel>
{
}