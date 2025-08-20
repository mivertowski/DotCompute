// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Linq.Expressions;


/// <summary>
/// Stores metadata for expression fusion operations.
/// </summary>
public sealed class FusionMetadataStore
{
private static readonly Lazy<FusionMetadataStore> _instance = new(() => new FusionMetadataStore());
private readonly ConcurrentDictionary<string, Dictionary<string, object>> _metadataCache = new();

private FusionMetadataStore() { }

/// <summary>
/// Gets the singleton instance of the metadata store.
/// </summary>
public static FusionMetadataStore Instance => _instance.Value;

    /// <summary>
    /// Gets metadata for the specified expression key.
    /// </summary>
    /// <param name="key">The expression key.</param>
    /// <returns>The metadata dictionary, or null if not found.</returns>
    public Dictionary<string, object>? GetMetadata(string key) => _metadataCache.TryGetValue(key, out var metadata) ? metadata : null;

    /// <summary>
    /// Sets metadata for the specified expression key.
    /// </summary>
    /// <param name="key">The expression key.</param>
    /// <param name="metadata">The metadata dictionary.</param>
    public void SetMetadata(string key, Dictionary<string, object> metadata) => _metadataCache.AddOrUpdate(key, metadata, (_, _) => metadata);

    /// <summary>
    /// Removes metadata for the specified expression key.
    /// </summary>
    /// <param name="key">The expression key.</param>
    /// <returns>True if the metadata was removed, false otherwise.</returns>
    public bool RemoveMetadata(string key) => _metadataCache.TryRemove(key, out _);

    /// <summary>
    /// Clears all cached metadata.
    /// </summary>
    public void Clear() => _metadataCache.Clear();

    /// <summary>
    /// Gets the number of cached metadata entries.
    /// </summary>
    public int Count => _metadataCache.Count;
}
