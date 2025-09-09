// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Configuration;

/// <summary>
/// Options for configuring the memory kernel cache.
/// </summary>
public class MemoryCacheOptions
{
    /// <summary>
    /// Maximum number of cache entries to maintain.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;

    /// <summary>
    /// Default expiration time for cache entries.
    /// </summary>
    public TimeSpan? DefaultExpiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether to enable cache statistics tracking.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Interval for cleaning up expired entries.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
}