// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Linq.Operators.Execution;

namespace DotCompute.Runtime.Examples.DependencyInjection.Mocks;

/// <summary>
/// Example mock compiled kernel for DI demonstration
/// </summary>
/// <param name="definition">The kernel definition</param>
internal class ExampleMockCompiledKernel(KernelDefinition definition) : ICompiledKernel
{
    /// <inheritdoc />
    public string Name => definition.Name;

    /// <inheritdoc />
    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}