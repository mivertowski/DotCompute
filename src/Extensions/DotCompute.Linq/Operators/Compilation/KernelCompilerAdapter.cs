// <copyright file="KernelCompilerAdapter.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Linq.Operators.Execution;
using DotCompute.Linq.Operators.Models;
using DotCompute.Linq.Operators.Mocks;
using DotCompute.Linq.Operators.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Operators.Compilation;

/// <summary>
/// Adapter that bridges the Core kernel compilers with the LINQ interface.
/// </summary>
internal class KernelCompilerAdapter : IUnifiedKernelCompiler
{
    private readonly DotCompute.Abstractions.IUnifiedKernelCompiler _coreCompiler;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCompilerAdapter"/> class.
    /// </summary>
    /// <param name="coreCompiler">The core kernel compiler to adapt.</param>
    /// <param name="logger">The logger instance.</param>
    public KernelCompilerAdapter(DotCompute.Abstractions.IUnifiedKernelCompiler coreCompiler, ILogger logger)
    {
        _coreCompiler = coreCompiler ?? throw new ArgumentNullException(nameof(coreCompiler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Compiles a kernel asynchronously using the adapted core compiler.
    /// </summary>
    /// <param name="request">The kernel compilation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the kernel compilation result.</returns>
    public async Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a mock kernel source for compatibility
            var kernelSource = new MockKernelSource
            {
                Name = request.Name,
                Code = request.Source,
                Language = KernelLanguage.CSharpIL,
                EntryPoint = request.Name
            };

            var options = new DotCompute.Abstractions.CompilationOptions
            {
                OptimizationLevel = ConvertOptimizationLevel(request.OptimizationLevel)
            };

            var kernelDefinition = new DotCompute.Abstractions.Kernels.KernelDefinition(request.Name, kernelSource.Code);

            var compiledKernel = await _coreCompiler.CompileAsync(kernelDefinition, options, cancellationToken);

            return new KernelCompilationResult
            {
                Success = true,
                CompiledKernel = new CompiledKernelAdapter(compiledKernel),
                CompilationTime = TimeSpan.FromMilliseconds(0) // TODO: Get actual time
            };
        }
        catch (Exception ex)
        {
            return new KernelCompilationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static DotCompute.Abstractions.OptimizationLevel ConvertOptimizationLevel(Models.OptimizationLevel level)
    {
        return level switch
        {
            Models.OptimizationLevel.Debug => DotCompute.Abstractions.OptimizationLevel.Debug,
            Models.OptimizationLevel.Default => DotCompute.Abstractions.OptimizationLevel.Default,
            Models.OptimizationLevel.Release => DotCompute.Abstractions.OptimizationLevel.Release,
            Models.OptimizationLevel.Aggressive => DotCompute.Abstractions.OptimizationLevel.Aggressive,
            _ => DotCompute.Abstractions.OptimizationLevel.Default
        };
    }
}