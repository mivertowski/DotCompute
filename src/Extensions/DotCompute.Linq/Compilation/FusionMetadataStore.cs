// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Centralized store for operator fusion metadata.
/// </summary>
public sealed class FusionMetadataStore
{
    private static readonly Lazy<FusionMetadataStore> _instance = new(() => new FusionMetadataStore());
    private readonly ConcurrentDictionary<string, Dictionary<string, object>> _metadata = new();

    /// <summary>
    /// Gets the singleton instance of the fusion metadata store.
    /// </summary>
    public static FusionMetadataStore Instance => _instance.Value;

    private FusionMetadataStore()
    {
    }

    /// <summary>
    /// Gets fusion metadata for a given key (typically an expression string).
    /// </summary>
    /// <param name="key">The key to retrieve metadata for.</param>
    /// <returns>The metadata dictionary, or null if not found.</returns>
    public Dictionary<string, object>? GetMetadata(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _metadata.TryGetValue(key, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Stores fusion metadata for a given key.
    /// </summary>
    /// <param name="key">The key to store metadata under.</param>
    /// <param name="metadata">The metadata dictionary to store.</param>
    public void SetMetadata(string key, Dictionary<string, object> metadata)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(metadata);


        _metadata.AddOrUpdate(key, metadata, (_, _) => metadata);
    }

    /// <summary>
    /// Removes fusion metadata for a given key.
    /// </summary>
    /// <param name="key">The key to remove metadata for.</param>
    /// <returns>True if metadata was removed, false if the key was not found.</returns>
    public bool RemoveMetadata(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _metadata.TryRemove(key, out _);
    }

    /// <summary>
    /// Clears all fusion metadata.
    /// </summary>
    public void Clear()
    {
        _metadata.Clear();
    }

    /// <summary>
    /// Gets all stored metadata keys.
    /// </summary>
    /// <returns>A collection of all metadata keys.</returns>
    public IEnumerable<string> GetAllKeys()
    {
        return _metadata.Keys;
    }

    /// <summary>
    /// Checks if metadata exists for a given key.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if metadata exists, false otherwise.</returns>
    public bool HasMetadata(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _metadata.ContainsKey(key);
    }
}