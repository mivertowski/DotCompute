// <copyright file="IKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.Operators.Types;

namespace DotCompute.Linq.Operators.Interfaces;

/// <summary>
/// Represents a compiled kernel ready for execution.
/// </summary>
public interface IKernel : IDisposable
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the kernel properties.
    /// </summary>
    KernelProperties Properties { get; }

    /// <summary>
    /// Compiles the kernel asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the compilation operation.</returns>
    Task CompileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the kernel with the specified work items and parameters.
    /// </summary>
    /// <param name="workItems">The work dimensions for execution.</param>
    /// <param name="parameters">The kernel parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the execution operation.</returns>
    Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about the kernel parameters.
    /// </summary>
    /// <returns>A read-only list of kernel parameters.</returns>
    IReadOnlyList<KernelParameter> GetParameterInfo();
}