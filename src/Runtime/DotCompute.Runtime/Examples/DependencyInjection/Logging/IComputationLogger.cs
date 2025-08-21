// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Logging;

/// <summary>
/// Interface for computation logging services
/// </summary>
public interface IComputationLogger
{
    /// <summary>
    /// Logs plugin initialization information
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    /// <param name="dataSize">The size of initialization data</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task LogInitializationAsync(string pluginId, int dataSize);

    /// <summary>
    /// Logs plugin execution information
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    /// <param name="inputSize">The size of input data</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task LogExecutionAsync(string pluginId, int inputSize);
}