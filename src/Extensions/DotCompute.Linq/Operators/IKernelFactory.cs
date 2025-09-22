// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Types;
namespace DotCompute.Linq.Operators;
{
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
    public IKernel CreateKernel(IAccelerator accelerator, DotCompute.Abstractions.Kernels.KernelDefinition definition);
    /// Validates whether a kernel definition is supported on the accelerator.
    /// <param name="definition">The kernel definition to validate.</param>
    /// <returns>True if the kernel is supported; otherwise, false.</returns>
    public bool IsKernelSupported(IAccelerator accelerator, DotCompute.Abstractions.Kernels.KernelDefinition definition);
    /// Gets the supported kernel languages for an accelerator.
    /// <param name="accelerator">The accelerator to query.</param>
    /// <returns>The supported kernel languages.</returns>
    public IReadOnlyList<DotCompute.Abstractions.Kernels.Types.KernelLanguage> GetSupportedLanguages(IAccelerator accelerator);
}
