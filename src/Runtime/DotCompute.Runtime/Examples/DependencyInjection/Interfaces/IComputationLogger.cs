// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Interfaces;

/// <summary>
/// Defines the contract for computation logging services that track plugin and service operations.
/// This interface provides structured logging for compute operations, initialization, and execution metrics.
/// </summary>
/// <remarks>
/// Implementations should:
/// - Use structured logging with consistent message formats
/// - Include relevant context information (plugin IDs, data sizes, execution times)
/// - Support different log levels based on operation importance
/// - Handle logging failures gracefully without impacting compute operations
/// </remarks>
public interface IComputationLogger
{
    /// <summary>
    /// Logs the initialization of a plugin or compute service.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin or service being initialized.</param>
    /// <param name="dataSize">The size of initialization data in bytes.</param>
    /// <returns>A task representing the asynchronous logging operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pluginId"/> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="dataSize"/> is negative.</exception>
    Task LogInitializationAsync(string pluginId, int dataSize);

    /// <summary>
    /// Logs the execution of a computation operation.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin or service executing the operation.</param>
    /// <param name="inputSize">The size of input data being processed.</param>
    /// <returns>A task representing the asynchronous logging operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pluginId"/> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="inputSize"/> is negative.</exception>
    Task LogExecutionAsync(string pluginId, int inputSize);
}