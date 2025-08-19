// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types.Abstractions;

namespace DotCompute.Algorithms.Types.Management
{

/// <summary>
/// Manages algorithm plugins and their lifecycle.
/// </summary>
public sealed class PluginManager : IDisposable
{
    private readonly Dictionary<string, IPluginAlgorithmProvider> _providers = new();
    private bool _disposed;

    public IReadOnlyDictionary<string, IPluginAlgorithmProvider> Providers => _providers;

    public Task<bool> LoadPluginAsync(string path, CancellationToken cancellationToken = default)
    {
        // Implementation for loading plugins
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        foreach (var provider in _providers.Values)
        {
            provider.DisposeAsync().AsTask().Wait();
        }
        
        _providers.Clear();
        _disposed = true;
    }
}}
