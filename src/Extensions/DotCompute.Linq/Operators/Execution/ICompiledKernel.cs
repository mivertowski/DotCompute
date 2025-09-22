// <copyright file="ICompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Operators.Models;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.KernelGeneration.Execution;
namespace DotCompute.Linq.Operators.Execution;
{
/// <summary>
/// Represents a compiled kernel ready for execution.
/// </summary>
public interface ICompiledKernel : IDisposable
{
    /// <summary>
    /// Gets the name of the compiled kernel.
    /// </summary>
    string Name { get; }
    /// Gets the compiled kernel source code.
    string SourceCode { get; }
    /// Gets the kernel parameters.
    IReadOnlyList<KernelParameter> Parameters { get; }
    /// Gets the kernel entry point name.
    string EntryPoint { get; }
    /// Executes the compiled kernel with the specified parameters.
    /// <param name="parameters">The kernel execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    public Task ExecuteAsync(KernelExecutionParameters parameters, CancellationToken cancellationToken = default);
    /// Launches the kernel asynchronously with specified dimensions and parameters.
    /// <param name="workgroupSize">The workgroup size for kernel execution.</param>
    /// <param name="globalSize">The global size for kernel execution.</param>
    /// <returns>A task representing the asynchronous kernel launch.</returns>
    public Task LaunchAsync(
        {
        (int x, int y, int z) workgroupSize,
        (int x, int y, int z) globalSize,
        KernelExecutionParameters parameters,
        CancellationToken cancellationToken = default);
    /// Launches the kernel asynchronously with specified dimensions.
    /// <param name="globalSize">The global work size.</param>
    /// <param name="localSize">The local work size.</param>
    /// <param name="args">The kernel arguments.</param>
        int globalSize,
        int localSize,
        object[] args,
    /// Launches the kernel asynchronously with basic parameters.
    /// Launches the kernel asynchronously with Dim3 block and grid sizes.
    /// <param name="blockSize">The block size dimensions.</param>
    /// <param name="gridSize">The grid size dimensions.</param>
        DotCompute.Linq.KernelGeneration.Execution.Dim3 blockSize,
        DotCompute.Linq.KernelGeneration.Execution.Dim3 gridSize,
}
