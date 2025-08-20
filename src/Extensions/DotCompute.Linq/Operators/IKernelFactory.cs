// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Kernels;
namespace DotCompute.Linq.Operators;


/// <summary>
/// Factory for creating compute kernels.
/// </summary>
public interface IKernelFactory
{
    /// <summary>
    /// Creates a kernel for the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The target accelerator.</param>
    /// <param name="definition">The kernel definition.</param>
    /// <returns>A compiled kernel.</returns>
    IKernel CreateKernel(IAccelerator accelerator, KernelDefinition definition);

    /// <summary>
    /// Validates whether a kernel definition is supported on the accelerator.
    /// </summary>
    /// <param name="accelerator">The target accelerator.</param>
    /// <param name="definition">The kernel definition to validate.</param>
    /// <returns>True if the kernel is supported; otherwise, false.</returns>
    bool IsKernelSupported(IAccelerator accelerator, KernelDefinition definition);

    /// <summary>
    /// Gets the supported kernel languages for an accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator to query.</param>
    /// <returns>The supported kernel languages.</returns>
    IReadOnlyList<KernelLanguage> GetSupportedLanguages(IAccelerator accelerator);
}
