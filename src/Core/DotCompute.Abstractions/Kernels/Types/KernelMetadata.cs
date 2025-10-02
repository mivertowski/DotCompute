// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Kernels.Types;

/// <summary>
/// Contains metadata information about a kernel.
/// </summary>
public sealed class KernelMetadata
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the kernel entry point.
    /// </summary>
    public required string EntryPoint { get; init; }

    /// <summary>
    /// Gets the kernel parameters.
    /// </summary>
    public required IReadOnlyList<KernelParameterInfo> Parameters { get; init; }

    /// <summary>
    /// Gets the required work group size.
    /// </summary>
    public IReadOnlyList<long>? RequiredWorkGroupSize { get; init; }

    /// <summary>
    /// Gets the local memory size required.
    /// </summary>
    public long LocalMemorySize { get; init; }

    /// <summary>
    /// Gets additional kernel attributes.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Attributes { get; init; }
}

/// <summary>
/// Contains information about a kernel parameter.
/// </summary>
public sealed class KernelParameterInfo
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets whether the parameter is an input.
    /// </summary>
    public required bool IsInput { get; init; }

    /// <summary>
    /// Gets whether the parameter is an output.
    /// </summary>
    public required bool IsOutput { get; init; }

    /// <summary>
    /// Gets the parameter size in bytes, if applicable.
    /// </summary>
    public long? SizeInBytes { get; init; }
}

/// <summary>
/// Contains kernel cache statistics.
/// </summary>
public sealed class KernelCacheStatistics
{
    /// <summary>
    /// Gets the number of cached kernels.
    /// </summary>
    public required int CachedKernelCount { get; init; }

    /// <summary>
    /// Gets the cache hit rate.
    /// </summary>
    public required double HitRate { get; init; }

    /// <summary>
    /// Gets the cache miss count.
    /// </summary>
    public required long MissCount { get; init; }

    /// <summary>
    /// Gets the cache hit count.
    /// </summary>
    public required long HitCount { get; init; }

    /// <summary>
    /// Gets the total memory used by cache in bytes.
    /// </summary>
    public required long MemoryUsedBytes { get; init; }

    /// <summary>
    /// Gets the cache eviction count.
    /// </summary>
    public required long EvictionCount { get; init; }
}