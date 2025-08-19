// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Abstractions;


/// <summary>
/// Base interface for algorithm providers.
/// </summary>
public interface IAlgorithmProvider
{
string Name { get; }
Version Version { get; }
bool IsSupported { get; }
}

/// <summary>
/// Interface for plugin-based algorithm providers.
/// </summary>
public interface IPluginAlgorithmProvider : IAlgorithmProvider
{
Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
Task DisposeAsync();
}
