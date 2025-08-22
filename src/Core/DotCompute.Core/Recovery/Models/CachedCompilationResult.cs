// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Recovery.Types;
using DotCompute.Core.Recovery.Configuration;

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

