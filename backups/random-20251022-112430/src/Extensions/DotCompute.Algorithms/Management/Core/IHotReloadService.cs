#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Interface for hot reload functionality.
/// </summary>
public interface IHotReloadService
{
    /// <summary>
    /// Sets up hot reload monitoring for a plugin assembly.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly to monitor.</param>
    public void SetupHotReload(string assemblyPath);

    /// <summary>
    /// Stops hot reload monitoring for a plugin assembly.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly to stop monitoring.</param>
    public void StopHotReload(string assemblyPath);

    /// <summary>
    /// Stops all hot reload monitoring.
    /// </summary>
    public void StopAllHotReload();
}