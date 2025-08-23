// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Interfaces;

/// <summary>
/// Defines the contract for plugin-specific services that provide specialized functionality for individual plugins.
/// This interface allows plugins to define and use their own custom services through dependency injection.
/// </summary>
/// <remarks>
/// This interface is designed to be:
/// - Extended by specific plugin implementations for custom functionality
/// - Registered with plugin-specific service containers
/// - Used for operations that are unique to a particular plugin or plugin type
/// - Implemented to support both synchronous and asynchronous processing patterns
/// </remarks>
public interface IPluginSpecificService
{
    /// <summary>
    /// Processes plugin-specific operations or data transformations.
    /// </summary>
    /// <returns>A task representing the asynchronous processing operation.</returns>
    /// <exception cref="PluginProcessingException">Thrown when processing fails due to plugin-specific issues.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service is not properly initialized.</exception>
    Task ProcessAsync();
}