// <copyright file="PluginLoadResultType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Loaders.NuGet.Types;

/// <summary>
/// Defines the types of plugin load results.
/// Categorizes different outcomes of plugin loading operations.
/// </summary>
public enum PluginLoadResultType
{
    /// <summary>
    /// Plugin loaded successfully.
    /// The plugin is ready for use.
    /// </summary>
    Success,

    /// <summary>
    /// Plugin was already loaded.
    /// Attempted to load a plugin that is currently active.
    /// </summary>
    AlreadyLoaded,

    /// <summary>
    /// Plugin validation failed.
    /// The plugin did not meet validation requirements.
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// Dependency resolution failed.
    /// Required dependencies could not be resolved or loaded.
    /// </summary>
    DependencyResolutionFailed,

    /// <summary>
    /// General load error occurred.
    /// An unexpected error prevented plugin loading.
    /// </summary>
    LoadError,

    /// <summary>
    /// Plugin was not found.
    /// The specified plugin package does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// Incompatible plugin version.
    /// The plugin version is not compatible with the current system.
    /// </summary>
    IncompatibleVersion
}
