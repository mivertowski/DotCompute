// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Interface for plugin health monitoring operations.
/// </summary>
public interface IHealthMonitor
{
    /// <summary>
    /// Starts health monitoring for all loaded plugins.
    /// </summary>
    void StartHealthMonitoring();

    /// <summary>
    /// Stops health monitoring.
    /// </summary>
    void StopHealthMonitoring();

    /// <summary>
    /// Performs health checks on all loaded plugins.
    /// </summary>
    /// <returns>A task representing the health check operation.</returns>
    Task PerformHealthChecksAsync();

    /// <summary>
    /// Checks the health of a single plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID to check.</param>
    /// <returns>A task representing the health check operation.</returns>
    Task CheckPluginHealthAsync(string pluginId);
}