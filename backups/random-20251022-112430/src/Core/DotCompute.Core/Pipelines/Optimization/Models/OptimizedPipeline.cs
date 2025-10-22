// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Core.Pipelines.Optimization.Models;

/// <summary>
/// Result of an optimization operation.
/// </summary>
public sealed class OptimizedPipeline
{
    /// <summary>
    /// Gets the optimized pipeline.
    /// </summary>
    public required IKernelPipeline Pipeline { get; init; }

    /// <summary>
    /// Gets the list of applied optimizations.
    /// </summary>
    public required IReadOnlyList<AppliedOptimization> AppliedOptimizations { get; init; }

    /// <summary>
    /// Gets the estimated speedup factor.
    /// </summary>
    public required double EstimatedSpeedup { get; init; }

    /// <summary>
    /// Gets the estimated memory savings in bytes.
    /// </summary>
    public required long EstimatedMemorySavings { get; init; }
}

/// <summary>
/// Information about an applied optimization.
/// </summary>
public sealed class AppliedOptimization
{
    /// <summary>
    /// Gets the optimization type.
    /// </summary>
    public required OptimizationType Type { get; init; }

    /// <summary>
    /// Gets the optimization description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the affected stage IDs.
    /// </summary>
    public required IReadOnlyList<string> AffectedStages { get; init; }

    /// <summary>
    /// Gets the estimated performance impact.
    /// </summary>
    public required double EstimatedImpact { get; init; }
}