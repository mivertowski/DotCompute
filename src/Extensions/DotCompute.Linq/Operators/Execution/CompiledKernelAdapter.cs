// <copyright file="CompiledKernelAdapter.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Linq.Operators.Models;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.KernelGeneration.Execution;
namespace DotCompute.Linq.Operators.Execution;
/// <summary>
/// Adapter for compiled kernels that bridges between LINQ and Core abstractions.
/// </summary>
internal class CompiledKernelAdapter : ICompiledKernel
{
    private readonly DotCompute.Abstractions.ICompiledKernel _coreKernel;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledKernelAdapter"/> class.
    /// </summary>
    /// <param name="coreKernel">The core compiled kernel to adapt.</param>
    public CompiledKernelAdapter(DotCompute.Abstractions.ICompiledKernel coreKernel)
    {
        _coreKernel = coreKernel ?? throw new ArgumentNullException(nameof(coreKernel));
    }
    /// <inheritdoc />
    public string Name => _coreKernel.Name;
    public string SourceCode => "// Adapted kernel from Core";
    public IReadOnlyList<KernelParameter> Parameters => new List<KernelParameter>();
    public string EntryPoint => _coreKernel.Name;
    /// Executes the compiled kernel with the specified parameters.
    /// <param name="parameters">The kernel execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    public async Task ExecuteAsync(KernelExecutionParameters parameters, CancellationToken cancellationToken = default)
        // Convert LINQ kernel execution parameters to Core kernel arguments
        var kernelArgs = new DotCompute.Abstractions.Kernels.KernelArguments(parameters.Arguments?.Count ?? 0);
        if (parameters.Arguments != null)
        {
            var index = 0;
            foreach (var kvp in parameters.Arguments)
            {
                kernelArgs.Set(index++, kvp.Value);
            }
        }
        // Set work dimensions as additional arguments if needed
        if (parameters.GlobalWorkSize != null && parameters.GlobalWorkSize.Length > 0)
            // Work dimensions are typically passed as kernel configuration, not arguments
            // They would be handled by the kernel execution context
        if (parameters.LocalWorkSize != null && parameters.LocalWorkSize.Length > 0)
            // Local work size is also handled by the execution context
        await _coreKernel.ExecuteAsync(kernelArgs, cancellationToken).ConfigureAwait(false);
    public async Task LaunchAsync(
        (int x, int y, int z) workgroupSize,
        (int x, int y, int z) globalSize,
        KernelExecutionParameters parameters,
        CancellationToken cancellationToken = default)
        // For adapting core kernels, we delegate to the standard ExecuteAsync method
        // The workgroup and global sizes are typically handled by the underlying kernel execution context
        await ExecuteAsync(parameters, cancellationToken).ConfigureAwait(false);
        int globalSize,
        int localSize,
        object[] args,
        // Convert object[] args to KernelExecutionParameters
        var parameters = new KernelExecutionParameters
            Arguments = []
        };
        // Add arguments with index-based keys
        for (var i = 0; i < args.Length; i++)
            parameters.Arguments[$"arg{i}"] = args[i];
        // Set work sizes
        parameters.GlobalWorkSize = new[] { globalSize };
        parameters.LocalWorkSize = new[] { localSize };
        DotCompute.Linq.KernelGeneration.Execution.Dim3 blockSize,
        DotCompute.Linq.KernelGeneration.Execution.Dim3 gridSize,
        // The block and grid sizes are typically handled by the underlying kernel execution context
    /// Disposes the adapter and the underlying compiled kernel.
    public void Dispose()
        if (!_disposed)
            _disposed = true;
            // Core kernel disposal is handled through its own lifecycle
}
