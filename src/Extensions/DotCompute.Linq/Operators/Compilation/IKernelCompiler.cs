// <copyright file="IKernelCompiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Operators.Models;

namespace DotCompute.Linq.Operators.Compilation;

/// <summary>
/// Interface for kernel compilers used by LINQ operations.
/// </summary>
public interface IKernelCompiler
{
    /// <summary>
    /// Compiles a kernel asynchronously from the provided compilation request.
    /// </summary>
    /// <param name="request">The kernel compilation request containing source code and configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the kernel compilation result.</returns>
    Task<KernelCompilationResult> CompileKernelAsync(KernelCompilationRequest request, CancellationToken cancellationToken = default);
}