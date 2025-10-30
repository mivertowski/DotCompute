// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Statistics;

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Kernel compilation statistics
/// </summary>
public class KernelCompilationStatistics
{
    /// <summary>
    /// Gets the total number of kernels compiled
    /// </summary>
    public long TotalCompilations { get; init; }

    /// <summary>
    /// Gets the number of successful compilations
    /// </summary>
    public long SuccessfulCompilations { get; init; }

    /// <summary>
    /// Gets the number of failed compilations
    /// </summary>
    public long FailedCompilations { get; init; }

    /// <summary>
    /// Gets the average compilation time
    /// </summary>
    public TimeSpan AverageCompilationTime { get; init; }

    /// <summary>
    /// Gets the total compilation time
    /// </summary>
    public TimeSpan TotalCompilationTime { get; init; }

    /// <summary>
    /// Gets compilation statistics by accelerator type
    /// </summary>
    public Dictionary<string, AcceleratorCompilationStats> ByAcceleratorType { get; init; } = [];

    /// <summary>
    /// Gets the timestamp when these statistics were collected
    /// </summary>
    public DateTime CollectedAt { get; init; } = DateTime.UtcNow;
}
