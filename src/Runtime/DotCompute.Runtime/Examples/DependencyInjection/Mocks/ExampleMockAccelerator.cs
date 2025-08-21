// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;

namespace DotCompute.Runtime.Examples.DependencyInjection.Mocks;

/// <summary>
/// Example mock accelerator implementation for DI demonstration
/// </summary>
internal class ExampleMockAccelerator : IAccelerator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleMockAccelerator"/> class
    /// </summary>
    /// <param name="info">The accelerator information</param>
    public ExampleMockAccelerator(AcceleratorInfo info)
    {
        Info = info;
    }

    /// <inheritdoc />
    public AcceleratorInfo Info { get; }

    /// <inheritdoc />
    public AcceleratorType Type => AcceleratorType.CPU;

    /// <inheritdoc />
    public AcceleratorContext Context { get; } = default!;

    /// <inheritdoc />
    public IMemoryManager Memory => new ExampleMockMemoryManager();

    /// <inheritdoc />
    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var mockKernel = new ExampleMockCompiledKernel(definition);
        return ValueTask.FromResult<ICompiledKernel>(mockKernel);
    }

    /// <inheritdoc />
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}