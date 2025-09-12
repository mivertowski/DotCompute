// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Backends.Metal.Execution.Interfaces;

/// <summary>
/// Represents a compiled kernel ready for execution in the Metal backend.
/// This is a simple placeholder interface for the Metal backend.
/// </summary>
public interface ICompiledKernel : IDisposable
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the kernel is compiled and ready for execution.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Executes the compiled kernel with the specified parameters.
    /// </summary>
    /// <param name="parameters">The kernel execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    Task ExecuteAsync(object[] parameters, CancellationToken cancellationToken = default);
}