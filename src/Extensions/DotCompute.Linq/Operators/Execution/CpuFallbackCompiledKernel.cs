// <copyright file="CpuFallbackCompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
namespace DotCompute.Linq.Operators.Execution;
/// <summary>
/// CPU fallback compiled kernel implementation.
/// </summary>
internal class CpuFallbackCompiledKernel : DotCompute.Abstractions.ICompiledKernel
{
    private readonly KernelDefinition _definition;
    private readonly ILogger _logger;
    private bool _disposed;
    /// <summary>
    /// Gets the kernel unique identifier.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// Initializes a new instance of the <see cref="CpuFallbackCompiledKernel"/> class.
    /// <param name="definition">The kernel definition.</param>
    /// <param name="logger">The logger instance.</param>
    public CpuFallbackCompiledKernel(KernelDefinition definition, ILogger logger)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    /// Gets the kernel name.
    public string Name => _definition.Name;
    /// Gets a value indicating whether the kernel has been disposed.
    public bool IsDisposed => _disposed;
    /// Executes the kernel on the CPU.
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuFallbackCompiledKernel));
        }
        _logger.LogDebugMessage("Executing CPU fallback kernel {Name}");
        // Simple CPU execution - just simulate work
        // In production, this would interpret or execute the kernel code
        return ValueTask.CompletedTask;
    /// Disposes the compiled kernel asynchronously.
    public ValueTask DisposeAsync()
        Dispose();
    /// Disposes the compiled kernel synchronously.
    public void Dispose()
        if (!_disposed)
            _disposed = true;
            _logger.LogDebugMessage("Disposed CPU fallback kernel {Name}");
}
