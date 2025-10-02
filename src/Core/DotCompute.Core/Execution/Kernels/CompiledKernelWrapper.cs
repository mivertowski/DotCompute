// <copyright file="CompiledKernelWrapper.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
namespace DotCompute.Core.Execution.Kernels;

/// <summary>
/// Wrapper that adapts CompiledKernel struct to ICompiledKernel interface.
/// Provides a unified interface for compiled kernel execution and resource management.
/// </summary>
internal sealed class CompiledKernelWrapper : ICompiledKernel
{
    private readonly CompiledKernel _kernel;
    private bool _disposed;

    /// <summary>
    /// Gets the kernel unique identifier.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Initializes a new instance of the CompiledKernelWrapper class.
    /// </summary>
    /// <param name="kernel">The compiled kernel to wrap.</param>
    public CompiledKernelWrapper(CompiledKernel kernel)
    {
        _kernel = kernel;
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    /// <value>A generated name based on the kernel ID.</value>
    public string Name => $"Kernel_{_kernel.Id}";

    /// <summary>
    /// Executes the kernel with the specified arguments.
    /// </summary>
    /// <param name="arguments">The kernel execution arguments containing input/output buffers and parameters.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous kernel execution.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CompiledKernelWrapper));
        }

        // Simulate kernel execution - in real implementation would call native kernel - TODO
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously disposes the kernel wrapper and releases associated resources.
    /// </summary>
    /// <returns>A completed task representing the disposal operation.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Disposes the kernel wrapper and releases associated resources.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
    }
}
