// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.Kernel.Generators;

/// <summary>
/// Defines the contract for kernel registry generation.
/// </summary>
public interface IKernelRegistryGenerator
{
    /// <summary>
    /// Generates the kernel registry for all discovered kernels.
    /// </summary>
    void GenerateRegistry(
        ImmutableArray<KernelMethodInfo> kernelMethods,
        ImmutableArray<KernelClassInfo> kernelClasses,
        SourceProductionContext context);

    /// <summary>
    /// Generates an invoker for kernel classes.
    /// </summary>
    void GenerateInvoker(
        KernelClassInfo kernelClass,
        ImmutableArray<KernelMethodInfo> allMethods,
        SourceProductionContext context);
}