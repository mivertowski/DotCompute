// <copyright file="PluginLifecycleHookAttribute.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Lifecycle;

/// <summary>
/// Marks a method as a plugin lifecycle hook.
/// Lifecycle hooks allow plugins to execute code at specific points during their lifecycle.
/// </summary>
/// <remarks>
/// The method marked with this attribute should be public and can be either void or return a Task for async operations.
/// The plugin system will automatically invoke these methods at the appropriate lifecycle stage.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginLifecycleHookAttribute"/> class.
/// </remarks>
/// <param name="stage">The lifecycle stage for this hook.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PluginLifecycleHookAttribute(PluginLifecycleStage stage) : Attribute
{
    /// <summary>
    /// Gets the lifecycle stage for this hook.
    /// Determines when this method will be invoked during the plugin lifecycle.
    /// </summary>
    public PluginLifecycleStage Stage { get; } = stage;

    /// <summary>
    /// Gets or sets the priority for hook execution.
    /// Higher values are executed first within the same stage. Default is 0.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets whether errors in this hook should prevent plugin loading.
    /// When true, any exception thrown will stop the plugin from loading.
    /// When false, exceptions are logged but plugin loading continues.
    /// </summary>
    public bool IsCritical { get; set; }
}
