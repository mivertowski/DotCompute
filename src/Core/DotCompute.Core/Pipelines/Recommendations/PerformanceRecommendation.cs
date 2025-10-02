// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Enums;

namespace DotCompute.Core.Pipelines.Recommendations;

/// <summary>
/// Represents a specific performance optimization recommendation for pipeline execution.
/// Provides actionable suggestions with impact estimates and implementation guidance.
/// </summary>
public sealed class PerformanceRecommendation
{
    /// <summary>
    /// Gets the type classification of this recommendation.
    /// Categorizes the recommendation into predefined optimization strategies.
    /// </summary>
    /// <value>The recommendation type from the RecommendationType enumeration.</value>
    public required RecommendationType Type { get; init; }

    /// <summary>
    /// Gets the detailed description of this recommendation.
    /// Explains what should be done to improve performance and why it would help.
    /// </summary>
    /// <value>The recommendation description as a string.</value>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the estimated performance improvement that could be achieved.
    /// Represents the potential speedup factor or percentage improvement.
    /// </summary>
    /// <value>The estimated improvement as a double (e.g., 1.5 = 50% improvement).</value>
    public required double EstimatedImprovement { get; init; }

    /// <summary>
    /// Gets the implementation difficulty level for this recommendation.
    /// Helps prioritize recommendations based on effort required versus benefit gained.
    /// </summary>
    /// <value>The implementation difficulty from the ImplementationDifficulty enumeration.</value>
    public required ImplementationDifficulty Difficulty { get; init; }

    /// <summary>
    /// Gets specific actions that should be taken to implement this recommendation.
    /// Provides concrete steps for applying the optimization suggestion.
    /// </summary>
    /// <value>A read-only list of action strings, or null if no specific actions are provided.</value>
    public IReadOnlyList<string>? Actions { get; init; }
}