// <copyright file="PluginLifecycleStage.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Lifecycle;

/// <summary>
/// Represents the lifecycle stages of a plugin.
/// These stages define the various points at which plugin code can be executed during the plugin's lifetime.
/// </summary>
public enum PluginLifecycleStage
{
    /// <summary>
    /// Occurs before the plugin is loaded into memory.
    /// Use for pre-loading validation or environment checks.
    /// </summary>
    PreLoad,

    /// <summary>
    /// Occurs after the plugin has been loaded into memory.
    /// Use for post-loading initialization that doesn't require dependencies.
    /// </summary>
    PostLoad,

    /// <summary>
    /// Occurs before the plugin initialization begins.
    /// Use for preparing resources or validating prerequisites.
    /// </summary>
    PreInitialize,

    /// <summary>
    /// Occurs after the plugin has been initialized.
    /// Use for final setup steps after all dependencies are resolved.
    /// </summary>
    PostInitialize,

    /// <summary>
    /// Occurs before the plugin starts operation.
    /// Use for final preparations before the plugin becomes active.
    /// </summary>
    PreStart,

    /// <summary>
    /// Occurs after the plugin has started.
    /// Use for logging or notifications that the plugin is now active.
    /// </summary>
    PostStart,

    /// <summary>
    /// Occurs before the plugin stops operation.
    /// Use for graceful shutdown preparation.
    /// </summary>
    PreStop,

    /// <summary>
    /// Occurs after the plugin has stopped.
    /// Use for cleanup that doesn't affect plugin unloading.
    /// </summary>
    PostStop,

    /// <summary>
    /// Occurs before the plugin is unloaded from memory.
    /// Use for releasing resources and final cleanup.
    /// </summary>
    PreUnload,

    /// <summary>
    /// Occurs after the plugin has been unloaded.
    /// Use for final logging or external cleanup.
    /// </summary>
    PostUnload,

    /// <summary>
    /// Occurs when the plugin's configuration has changed.
    /// Use for reloading settings and adjusting behavior.
    /// </summary>
    ConfigurationChanged,

    /// <summary>
    /// Occurs when a health check is requested.
    /// Use for reporting plugin status and diagnostics.
    /// </summary>
    HealthCheck,

    /// <summary>
    /// Occurs during cleanup operations.
    /// Use for releasing temporary resources or resetting state.
    /// </summary>
    Cleanup
}