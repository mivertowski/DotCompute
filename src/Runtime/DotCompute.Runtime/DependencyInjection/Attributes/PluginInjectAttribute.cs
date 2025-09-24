// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Attributes;

/// <summary>
/// Attribute to mark properties for dependency injection.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PluginInjectAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInjectAttribute"/> class.
    /// </summary>
    /// <param name="required">Whether the dependency is required.</param>
    public PluginInjectAttribute(bool required = true)
    {
        Required = required;
    }

    /// <summary>
    /// Gets whether the dependency is required.
    /// </summary>
    public bool Required { get; }
}