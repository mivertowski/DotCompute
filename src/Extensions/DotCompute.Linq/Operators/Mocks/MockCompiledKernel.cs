// <copyright file="MockCompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Operators.Execution;
using DotCompute.Linq.Operators.Models;
using DotCompute.Linq.Operators.Parameters;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;
using DotCompute.Linq.KernelGeneration.Execution;

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

    /// <inheritdoc />
    public string Name => _name;

    /// <inheritdoc />
    public string SourceCode => $"// Mock kernel source for {_name}";

    /// <inheritdoc />
    public IReadOnlyList<KernelParameter> Parameters => new List<KernelParameter>();

    /// <inheritdoc />
    public string EntryPoint => _name;

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

        _executionCount++;
        _logger.LogDebugMessage($"Executing mock kernel {_name} (execution #{_executionCount})");

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

            _logger.LogDebugMessage($"Mock kernel {_name} processed {totalWork} work items");
        }

        // Simulate handling arguments
        if (parameters.Arguments != null)
        {
            _logger.LogDebugMessage($"Mock kernel {_name} received {parameters.Arguments.Count} arguments");
        }
    }

    /// <summary>
    /// Launches the mock kernel with specified dimensions.
    /// </summary>
    /// <param name="workgroupSize">The workgroup size for kernel execution.</param>
    /// <param name="globalSize">The global size for kernel execution.</param>
    /// <param name="parameters">The kernel execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous kernel launch.</returns>
    public async Task LaunchAsync(
        (int x, int y, int z) workgroupSize,
        (int x, int y, int z) globalSize,
        KernelExecutionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MockCompiledKernel));
        }

        _executionCount++;
        _logger.LogDebugMessage($"Launching mock kernel {_name} (execution #{_executionCount}) with workgroup {workgroupSize} and global size {globalSize}");

        // Simulate some async work
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        // Simulate processing based on global work size
        var totalWork = globalSize.x * globalSize.y * globalSize.z;
        _logger.LogDebugMessage($"Mock kernel {_name} launched for {totalWork} work items");
    }

    /// <inheritdoc />
    public async Task LaunchAsync(
        int globalSize,
        int localSize,
        object[] args,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MockCompiledKernel));
        }

        _executionCount++;
        _logger.LogDebugMessage($"Launching mock kernel {_name} (execution #{_executionCount}) with global size {globalSize}, local size {localSize}");

        // Simulate some async work
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        // Simulate handling arguments
        _logger.LogDebugMessage($"Mock kernel {_name} received {args.Length} arguments");
    }

    /// <inheritdoc />
    public async Task LaunchAsync(
        object[] args,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MockCompiledKernel));
        }

        _executionCount++;
        _logger.LogDebugMessage($"Launching mock kernel {_name} (execution #{_executionCount})");

        // Simulate some async work
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        // Simulate handling arguments
        _logger.LogDebugMessage($"Mock kernel {_name} received {args.Length} arguments");
    }

    /// <inheritdoc />
    public async Task LaunchAsync(
        DotCompute.Linq.KernelGeneration.Execution.Dim3 blockSize,
        DotCompute.Linq.KernelGeneration.Execution.Dim3 gridSize,
        KernelExecutionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MockCompiledKernel));
        }

        _executionCount++;
        _logger.LogDebugMessage($"Launching mock kernel {_name} (execution #{_executionCount}) with block size {blockSize.X}x{blockSize.Y}x{blockSize.Z} and grid size {gridSize.X}x{gridSize.Y}x{gridSize.Z}");

        // Simulate some async work
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        // Simulate processing based on grid and block sizes
        var totalWork = gridSize.X * gridSize.Y * gridSize.Z * blockSize.X * blockSize.Y * blockSize.Z;
        _logger.LogDebugMessage($"Mock kernel {_name} launched for {totalWork} total threads");
    }

    /// <summary>
    /// Disposes the mock kernel.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebugMessage($"Disposed mock kernel {_name} after {_executionCount} executions");
        }
    }
}