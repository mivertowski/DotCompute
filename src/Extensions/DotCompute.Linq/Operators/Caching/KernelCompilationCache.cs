// <copyright file="KernelCompilationCache.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Concurrent;
using global::System.Security.Cryptography;
using System.Text;
using DotCompute.Linq.Operators.Execution;
using DotCompute.Linq.Operators.Models;

namespace DotCompute.Linq.Operators.Caching;

/// <summary>
/// Static cache for compiled kernels to avoid recompilation.
/// </summary>
internal static class KernelCompilationCache
{
    private static readonly ConcurrentDictionary<string, WeakReference<ICompiledKernel>> _cache = new();

    /// <summary>
    /// Generates a cache key from a kernel compilation request.
    /// </summary>
    /// <param name="request">The compilation request.</param>
    /// <returns>A unique cache key for the request.</returns>
    public static string GenerateCacheKey(KernelCompilationRequest request)
    {
        var keyBuilder = new StringBuilder();
        _ = keyBuilder.Append(request.Name);
        _ = keyBuilder.Append('|');
        _ = keyBuilder.Append(request.Language);
        _ = keyBuilder.Append('|');
        _ = keyBuilder.Append(request.OptimizationLevel);
        _ = keyBuilder.Append('|');
        _ = keyBuilder.Append(request.TargetAccelerator?.Info.Name ?? "CPU");
        _ = keyBuilder.Append('|');
        
        // Hash the source code to keep the key manageable
        using var sha256 = SHA256.Create();
        var sourceBytes = Encoding.UTF8.GetBytes(request.Source);
        var hashBytes = sha256.ComputeHash(sourceBytes);
        var sourceHash = Convert.ToBase64String(hashBytes);
        _ = keyBuilder.Append(sourceHash);

        return keyBuilder.ToString();
    }

    /// <summary>
    /// Tries to get a cached compiled kernel.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="compiledKernel">The compiled kernel if found.</param>
    /// <returns>True if a valid cached kernel was found; otherwise, false.</returns>
    public static bool TryGetCached(string cacheKey, out ICompiledKernel? compiledKernel)
    {
        compiledKernel = null;

        if (_cache.TryGetValue(cacheKey, out var weakRef))
        {
            if (weakRef.TryGetTarget(out compiledKernel))
            {
                return true;
            }
            else
            {
                // The kernel was garbage collected, remove the stale entry
                _ = _cache.TryRemove(cacheKey, out _);
            }
        }

        return false;
    }

    /// <summary>
    /// Caches a compiled kernel.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="compiledKernel">The compiled kernel to cache.</param>
    public static void Cache(string cacheKey, ICompiledKernel compiledKernel)
    {
        if (compiledKernel != null)
        {
            _cache[cacheKey] = new WeakReference<ICompiledKernel>(compiledKernel);
        }
    }

    /// <summary>
    /// Clears the entire cache.
    /// </summary>
    public static void Clear() => _cache.Clear();

    /// <summary>
    /// Gets the current number of entries in the cache.
    /// </summary>
    public static int Count => _cache.Count;
}