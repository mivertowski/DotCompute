// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;

namespace DotCompute.Core.Pipelines.Optimization.Models;

/// <summary>
/// Result of applying an optimization.
/// </summary>
internal sealed class OptimizationResult
{
    /// <summary>
    /// Gets whether the optimization was applied.
    /// </summary>
    public required bool WasApplied { get; init; }

    /// <summary>
    /// Gets the optimized stages.
    /// </summary>
    public required List<IPipelineStage> OptimizedStages { get; init; }

    /// <summary>
    /// Gets the optimization description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the affected stage IDs.
    /// </summary>
    public required IReadOnlyList<string> AffectedStages { get; init; }

    /// <summary>
    /// Gets the estimated performance impact (0-1).
    /// </summary>
    public required double EstimatedImpact { get; init; }

    /// <summary>
    /// Gets the estimated memory savings in bytes.
    /// </summary>
    public required long EstimatedMemorySavings { get; init; }
}