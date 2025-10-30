// <copyright file="BenchmarkDefinition.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Results;

/// <summary>
/// Defines a benchmark configuration.
/// Specifies the parameters and settings for a performance benchmark.
/// </summary>
public class BenchmarkDefinition
{
    /// <summary>
    /// Gets the benchmark name.
    /// Unique identifier for this benchmark configuration.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the benchmark description.
    /// Detailed explanation of what this benchmark measures.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the benchmark parameters.
    /// Configuration settings and input parameters for the benchmark.
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = [];
}
