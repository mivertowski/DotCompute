// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types.Abstractions;

namespace DotCompute.Algorithms.Types.Management
{

/// <summary>
/// Manages algorithm plugins.
/// </summary>
public sealed class AlgorithmPluginManager : IDisposable
{
    private readonly List<IPluginAlgorithmProvider> _plugins = new();
    private bool _disposed;

    /// <summary>
    /// Load a plugin from the specified path.
    /// </summary>
    public Task<bool> LoadPluginAsync(string path, CancellationToken cancellationToken = default)
    {
        // Placeholder implementation
        return Task.FromResult(true);
    }

    /// <summary>
    /// Get all loaded plugins.
    /// </summary>
    public IReadOnlyList<IPluginAlgorithmProvider> GetPlugins() => _plugins;

    public void Dispose()
    {
        if (_disposed) return;
        
        foreach (var plugin in _plugins)
        {
            plugin.DisposeAsync().AsTask().Wait();
        }
        _plugins.Clear();
        _disposed = true;
    }
}}
