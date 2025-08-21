// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Services;

/// <summary>
/// Interface for plugin-specific services that can be injected into plugins
/// </summary>
public interface IPluginSpecificService
{
    /// <summary>
    /// Processes plugin-specific data or operations
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ProcessAsync();
}