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

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public OptimizationType OptimizationType => OptimizationType.None; // Custom optimizations don't have a specific type

    public Task<IKernelPipeline> ApplyAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default) => _optimizationLogic(pipeline);
}