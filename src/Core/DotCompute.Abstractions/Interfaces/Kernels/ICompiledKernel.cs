// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Abstractions.Interfaces.Kernels;

/// <summary>
/// Represents a compiled kernel ready for execution across all backends.
/// This is the unified interface for compiled kernels that abstracts away
/// backend-specific implementation details.
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
    /// Gets the backend type that compiled this kernel.
    /// </summary>
    string BackendType { get; }

    /// <summary>
    /// Executes the compiled kernel with the specified parameters.
    /// </summary>
    /// <param name="parameters">The kernel execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    Task ExecuteAsync(object[] parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets kernel metadata and properties.
    /// </summary>
    /// <returns>Kernel metadata.</returns>
    object GetMetadata();
}