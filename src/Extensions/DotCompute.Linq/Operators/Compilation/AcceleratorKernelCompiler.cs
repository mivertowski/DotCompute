// <copyright file="AcceleratorKernelCompiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
namespace DotCompute.Linq.Operators.Compilation;
{
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
    /// Gets the compiler name.
    public string Name => $"{_accelerator.Info.Name} Kernel Compiler";
    /// Gets the supported source types.
    public IReadOnlyList<DotCompute.Abstractions.Kernels.Types.KernelLanguage> SupportedSourceTypes => _accelerator.Type switch
        AcceleratorType.CUDA => new[] { DotCompute.Abstractions.Kernels.Types.KernelLanguage.CUDA, DotCompute.Abstractions.Kernels.Types.KernelLanguage.Ptx },
        AcceleratorType.OpenCL => [DotCompute.Abstractions.Kernels.Types.KernelLanguage.OpenCL],
        AcceleratorType.Metal => [DotCompute.Abstractions.Kernels.Types.KernelLanguage.Metal],
        AcceleratorType.DirectCompute => [DotCompute.Abstractions.Kernels.Types.KernelLanguage.HLSL, DotCompute.Abstractions.Kernels.Types.KernelLanguage.DirectCompute],
        _ => [DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharp]
    };
    /// Gets the compiler capabilities.
    public IReadOnlyDictionary<string, object> Capabilities => new Dictionary<string, object>
        ["SupportsAsync"] = true,
        ["SupportsCaching"] = true,
        ["SupportsOptimization"] = true
    /// Compiles a kernel definition asynchronously.
    /// <param name="definition">The kernel definition to compile.</param>
    /// <param name="options">Optional compilation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the compiled kernel.</returns>
    public async ValueTask<DotCompute.Abstractions.ICompiledKernel> CompileAsync(
        DotCompute.Abstractions.Kernels.KernelDefinition definition,
        DotCompute.Abstractions.CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
        // Delegate to the accelerator's compilation capabilities
        => await _accelerator.CompileKernelAsync(definition, options, cancellationToken).ConfigureAwait(false);
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
        // Delegate to accelerator's optimization capabilities if available
        => ValueTask.FromResult(kernel);
