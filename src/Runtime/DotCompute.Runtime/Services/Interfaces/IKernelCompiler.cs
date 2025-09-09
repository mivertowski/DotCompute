// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Compiles kernel definitions into executable code for specific accelerators.
/// </summary>
public interface IKernelCompiler
{
    /// <summary>
    /// Compiles a kernel definition for the specified accelerator.
    /// </summary>
    /// <param name="kernelDefinition">The kernel definition to compile</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>The compiled kernel ready for execution</returns>
    public Task<ICompiledKernel> CompileAsync(
        KernelDefinition kernelDefinition,

        IAccelerator accelerator,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a kernel can be compiled for the specified accelerator.
    /// </summary>
    /// <param name="kernelDefinition">The kernel definition to validate</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>True if the kernel can be compiled, false otherwise</returns>
    public Task<bool> CanCompileAsync(KernelDefinition kernelDefinition, IAccelerator accelerator);

    /// <summary>
    /// Gets the supported compilation options for the accelerator.
    /// </summary>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>The supported compilation options</returns>
    public CompilationOptions GetSupportedOptions(IAccelerator accelerator);

    /// <summary>
    /// Performs batch compilation of multiple kernels for optimization.
    /// </summary>
    /// <param name="kernelDefinitions">The kernel definitions to compile</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Dictionary mapping kernel names to compiled kernels</returns>
    public Task<IDictionary<string, ICompiledKernel>> BatchCompileAsync(
        IEnumerable<KernelDefinition> kernelDefinitions,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default);
}