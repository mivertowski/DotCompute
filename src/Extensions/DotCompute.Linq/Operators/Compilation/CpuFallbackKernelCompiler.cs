// <copyright file="CpuFallbackKernelCompiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Linq.Operators.Execution;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Operators.Compilation;

/// <summary>
/// CPU fallback kernel compiler for when no accelerator is available.
/// </summary>
internal class CpuFallbackKernelCompiler : DotCompute.Abstractions.IKernelCompiler
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuFallbackKernelCompiler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public CpuFallbackKernelCompiler(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the compiler name.
    /// </summary>
    public string Name => "CPU Fallback Kernel Compiler";

    /// <summary>
    /// Gets the supported source types.
    /// </summary>
    public DotCompute.Abstractions.KernelSourceType[] SupportedSourceTypes => new[]
    {
        DotCompute.Abstractions.KernelSourceType.ExpressionTree,
        DotCompute.Abstractions.KernelSourceType.Binary
    };

    /// <summary>
    /// Compiles a kernel definition asynchronously for CPU execution.
    /// </summary>
    /// <param name="definition">The kernel definition to compile.</param>
    /// <param name="options">Optional compilation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the compiled kernel.</returns>
    public async ValueTask<DotCompute.Abstractions.ICompiledKernel> CompileAsync(
        DotCompute.Abstractions.Kernels.KernelDefinition definition,
        DotCompute.Abstractions.CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Using CPU fallback compiler for kernel {KernelName}", definition.Name);

        // Create a simple CPU-executable kernel
        var compiledKernel = new CpuFallbackCompiledKernel(definition, _logger);

        await Task.Yield(); // Simulate async work
        return compiledKernel;
    }

    /// <summary>
    /// Validates a kernel definition.
    /// </summary>
    /// <param name="definition">The kernel definition to validate.</param>
    /// <returns>The validation result.</returns>
    public DotCompute.Abstractions.ValidationResult Validate(DotCompute.Abstractions.Kernels.KernelDefinition definition)
    {
        if (definition == null)
        {
            return DotCompute.Abstractions.ValidationResult.Failure("Kernel definition cannot be null");
        }

        if (string.IsNullOrEmpty(definition.Name))
        {
            return DotCompute.Abstractions.ValidationResult.Failure("Kernel name cannot be empty");
        }

        return DotCompute.Abstractions.ValidationResult.Success();
    }
}