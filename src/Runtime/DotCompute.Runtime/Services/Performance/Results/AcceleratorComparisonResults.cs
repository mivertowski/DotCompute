// <copyright file="AcceleratorComparisonResults.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Results;

/// <summary>
/// Results from comparing performance across multiple accelerators.
/// Provides comparative analysis of different compute devices.
/// </summary>
public class AcceleratorComparisonResults
{
    /// <summary>
    /// Gets the list of accelerator identifiers.
    /// IDs of all devices included in the comparison.
    /// </summary>
    public required IReadOnlyList<string> AcceleratorIds { get; init; }

    /// <summary>
    /// Gets the benchmark results by accelerator.
    /// Performance data for each device in the comparison.
    /// </summary>
    public Dictionary<string, BenchmarkResults> Results { get; init; } = [];

    /// <summary>
    /// Gets the performance rankings.
    /// Relative ranking of each accelerator (1 = best).
    /// </summary>
    public Dictionary<string, int> Rankings { get; init; } = [];
}
