// <copyright file="MockCompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Operators.Execution;
using DotCompute.Linq.Operators.Models;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Operators.Mocks;

/// <summary>
/// Mock implementation of a compiled kernel for testing purposes.
/// </summary>
internal class MockCompiledKernel : ICompiledKernel
{
    private readonly string _name;
    private readonly ILogger _logger;
    private bool _disposed;
    private int _executionCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockCompiledKernel"/> class.
    /// </summary>
    /// <param name="name">The kernel name.</param>
    /// <param name="logger">The logger instance.</param>
    public MockCompiledKernel(string name, ILogger logger)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the number of times this kernel has been executed.
    /// </summary>
    public int ExecutionCount => _executionCount;

    /// <summary>
    /// Gets a value indicating whether the kernel has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Executes the mock kernel.
    /// </summary>
    /// <param name="parameters">The execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteAsync(KernelExecutionParameters parameters, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MockCompiledKernel));
        }

        _logger.LogDebug("Executing mock kernel {KernelName} (execution #{Count})", _name, ++_executionCount);

        // Simulate some async work
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        // Simulate processing based on work size
        if (parameters.GlobalWorkSize != null && parameters.GlobalWorkSize.Length > 0)
        {
            var totalWork = 1;
            foreach (var dimension in parameters.GlobalWorkSize)
            {
                totalWork *= dimension;
            }

            _logger.LogDebug("Mock kernel {KernelName} processed {WorkItems} work items", _name, totalWork);
        }

        // Simulate handling arguments
        if (parameters.Arguments != null)
        {
            _logger.LogDebug("Mock kernel {KernelName} received {ArgCount} arguments", _name, parameters.Arguments.Count);
        }
    }

    /// <summary>
    /// Disposes the mock kernel.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebug("Disposed mock kernel {KernelName} after {Count} executions", _name, _executionCount);
        }
    }
}