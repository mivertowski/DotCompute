// <copyright file="PluginState.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Algorithms.Management.Types;

/// <summary>
/// Represents the current state of a loaded plugin.
/// Tracks the lifecycle stages of algorithm plugins in the system.
/// </summary>
public enum PluginState
{
    /// <summary>
    /// Plugin is loaded but not yet initialized.
    /// The assembly has been loaded but initialization has not been called.
    /// </summary>
    Loaded,

    /// <summary>
    /// Plugin is currently being initialized.
    /// The plugin's initialization method is executing.
    /// </summary>
    Initializing,

    /// <summary>
    /// Plugin has been successfully initialized and is ready for use.
    /// All required resources have been allocated and the plugin is operational.
    /// </summary>
    Initialized,

    /// <summary>
    /// Plugin is currently executing an algorithm.
    /// The plugin is actively processing a workload.
    /// </summary>
    Executing,

    /// <summary>
    /// Plugin has failed and is not operational.
    /// An error occurred during initialization or execution.
    /// </summary>
    Failed,

    /// <summary>
    /// Plugin is being unloaded from memory.
    /// Resources are being released and the assembly context is being disposed.
    /// </summary>
    Unloading,

    /// <summary>
    /// Plugin has been completely unloaded.
    /// All resources have been released and the plugin is no longer available.
    /// </summary>
    Unloaded,

    /// <summary>
    /// Plugin is temporarily suspended.
    /// The plugin is loaded but temporarily disabled for maintenance or updates.
    /// </summary>
    Suspended,

    /// <summary>
    /// Plugin is being updated or reloaded.
    /// A new version of the plugin is being loaded to replace the current one.
    /// </summary>
    Updating
}