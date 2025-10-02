// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Abstractions;


/// <summary>
/// Base interface for algorithm providers.
/// </summary>
public interface IAlgorithmProvider
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; }
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    /// <value>The version.</value>
    public Version Version { get; }
    /// <summary>
    /// Gets or sets a value indicating whether supported.
    /// </summary>
    /// <value>The is supported.</value>
    public bool IsSupported { get; }
}

/// <summary>
/// Interface for plugin-based algorithm providers.
/// </summary>
public interface IPluginAlgorithmProvider : IAlgorithmProvider
{
    /// <summary>
    /// Initializes the async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public Task DisposeAsync();
}
