// <copyright file="CpuFallbackKernelCompiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Execution;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
namespace DotCompute.Linq.Operators.Compilation;
/// <summary>
/// CPU fallback kernel compiler for when no accelerator is available.
/// </summary>
internal class CpuFallbackKernelCompiler : DotCompute.Abstractions.IUnifiedKernelCompiler
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
    /// Gets the compiler name.
    public string Name => "CPU Fallback Kernel Compiler";
    /// Gets the supported source types.
    public IReadOnlyList<DotCompute.Abstractions.Kernels.Types.KernelLanguage> SupportedSourceTypes => new[]
        DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharp,
        DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharpIL,
        DotCompute.Abstractions.Kernels.Types.KernelLanguage.Binary
    };
    /// Gets the compiler capabilities.
    public IReadOnlyDictionary<string, object> Capabilities => new Dictionary<string, object>
        ["SupportsAsync"] = true,
        ["SupportsCaching"] = false,
        ["SupportsOptimization"] = false
    /// Compiles a kernel definition asynchronously for CPU execution.
    /// <param name="definition">The kernel definition to compile.</param>
    /// <param name="options">Optional compilation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the compiled kernel.</returns>
    public async ValueTask<DotCompute.Abstractions.ICompiledKernel> CompileAsync(
        DotCompute.Abstractions.Kernels.KernelDefinition definition,
        DotCompute.Abstractions.CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
        _logger.LogWarningMessage("Using CPU fallback compiler for kernel {definition.Name}");
        // Create a simple CPU-executable kernel
        var compiledKernel = new CpuFallbackCompiledKernel(definition, _logger);
        await Task.Yield(); // Simulate async work
        return compiledKernel;
    /// Validates a kernel definition.
    /// <param name="definition">The kernel definition to validate.</param>
    /// <returns>The validation result.</returns>
    public DotCompute.Abstractions.Validation.UnifiedValidationResult Validate(DotCompute.Abstractions.Kernels.KernelDefinition definition)
        if (definition == null)
        {
            return DotCompute.Abstractions.Validation.UnifiedValidationResult.Failure("Kernel definition cannot be null");
        }
        if (string.IsNullOrEmpty(definition.Name))
            return DotCompute.Abstractions.Validation.UnifiedValidationResult.Failure("Kernel name cannot be empty");
        return DotCompute.Abstractions.Validation.UnifiedValidationResult.Success();
    /// Validates a kernel definition asynchronously.
    /// <returns>A task representing the validation operation.</returns>
    public ValueTask<DotCompute.Abstractions.Validation.UnifiedValidationResult> ValidateAsync(
        CancellationToken cancellationToken = default) => ValueTask.FromResult(Validate(definition));
    /// Optimizes an already compiled kernel.
    /// <param name="kernel">The kernel to optimize.</param>
    /// <param name="level">The optimization level.</param>
    /// <returns>A task representing the optimization operation.</returns>
    public ValueTask<DotCompute.Abstractions.ICompiledKernel> OptimizeAsync(
        DotCompute.Abstractions.ICompiledKernel kernel,
        OptimizationLevel level,
        // CPU fallback doesn't support optimization, return the same kernel
        _logger.LogDebugMessage("CPU fallback compiler does not support optimization");
        return ValueTask.FromResult(kernel);
}
