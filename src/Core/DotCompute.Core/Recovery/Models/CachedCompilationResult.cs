// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Represents a cached compilation result that can be reused
/// </summary>
public class CachedCompilationResult
{
    /// <summary>
    /// Gets or sets the cache key for this compilation result
    /// </summary>
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source code hash
    /// </summary>
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compilation options used
    /// </summary>
    public CompilationOptions CompilationOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the compiled kernel data
    /// </summary>
    public byte[]? CompiledData { get; set; }

    /// <summary>
    /// Gets or sets the compilation timestamp
    /// </summary>
    public DateTimeOffset CompilationTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the expiration time for this cache entry
    /// </summary>
    public DateTimeOffset ExpirationTime { get; set; } = DateTimeOffset.UtcNow.AddHours(24);

    /// <summary>
    /// Gets or sets the compilation duration
    /// </summary>
    public TimeSpan CompilationDuration { get; set; }

    /// <summary>
    /// Gets or sets the number of times this cache entry has been accessed
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Gets or sets the last access time
    /// </summary>
    public DateTimeOffset LastAccessTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the size of the cached data (in bytes)
    /// </summary>
    public long DataSize { get; set; }

    /// <summary>
    /// Gets or sets the compilation statistics
    /// </summary>
    public CompilationStatistics? Statistics { get; set; }

    /// <summary>
    /// Gets or sets additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether this cache entry has expired
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow > ExpirationTime;

    /// <summary>
    /// Gets or sets the target backend for this compilation
    /// </summary>
    public string? TargetBackend { get; set; }

    /// <summary>
    /// Gets or sets the kernel name
    /// </summary>
    public string KernelName { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for compilation fallback operations
/// </summary>
public class CompilationFallbackConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of fallback attempts
    /// </summary>
    public int MaxFallbackAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout for each fallback attempt
    /// </summary>
    public TimeSpan FallbackTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets a value indicating whether caching is enabled
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache expiration time
    /// </summary>
    public TimeSpan CacheExpirationTime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the maximum cache size (in MB)
    /// </summary>
    public int MaxCacheSizeMB { get; set; } = 1024;

    /// <summary>
    /// Gets or sets a value indicating whether interpreted mode fallback is enabled
    /// </summary>
    public bool EnableInterpretedFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of fallback strategies to try in order
    /// </summary>
    public List<CompilationFallbackStrategy> FallbackStrategies { get; set; } = new()
    {
        CompilationFallbackStrategy.ReduceOptimization,
        CompilationFallbackStrategy.DisableFastMath,
        CompilationFallbackStrategy.ChangeBackend,
        CompilationFallbackStrategy.UseInterpretedMode
    };

    /// <summary>
    /// Gets or sets a value indicating whether performance monitoring is enabled
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum acceptable performance degradation percentage
    /// </summary>
    public double MaxAcceptablePerformanceDegradation { get; set; } = 50.0;
}