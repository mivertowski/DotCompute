// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Accelerators;
using DotCompute.Abstractions.Kernels;

namespace DotCompute.Runtime.Services;


/// <summary>
/// Service for compiling kernels with caching and optimization
/// </summary>
public interface IKernelCompilerService
{
    /// <summary>
    /// Compiles a kernel with optimizations and caching
    /// </summary>
    /// <param name="definition">The kernel definition</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <param name="options">Compilation options</param>
    /// <returns>The compiled kernel</returns>
    public Task<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        IAccelerator accelerator,
        CompilationOptions? options = null);

    /// <summary>
    /// Pre-compiles kernels for better runtime performance
    /// </summary>
    /// <param name="definitions">The kernel definitions to pre-compile</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>A task representing the pre-compilation operation</returns>
    public Task PrecompileAsync(IEnumerable<KernelDefinition> definitions, IAccelerator accelerator);

    /// <summary>
    /// Gets compilation statistics
    /// </summary>
    /// <returns>Compilation statistics</returns>
    public KernelCompilationStatistics GetStatistics();

    /// <summary>
    /// Optimizes a kernel definition for the target accelerator
    /// </summary>
    /// <param name="definition">The kernel definition</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>The optimized kernel definition</returns>
    public Task<KernelDefinition> OptimizeAsync(KernelDefinition definition, IAccelerator accelerator);

    /// <summary>
    /// Validates a kernel definition for compilation
    /// </summary>
    /// <param name="definition">The kernel definition</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>Validation result</returns>
    public Task<KernelValidationResult> ValidateAsync(KernelDefinition definition, IAccelerator accelerator);
}

/// <summary>
/// Service for caching compiled kernels
/// </summary>
public interface IKernelCacheService
{
    /// <summary>
    /// Gets a cached kernel if available
    /// </summary>
    /// <param name="cacheKey">The cache key</param>
    /// <returns>The cached kernel or null if not found</returns>
    public Task<ICompiledKernel?> GetAsync(string cacheKey);

    /// <summary>
    /// Stores a compiled kernel in the cache
    /// </summary>
    /// <param name="cacheKey">The cache key</param>
    /// <param name="kernel">The compiled kernel</param>
    /// <returns>A task representing the store operation</returns>
    public Task StoreAsync(string cacheKey, ICompiledKernel kernel);

    /// <summary>
    /// Generates a cache key for a kernel definition and accelerator
    /// </summary>
    /// <param name="definition">The kernel definition</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <param name="options">Compilation options</param>
    /// <returns>The cache key</returns>
    public string GenerateCacheKey(KernelDefinition definition, IAccelerator accelerator, CompilationOptions? options);

    /// <summary>
    /// Clears the kernel cache
    /// </summary>
    /// <returns>A task representing the clear operation</returns>
    public Task ClearAsync();

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    /// <returns>Cache statistics</returns>
    public KernelCacheStatistics GetStatistics();

    /// <summary>
    /// Evicts old or unused cached kernels
    /// </summary>
    /// <returns>The number of evicted kernels</returns>
    public Task<int> EvictAsync();
}

/// <summary>
/// Algorithm plugin manager interface
/// </summary>
public interface IAlgorithmPluginManager
{
    /// <summary>
    /// Loads all algorithm plugins from configured directories
    /// </summary>
    /// <returns>A task representing the load operation</returns>
    public Task LoadPluginsAsync();

    /// <summary>
    /// Gets a plugin by its ID
    /// </summary>
    /// <typeparam name="T">The plugin interface type</typeparam>
    /// <param name="pluginId">The plugin ID</param>
    /// <returns>The plugin instance or null if not found</returns>
    public Task<T?> GetPluginAsync<T>(string pluginId) where T : class;

    /// <summary>
    /// Gets all plugins of a specific type
    /// </summary>
    /// <typeparam name="T">The plugin interface type</typeparam>
    /// <returns>All plugins of the specified type</returns>
    public Task<IEnumerable<T>> GetPluginsAsync<T>() where T : class;

    /// <summary>
    /// Registers a plugin instance
    /// </summary>
    /// <typeparam name="T">The plugin interface type</typeparam>
    /// <param name="pluginId">The plugin ID</param>
    /// <param name="plugin">The plugin instance</param>
    /// <returns>A task representing the registration operation</returns>
    public Task RegisterPluginAsync<T>(string pluginId, T plugin) where T : class;

