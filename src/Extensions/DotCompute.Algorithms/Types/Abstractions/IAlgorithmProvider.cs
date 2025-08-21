// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Abstractions;


/// <summary>
/// Base interface for algorithm providers.
/// </summary>
public interface IAlgorithmProvider
{
    public string Name { get; }
    public Version Version { get; }
    public bool IsSupported { get; }
}

/// <summary>
/// Interface for plugin-based algorithm providers.
/// </summary>
public interface IPluginAlgorithmProvider : IAlgorithmProvider
{
    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    public Task DisposeAsync();
}
