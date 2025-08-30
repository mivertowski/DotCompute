// <copyright file="KernelCacheConfig.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Configuration for kernel compilation cache.
/// </summary>
public sealed class KernelCacheConfig
{
    /// <summary>
    /// Gets or sets the maximum number of cached kernels.
    /// </summary>
    public int MaxCachedKernels { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the cache directory path.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether disk caching is enabled.
    /// </summary>
    public bool EnableDiskCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache expiry time in hours.
    /// </summary>
    public int CacheExpiryHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the disk cache path for compiled kernels.
    /// If null, uses the default system temp directory.
    /// </summary>
    public string? DiskCachePath { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory cache size in megabytes.
    /// </summary>
    public int MaxMemoryCacheSizeMB { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum disk cache size in megabytes.
    /// </summary>
    public int MaxDiskCacheSizeMB { get; set; } = 1024;
}