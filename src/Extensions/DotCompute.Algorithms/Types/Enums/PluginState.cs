// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Enums;

/// <summary>
/// Represents the operational state of a plugin.
/// </summary>
public enum PluginState
{
    /// <summary>
    /// Plugin has been discovered but not yet loaded.
    /// </summary>
    Discovered = 0,

    /// <summary>
    /// Plugin is currently being loaded.
    /// </summary>
    Loading = 1,

    /// <summary>
    /// Plugin has been loaded but not yet initialized.
    /// </summary>
    Loaded = 2,

    /// <summary>
    /// Plugin is currently being initialized.
    /// </summary>
    Initializing = 3,

    /// <summary>
    /// Plugin has been successfully initialized.
    /// </summary>
    Initialized = 4,

    /// <summary>
    /// Plugin is currently executing.
    /// </summary>
    Executing = 5,

    /// <summary>
    /// Plugin has been successfully initialized and is running.
    /// </summary>
    Running = 6,

    /// <summary>
    /// Plugin is currently being stopped.
    /// </summary>
    Stopping = 7,

    /// <summary>
    /// Plugin has been unloaded.
    /// </summary>
    Unloaded = 8,

    /// <summary>
    /// Plugin has failed to load or execute properly.
    /// </summary>
    Failed = 9
}