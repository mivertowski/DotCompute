// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;

namespace DotCompute.Core.Pipelines.Optimization.Models;

/// <summary>
/// Custom optimization pass implementation.
/// </summary>
internal sealed class CustomOptimizationPass(string name, Func<IKernelPipeline, Task<IKernelPipeline>> optimizationLogic) : IOptimizationPass
{
    private readonly Func<IKernelPipeline, Task<IKernelPipeline>> _optimizationLogic = optimizationLogic ?? throw new ArgumentNullException(nameof(optimizationLogic));
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    /// <summary>
    /// Gets or sets the optimization type.
    /// </summary>
    /// <value>The optimization type.</value>

    public OptimizationType OptimizationType => OptimizationType.None; // Custom optimizations don't have a specific type
    /// <summary>
    /// Gets apply asynchronously.
    /// </summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default) => _optimizationLogic(pipeline);
}
