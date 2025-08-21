// <copyright file="ICompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Operators.Models;

namespace DotCompute.Linq.Operators.Execution;

/// <summary>
/// Represents a compiled kernel ready for execution.
/// </summary>
public interface ICompiledKernel : IDisposable
{
    /// <summary>
    /// Executes the compiled kernel with the specified parameters.
    /// </summary>
    /// <param name="parameters">The kernel execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    Task ExecuteAsync(KernelExecutionParameters parameters, CancellationToken cancellationToken = default);
}