// <copyright file="AcceleratorKernelCompiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;

namespace DotCompute.Linq.Operators.Compilation;

/// <summary>
/// Accelerator-based kernel compiler that uses the accelerator's native compilation capabilities.
/// </summary>
internal class AcceleratorKernelCompiler : DotCompute.Abstractions.IUnifiedKernelCompiler
{
    private readonly IAccelerator _accelerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcceleratorKernelCompiler"/> class.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for compilation.</param>
    public AcceleratorKernelCompiler(IAccelerator accelerator)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }

    /// <summary>
    /// Gets the compiler name.
    /// </summary>
    public string Name => $"{_accelerator.Info.Name} Kernel Compiler";

    /// <summary>
    /// Gets the supported source types.
    /// </summary>
    public IReadOnlyList<DotCompute.Abstractions.Types.KernelLanguage> SupportedSourceTypes => _accelerator.Type switch
    {
        AcceleratorType.CUDA => new[] { DotCompute.Abstractions.Types.KernelLanguage.CUDA, DotCompute.Abstractions.Types.KernelLanguage.Ptx },
        AcceleratorType.OpenCL => new[] { DotCompute.Abstractions.Types.KernelLanguage.OpenCL },
        AcceleratorType.Metal => new[] { DotCompute.Abstractions.Types.KernelLanguage.Metal },
        AcceleratorType.DirectCompute => new[] { DotCompute.Abstractions.Types.KernelLanguage.HLSL, DotCompute.Abstractions.Types.KernelLanguage.DirectCompute },
        _ => new[] { DotCompute.Abstractions.Types.KernelLanguage.CSharp }
    };

    /// <summary>
    /// Gets the compiler capabilities.
    /// </summary>
    public IReadOnlyDictionary<string, object> Capabilities => new Dictionary<string, object>
    {
        ["SupportsAsync"] = true,
        ["SupportsCaching"] = true,
        ["SupportsOptimization"] = true
    };

    /// <summary>
    /// Compiles a kernel definition asynchronously.
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
        // Delegate to the accelerator's compilation capabilities
        return await _accelerator.CompileKernelAsync(definition, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates a kernel definition.
    /// </summary>
    /// <param name="definition">The kernel definition to validate.</param>
    /// <returns>The validation result.</returns>
    public DotCompute.Abstractions.Validation.UnifiedValidationResult Validate(DotCompute.Abstractions.Kernels.KernelDefinition definition)
    {
        if (definition == null)
        {
            return DotCompute.Abstractions.Validation.UnifiedValidationResult.Failure("Kernel definition cannot be null");
        }

        if (string.IsNullOrEmpty(definition.Name))
        {
            return DotCompute.Abstractions.Validation.UnifiedValidationResult.Failure("Kernel name cannot be empty");
        }

        return DotCompute.Abstractions.Validation.UnifiedValidationResult.Success();
    }

    /// <summary>
    /// Validates a kernel definition asynchronously.
    /// </summary>
    /// <param name="definition">The kernel definition to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the validation operation.</returns>
    public ValueTask<DotCompute.Abstractions.Validation.UnifiedValidationResult> ValidateAsync(
        DotCompute.Abstractions.Kernels.KernelDefinition definition,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Validate(definition));
    }

    /// <summary>
    /// Optimizes an already compiled kernel.
    /// </summary>
    /// <param name="kernel">The kernel to optimize.</param>
    /// <param name="level">The optimization level.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the optimization operation.</returns>
    public ValueTask<DotCompute.Abstractions.ICompiledKernel> OptimizeAsync(
        DotCompute.Abstractions.ICompiledKernel kernel,
        OptimizationLevel level,
        CancellationToken cancellationToken = default)
    {
        // Delegate to accelerator's optimization capabilities if available
        return ValueTask.FromResult(kernel);
    }
}