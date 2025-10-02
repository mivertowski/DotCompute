// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Recovery.Models;
/// <summary>
/// Represents the result of a compilation fallback operation
/// </summary>
public class CompilationFallbackResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the fallback was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the compilation options that were used successfully
    /// </summary>
    public CompilationOptions? SuccessfulOptions { get; set; }

    /// <summary>
    /// Gets or sets the fallback strategy that was used
    /// </summary>
    public Types.CompilationFallbackStrategy Strategy { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts made before success or failure
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the total time taken for the fallback process
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the compiled kernel if successful
    /// </summary>
    public object? CompiledKernel { get; set; }

    /// <summary>
    /// Gets or sets the final error if all fallback attempts failed
    /// </summary>
    public Exception? FinalError { get; set; }

    /// <summary>
    /// Gets or sets the list of errors encountered during fallback attempts
    /// </summary>
    public IList<Exception> AttemptErrors { get; } = [];

    /// <summary>
    /// Gets or sets additional metadata about the fallback process
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];

    /// <summary>
    /// Gets or sets the compilation statistics
    /// </summary>
    public CompilationStatistics? Statistics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the result uses an interpreted fallback
    /// </summary>
    public bool IsInterpretedFallback { get; set; }

    /// <summary>
    /// Gets or sets the performance impact of using the fallback
    /// </summary>
    public PerformanceImpact? PerformanceImpact { get; set; }
}

/// <summary>
/// Represents compilation statistics
/// </summary>
public class CompilationStatistics
{
    /// <summary>
    /// Gets or sets the total compilation time
    /// </summary>
    public TimeSpan CompilationTime { get; set; }

    /// <summary>
    /// Gets or sets the memory usage during compilation
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the number of compilation passes
    /// </summary>
    public int CompilationPasses { get; set; }

    /// <summary>
    /// Gets or sets the size of the compiled output
    /// </summary>
    public long OutputSize { get; set; }

    /// <summary>
    /// Gets or sets the optimization level used
    /// </summary>
    public string OptimizationLevel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compilation warnings
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets or sets additional compilation metrics
    /// </summary>
    public Dictionary<string, object> Metrics { get; } = [];
}

/// <summary>
/// Represents the performance impact of using a fallback strategy
/// </summary>
public class PerformanceImpact
{
    /// <summary>
    /// Gets or sets the estimated performance degradation as a percentage
    /// </summary>
    public double PerformanceDegradation { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory overhead
    /// </summary>
    public long MemoryOverhead { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the impact is acceptable
    /// </summary>
    public bool IsAcceptable { get; set; } = true;

    /// <summary>
    /// Gets or sets additional impact details
    /// </summary>
    public Dictionary<string, object> Details { get; } = [];
}