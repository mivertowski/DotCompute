// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Recovery.Types;

namespace DotCompute.Core.Recovery.Configuration;

/// <summary>
/// Configuration for compilation fallback behavior that handles kernel compilation failures
/// through progressive degradation strategies and caching mechanisms.
/// </summary>
/// <remarks>
/// This configuration defines how the system should respond to compilation failures by:
/// - Implementing progressive fallback strategies
/// - Managing compilation result caching
/// - Enabling kernel simplification and interpreter modes
/// - Configuring cache expiration and cleanup policies
/// </remarks>
public class CompilationFallbackConfiguration
{
    /// <summary>
    /// Gets or sets the duration after which cached compilation results expire.
    /// </summary>
    /// <value>The default value is 4 hours.</value>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(4);


    /// <summary>
    /// Gets or sets the interval at which expired cache entries are cleaned up.
    /// </summary>
    /// <value>The default value is 1 hour.</value>
    public TimeSpan CacheCleanupInterval { get; set; } = TimeSpan.FromHours(1);


    /// <summary>
    /// Gets or sets the maximum number of entries allowed in the compilation cache.
    /// </summary>
    /// <value>The default value is 1000.</value>
    public int MaxCacheEntries { get; set; } = 1000;


    /// <summary>
    /// Gets or sets a value indicating whether progressive fallback is enabled.
    /// When enabled, the system will try multiple fallback strategies in sequence.
    /// </summary>
    /// <value>The default value is true.</value>
    public bool EnableProgressiveFallback { get; set; } = true;


    /// <summary>
    /// Gets or sets a value indicating whether kernel simplification is enabled.
    /// When enabled, complex kernels may be simplified to improve compilation success.
    /// </summary>
    /// <value>The default value is true.</value>
    public bool EnableKernelSimplification { get; set; } = true;


    /// <summary>
    /// Gets or sets a value indicating whether interpreter mode is enabled as a fallback.
    /// When enabled, kernels that cannot be compiled may be executed in interpreter mode.
    /// </summary>
    /// <value>The default value is true.</value>
    public bool EnableInterpreterMode { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of fallback strategies to attempt in order when compilation fails.
    /// </summary>
    /// <value>
    /// The default strategies include:
    /// <see cref="CompilationFallbackStrategy.ReduceOptimizations"/>,
    /// <see cref="CompilationFallbackStrategy.DisableFastMath"/>,
    /// <see cref="CompilationFallbackStrategy.SimplifyKernel"/>,
    /// <see cref="CompilationFallbackStrategy.AlternativeCompiler"/>,
    /// <see cref="CompilationFallbackStrategy.InterpreterMode"/>
    /// </value>
    public List<CompilationFallbackStrategy> FallbackStrategies { get; set; } =
    [
        CompilationFallbackStrategy.ReduceOptimizations,
        CompilationFallbackStrategy.DisableFastMath,
        CompilationFallbackStrategy.SimplifyKernel,
        CompilationFallbackStrategy.AlternativeCompiler,
        CompilationFallbackStrategy.InterpreterMode
    ];

    /// <summary>
    /// Gets the default configuration instance.
    /// </summary>
    /// <value>A new instance of <see cref="CompilationFallbackConfiguration"/> with default values.</value>
    public static CompilationFallbackConfiguration Default => new();

    /// <summary>
    /// Returns a string representation of the configuration.
    /// </summary>
    /// <returns>A string containing key configuration values.</returns>
    public override string ToString()
        => $"CacheExpiration={CacheExpiration}, Strategies={FallbackStrategies.Count}, ProgressiveFallback={EnableProgressiveFallback}";
}