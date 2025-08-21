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
    /// Initializes a new instance of the <see cref="CpuFallbackCompiledKernel"/> class.
    /// </summary>
    /// <param name="definition">The kernel definition.</param>
    /// <param name="logger">The logger instance.</param>
    public CpuFallbackCompiledKernel(KernelDefinition definition, ILogger logger)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name => _definition.Name;

    /// <summary>
    /// Gets a value indicating whether the kernel has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Executes the kernel on the CPU.
    /// </summary>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuFallbackCompiledKernel));
        }

        _logger.LogDebug("Executing CPU fallback kernel {KernelName}", Name);

        // Simple CPU execution - just simulate work
        // In production, this would interpret or execute the kernel code
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Disposes the compiled kernel asynchronously.
    /// </summary>
    /// <returns>A value task representing the asynchronous operation.</returns>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebug("Disposed CPU fallback kernel {KernelName}", Name);
        }
        return ValueTask.CompletedTask;
    }
}