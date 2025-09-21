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
    public string Name { get; }
    /// Gets the kernel properties.
    public KernelProperties Properties { get; }
    /// Compiles the kernel asynchronously.
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the compilation operation.</returns>
    public Task CompileAsync(CancellationToken cancellationToken = default);
    /// Executes the kernel with the specified work items and parameters.
    /// <param name="workItems">The work dimensions for execution.</param>
    /// <param name="parameters">The kernel parameters.</param>
    /// <returns>A task representing the execution operation.</returns>
    public Task ExecuteAsync(WorkItems workItems, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
    /// Gets information about the kernel parameters.
    /// <returns>A read-only list of kernel parameters.</returns>
    public IReadOnlyList<KernelParameter> GetParameterInfo();
}
