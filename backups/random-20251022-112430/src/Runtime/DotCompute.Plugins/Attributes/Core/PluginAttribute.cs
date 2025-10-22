// <copyright file="PluginAttribute.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Core;

/// <summary>
/// Marks a class as a DotCompute plugin.
/// This attribute identifies classes that should be loaded and managed as plugins by the DotCompute plugin system.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PluginAttribute : Attribute
{
    /// <summary>
    /// Gets the unique identifier for the plugin.
    /// This ID is used to reference the plugin throughout the system.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets or sets the display name of the plugin.
    /// This is the human-readable name shown in UI and logs.
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    /// Gets or sets the version of the plugin.
    /// Uses semantic versioning (MAJOR.MINOR.PATCH).
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the description of the plugin.
    /// Provides detailed information about the plugin's functionality.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the author of the plugin.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the website URL for the plugin.
    /// Can point to documentation, support, or the plugin's homepage.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the license for the plugin.
    /// Should specify the license type (e.g., MIT, Apache 2.0).
    /// </summary>
    public string? License { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin supports hot reload.
    /// When true, the plugin can be reloaded without restarting the application.
    /// </summary>
    public bool SupportsHotReload { get; set; }

    /// <summary>
    /// Gets or sets the priority for plugin loading.
    /// Higher values are loaded first. Default is 0.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the load priority for backward compatibility.
    /// This is an alias for the Priority property.
    /// </summary>
    public int LoadPriority
    {
        get => Priority;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "LoadPriority must be non-negative.");
            }

            Priority = value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAttribute"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for the plugin.</param>
    /// <param name="name">The display name of the plugin.</param>
    /// <exception cref="ArgumentNullException">Thrown when id or name is null.</exception>
    public PluginAttribute(string id, string name)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAttribute"/> class with just a name.
    /// The name is used as both the ID and display name.
    /// </summary>
    /// <param name="name">The display name of the plugin (also used as ID).</param>
    /// <exception cref="ArgumentException">Thrown when name is null, empty, or whitespace.</exception>
    public PluginAttribute(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Plugin name cannot be null or empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Plugin name cannot be whitespace.", nameof(name));
        }

        Id = name;
        Name = name;
    }
}