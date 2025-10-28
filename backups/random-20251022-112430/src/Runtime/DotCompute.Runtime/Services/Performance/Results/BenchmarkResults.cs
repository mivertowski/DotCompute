// <copyright file="BenchmarkResults.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Results;

/// <summary>
/// Results from a benchmark execution.
/// Contains performance measurements and metadata from a benchmark run.
/// </summary>
public class BenchmarkResults
{
    /// <summary>
    /// Gets the benchmark name.
    /// Identifies the specific benchmark that was executed.
    /// </summary>
    public required string BenchmarkName { get; init; }

    /// <summary>
    /// Gets the accelerator identifier.
    /// ID of the device on which the benchmark was run.
    /// </summary>
    public required string AcceleratorId { get; init; }

    /// <summary>
    /// Gets the execution timestamp.
    /// When the benchmark was executed.
    /// </summary>
    public DateTime ExecutionTime { get; init; }

    /// <summary>
    /// Gets the benchmark results.
    /// Key-value pairs of performance measurements.
    /// </summary>
    public Dictionary<string, double> Results { get; init; } = [];

    /// <summary>
    /// Gets additional metadata.
    /// Extra information about the benchmark execution context.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}