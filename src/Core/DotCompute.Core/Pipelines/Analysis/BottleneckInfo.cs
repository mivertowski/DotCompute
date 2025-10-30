// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Pipelines.Analysis;

/// <summary>
/// Contains detailed information about a specific performance bottleneck identified during pipeline analysis.
/// Provides comprehensive data for understanding and addressing performance issues.
/// </summary>
public sealed class BottleneckInfo
{
    /// <summary>
    /// Gets the type classification of this bottleneck.
    /// Categorizes the bottleneck into predefined types for targeted optimization.
    /// </summary>
    /// <value>The bottleneck type from the BottleneckType enumeration.</value>
    public required BottleneckType Type { get; init; }

    /// <summary>
    /// Gets the specific location where this bottleneck occurs.
    /// Could be a stage name, kernel name, or other identifiable location within the pipeline.
    /// </summary>
    /// <value>The bottleneck location as a string.</value>
    public required string Location { get; init; }

    /// <summary>
    /// Gets the detailed description of this bottleneck.
    /// Explains the nature of the performance issue and its characteristics.
    /// </summary>
    /// <value>The bottleneck description as a string.</value>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the severity score of this bottleneck on a scale from 0.0 to 1.0.
    /// Higher values indicate more severe performance impact requiring urgent attention.
    /// </summary>
    /// <value>The severity score as a double between 0.0 and 1.0.</value>
    public required double Severity { get; init; }

    /// <summary>
    /// Gets the frequency of occurrence for this bottleneck across multiple executions.
    /// Higher values indicate bottlenecks that occur consistently across executions.
    /// </summary>
    /// <value>The frequency as a double between 0.0 and 1.0.</value>
    public required double Frequency { get; init; }
}
