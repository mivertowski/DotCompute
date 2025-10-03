// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Statistics;

/// <summary>
/// Compilation statistics for a specific accelerator type
/// </summary>
public class AcceleratorCompilationStats
{
    /// <summary>
    /// Gets the accelerator type
    /// </summary>
    public required string AcceleratorType { get; init; }

    /// <summary>
    /// Gets the number of compilations for this accelerator type
    /// </summary>
    public long CompilationCount { get; init; }

    /// <summary>
    /// Gets the average compilation time for this accelerator type
    /// </summary>
    public TimeSpan AverageCompilationTime { get; init; }

    /// <summary>
    /// Gets the success rate for this accelerator type
    /// </summary>
    public double SuccessRate { get; init; }
}
