// <copyright file="AcceleratorKernelCompiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Linq.Operators.Compilation;

/// <summary>
/// Accelerator-based kernel compiler that uses the accelerator's native compilation capabilities.
/// </summary>
internal class AcceleratorKernelCompiler : DotCompute.Abstractions.IKernelCompiler
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
    public DotCompute.Abstractions.KernelSourceType[] SupportedSourceTypes => new[]
    {
        DotCompute.Abstractions.KernelSourceType.ExpressionTree,
        DotCompute.Abstractions.KernelSourceType.CUDA,
        DotCompute.Abstractions.KernelSourceType.OpenCL,
        DotCompute.Abstractions.KernelSourceType.HLSL
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