    /// <summary>
    /// Unloads a plugin
    /// </summary>
    /// <param name="pluginId">The plugin ID</param>
    /// <returns>A task representing the unload operation</returns>
    public Task UnloadPluginAsync(string pluginId);

    /// <summary>
    /// Gets plugin information
    /// </summary>
    /// <returns>Information about all loaded plugins</returns>
    public IEnumerable<PluginInfo> GetPluginInfo();

    /// <summary>
    /// Reloads all plugins (if hot reload is enabled)
    /// </summary>
    /// <returns>A task representing the reload operation</returns>
    public Task ReloadPluginsAsync();
}

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

/// <summary>
/// Kernel cache statistics
/// </summary>
public class KernelCacheStatistics
{
    /// <summary>
    /// Gets the total number of cache requests
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Gets the number of cache hits
    /// </summary>
    public long CacheHits { get; init; }

    /// <summary>
    /// Gets the number of cache misses
    /// </summary>
    public long CacheMisses { get; init; }

    /// <summary>
    /// Gets the cache hit rate
    /// </summary>
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0.0;

    /// <summary>
    /// Gets the number of cached kernels
    /// </summary>
    public int CachedKernelCount { get; init; }

    /// <summary>
    /// Gets the total cache size in bytes
    /// </summary>
    public long TotalCacheSize { get; init; }

    /// <summary>
    /// Gets the number of cache evictions
    /// </summary>
    public long EvictionCount { get; init; }

    /// <summary>
    /// Gets the timestamp when these statistics were collected
    /// </summary>
    public DateTime CollectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Kernel validation result
/// </summary>
public class KernelValidationResult
{
    /// <summary>
    /// Gets whether the kernel is valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets validation errors
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets validation warnings
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets estimated resource requirements
    /// </summary>
    public KernelResourceRequirements? ResourceRequirements { get; init; }

    /// <summary>
    /// Gets performance predictions
    /// </summary>
    public Dictionary<string, double> PerformancePredictions { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <param name="resourceRequirements">Resource requirements</param>
    /// <param name="performancePredictions">Performance predictions</param>
    /// <returns>A successful validation result</returns>
    public static KernelValidationResult Success(
        KernelResourceRequirements? resourceRequirements = null,
        Dictionary<string, double>? performancePredictions = null)
        => new()
        {
            IsValid = true,
            ResourceRequirements = resourceRequirements,
            PerformancePredictions = performancePredictions ?? []
        };

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    /// <param name="errors">Validation errors</param>
    /// <param name="warnings">Validation warnings</param>
    /// <returns>A failed validation result</returns>
    public static KernelValidationResult Failure(
        IEnumerable<string> errors,
        IEnumerable<string>? warnings = null)
        => new()
        {
            IsValid = false,
            Errors = errors.ToList(),
            Warnings = warnings?.ToList() ?? []
        };
}

/// <summary>
/// Kernel resource requirements
/// </summary>
public class KernelResourceRequirements
{
    /// <summary>
    /// Gets the required memory in bytes
    /// </summary>
    public long RequiredMemoryBytes { get; init; }

    /// <summary>
    /// Gets the required shared memory in bytes
    /// </summary>
    public long RequiredSharedMemoryBytes { get; init; }

    /// <summary>
    /// Gets the required compute capability
    /// </summary>
    public Version? RequiredComputeCapability { get; init; }

    /// <summary>
    /// Gets the required accelerator features
    /// </summary>
    public AcceleratorFeature RequiredFeatures { get; init; }

    /// <summary>
    /// Gets the estimated execution time
    /// </summary>
    public TimeSpan? EstimatedExecutionTime { get; init; }

    /// <summary>
    /// Gets additional resource metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Plugin information
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// Gets the plugin ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the plugin name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the plugin version
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// Gets the plugin description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the plugin author
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the plugin assembly path
    /// </summary>
    public string? AssemblyPath { get; init; }

    /// <summary>
    /// Gets the plugin load time
    /// </summary>
    public DateTime LoadTime { get; init; }

    /// <summary>
    /// Gets whether the plugin is currently loaded
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// Gets the supported interfaces
    /// </summary>
    public IReadOnlyList<Type> SupportedInterfaces { get; init; } = Array.Empty<Type>();

    /// <summary>
    /// Gets plugin-specific metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
