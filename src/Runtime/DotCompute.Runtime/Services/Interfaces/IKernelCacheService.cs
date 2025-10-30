// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;

namespace DotCompute.Runtime.Services.Interfaces;

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
