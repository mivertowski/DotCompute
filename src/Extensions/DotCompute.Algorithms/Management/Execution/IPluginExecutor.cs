
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Abstractions;

namespace DotCompute.Algorithms.Management.Execution;

/// <summary>
/// Interface for plugin execution operations.
/// </summary>
public interface IPluginExecutor
{
    /// <summary>
    /// Executes a plugin with the specified inputs and enhanced monitoring.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public Task<object> ExecutePluginAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes plugin with retry logic for transient failures.
    /// </summary>
    /// <param name="plugin">The plugin to execute.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public Task<object> ExecuteWithRetryAsync(
        IAlgorithmPlugin plugin,
        object[] inputs,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken);
}
