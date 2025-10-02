// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Base;

/// <summary>
/// Base class for optimized kernel implementations providing common functionality
/// for kernel execution, disposal, and error handling.
/// </summary>
/// <remarks>
/// This abstract base class provides the foundation for all optimized CPU kernel implementations.
/// It includes common functionality for lifecycle management, argument validation, and
/// performance monitoring through logging.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="OptimizedKernelBase"/> class.
/// </remarks>
/// <param name="name">The name of the kernel.</param>
/// <param name="options">The compilation options for the kernel.</param>
/// <param name="logger">The logger instance for diagnostics.</param>
/// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
internal abstract class OptimizedKernelBase(string name, CompilationOptions options, ILogger logger) : ICompiledKernel
{
    /// <summary>
    /// Gets the logger instance for this kernel.
    /// </summary>
    protected readonly ILogger Logger = logger ?? throw new ArgumentNullException(nameof(logger));


    /// <summary>
    /// Gets the compilation options used for this kernel.
    /// </summary>
    protected readonly CompilationOptions Options = options ?? throw new ArgumentNullException(nameof(options));


    /// <summary>
    /// Gets a value indicating whether this kernel has been disposed.
    /// </summary>
    protected bool Disposed;

    /// <summary>
    /// Gets the unique identifier for this kernel.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the name of the kernel.
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Executes the kernel asynchronously with the specified arguments.
    /// </summary>
    /// <param name="arguments">The kernel arguments containing input and output buffers.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    public abstract ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disposes the kernel asynchronously, releasing any allocated resources.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous disposal operation.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => Disposed = true;

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the kernel has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Disposed, this);
}