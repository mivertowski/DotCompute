// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Represents metadata for a cached CUDA kernel.
/// </summary>
public sealed class KernelMetadata
{
    /// <summary>
    /// Gets or sets the cache key for this kernel.
    /// </summary>
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the disk path where the kernel is stored.
    /// </summary>
    public string DiskPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compute capability this kernel was compiled for.
    /// </summary>
    public Version ComputeCapability { get; set; } = new Version(6, 0);

    /// <summary>
    /// Gets or sets the compilation time in milliseconds.
    /// </summary>
    public double CompilationTime { get; set; }

    /// <summary>
    /// Gets or sets when this kernel was compiled.
    /// </summary>
    public DateTimeOffset CompiledAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the file size of the cached kernel in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the last access time for LRU eviction.
    /// </summary>
    public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the access count for usage statistics.
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Gets or sets whether this kernel uses cubin format.
    /// </summary>
    public bool IsCubin { get; set; }

    /// <summary>
    /// Gets or sets the optimization level used during compilation.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;

    /// <summary>
    /// Gets or sets additional metadata as key-value pairs.
    /// </summary>
    public Dictionary<string, object> AdditionalMetadata { get; set; } = [];
}