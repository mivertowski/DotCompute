// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Recovery.Types;

namespace DotCompute.Core.Recovery.Statistics;

/// <summary>
/// Comprehensive compilation statistics and performance metrics for kernel compilation operations.
/// Tracks success rates, fallback strategy effectiveness, error patterns, and caching performance.
/// </summary>
/// <remarks>
/// These statistics are essential for monitoring compilation health and optimizing
/// fallback strategies. They provide insights into:
/// - Overall compilation success rates and patterns
/// - Effectiveness of different fallback strategies
/// - Common error patterns that may require attention
/// - Cache performance and hit rates
/// - Average compilation attempts per kernel
/// 
/// The data is used for system optimization, alerting on compilation issues,
/// and making informed decisions about fallback strategy ordering.
/// </remarks>
public class CompilationStatistics
{
    /// <summary>
    /// Gets or sets the total number of kernels that have been processed for compilation.
    /// </summary>
    /// <value>The cumulative count of all kernel compilation attempts.</value>
    public int TotalKernels { get; set; }


    /// <summary>
    /// Gets or sets the number of kernels that were successfully compiled.
    /// </summary>
    /// <value>The count of kernels that achieved successful compilation.</value>
    public int SuccessfulCompilations { get; set; }


    /// <summary>
    /// Gets or sets the overall compilation success rate as a percentage (0.0 to 1.0).
    /// </summary>
    /// <value>The ratio of successful compilations to total compilation attempts.</value>
    public double SuccessRate { get; set; }


    /// <summary>
    /// Gets or sets the average number of compilation attempts required per kernel.
    /// This includes initial attempts and fallback retries.
    /// </summary>
    /// <value>The mean number of attempts across all kernels.</value>
    public double AverageAttemptsPerKernel { get; set; }


    /// <summary>
    /// Gets or sets the success rates for each fallback strategy.
    /// Maps each strategy to the number of times it resulted in successful compilation.
    /// </summary>
    /// <value>A dictionary of strategy types and their success counts.</value>
    public Dictionary<CompilationFallbackStrategy, int> StrategySuccessRates { get; } = [];


    /// <summary>
    /// Gets or sets the most common compilation errors and their occurrence counts.
    /// This helps identify systematic issues that may require code or configuration changes.
    /// </summary>
    /// <value>A dictionary of error messages and their frequency.</value>
    public Dictionary<string, int> MostCommonErrors { get; } = [];


    /// <summary>
    /// Gets or sets the current number of entries in the compilation result cache.
    /// </summary>
    /// <value>The count of cached compilation results.</value>
    public int CacheSize { get; set; }


    /// <summary>
    /// Gets or sets the cache hit rate as a percentage (0.0 to 1.0).
    /// This indicates how often cached results are used instead of recompiling.
    /// </summary>
    /// <value>The ratio of cache hits to total compilation requests.</value>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Returns a string representation of the compilation statistics.
    /// </summary>
    /// <returns>A formatted string containing key statistics values.</returns>
    public override string ToString()
        => $"Success={SuccessRate:P1} ({SuccessfulCompilations}/{TotalKernels}), " +
        $"Cache={CacheSize} entries ({CacheHitRate:P1} hit rate)";
}